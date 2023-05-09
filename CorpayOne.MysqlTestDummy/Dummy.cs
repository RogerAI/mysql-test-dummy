using System.Data;
using System.Data.Common;
using System.Globalization;

namespace CorpayOne.MysqlTestDummy;

public static class Dummy
{
    private const string GetColumnSchemaSql =
        @"      SELECT      COLUMN_NAME as Name,
                            COLUMN_DEFAULT as ColumnDefault,
                            IS_NULLABLE as IsNullable,
                            DATA_TYPE as DataType,
                            CHARACTER_MAXIMUM_LENGTH as MaxLength,
                            (COLUMN_KEY = 'PRI') as IsPrimary,
                            (EXTRA = 'auto_increment') as Auto
                    FROM    INFORMATION_SCHEMA.COLUMNS
                    WHERE   TABLE_NAME = @table
                        AND TABLE_SCHEMA = @schema;";

    private const string GetForeignKeySchemaSql =
        @"      SELECT      COLUMN_NAME as ColumnName,
                            REFERENCED_TABLE_NAME as TargetTableName
                    FROM    INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                    WHERE   TABLE_NAME = @table
                        AND TABLE_SCHEMA = @schema
                        AND REFERENCED_TABLE_NAME IS NOT NULL;";

    private const string GetUniqueConstraintSchemaSql =
        @"      SELECT      tc.CONSTRAINT_NAME,
                            cu.COLUMN_NAME
                FROM        INFORMATION_SCHEMA.TABLE_CONSTRAINTS as tc
                INNER JOIN  INFORMATION_SCHEMA.KEY_COLUMN_USAGE as cu
                    ON      cu.TABLE_SCHEMA = tc.TABLE_SCHEMA
                    AND     cu.TABLE_NAME = tc.TABLE_NAME
                    AND     cu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME 
                WHERE       tc.TABLE_NAME = @table
                    AND     tc.TABLE_SCHEMA = @schema
                    AND     tc.CONSTRAINT_TYPE = 'UNIQUE';";

    private static string GetSchemaName(IDbConnection connection, DummyOptions? dummyOptions = null)
    {
        if (!string.IsNullOrWhiteSpace(dummyOptions?.DatabaseName))
        {
            return dummyOptions.DatabaseName;
        }

        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = connection.ConnectionString
        };

        if (!builder.TryGetValue("database", out var databaseName) || databaseName is not string dbName || string.IsNullOrWhiteSpace(dbName))
        {
            throw new InvalidOperationException(
                $"Cannot establish the desired database name from the provided connection, you can provide it in {nameof(DummyOptions.DatabaseName)} instead.");
        }

        return dbName;
    }

    private static IDbCommand PrepareCommand(IDbConnection connection, string sql, Dictionary<string, object?>? parameters = null)
    {
        var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = sql;

        if (parameters != null)
        {
            foreach (var (name, val) in parameters)
            {
                if (val == null)
                {
                    continue;
                }

                AddParameter(command, name, val);
            }
        }

        return command;
    }

    public static TId CreateId<TId>(IDbConnection connection, string tableName, DummyOptions<TId>? dummyOptions = null)
    {
        dummyOptions ??= new DummyOptions<TId>();

        dummyOptions.ForceCreate = true;

        return GetOrCreateId(connection, tableName, dummyOptions);
    }

    public static TId GetOrCreateId<TId>(
        IDbConnection connection,
        string tableName,
        DummyOptions<TId>? dummyOptions = null)
    {
        return (TId)GetOrCreateId(typeof(TId), connection, tableName, dummyOptions);
    }

    public static object GetOrCreateId(
        Type idType,
        IDbConnection connection,
        string tableName,
        DummyOptions? dummyOptions = null)
    {
        var visited = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        return GetOrCreateIdRecursive(
            idType,
            connection,
            tableName,
            dummyOptions ?? new DummyOptions(idType),
            visited,
            new List<string>());
    }

    private static object GetOrCreateIdRecursive(
        Type idType,
        IDbConnection connection,
        string tableName,
        DummyOptions dummyOptions,
        Dictionary<string, object> previouslyVisitedTables,
        IReadOnlyList<string> referencePath)
    {
        var schemaName = GetSchemaName(connection, dummyOptions);

        if (string.IsNullOrWhiteSpace(dummyOptions.DatabaseName))
        {
            dummyOptions.DatabaseName = schemaName;
        }

        var parameters = new Dictionary<string, object?>
        {
            { "schema", schemaName },
            { "table", tableName }
        };

        var schema = GetTableSchema(connection, parameters, tableName);

        var columnSchema = schema.Columns;
        var foreignKeySchema = schema.ForeignKeys;

        if (columnSchema.Count == 0)
        {
            throw new InvalidOperationException(
                $"Could not find columns on table {schemaName}.{tableName}, did you get the table name wrong?");
        }

        var primaryKeyColumns = columnSchema.Where(x => x.IsPrimary).ToList();

        if (primaryKeyColumns.Count == 0)
        {
            throw new InvalidOperationException($"Could not find primary key column name(s) on {schemaName}.{tableName}");
        }

        if (!dummyOptions.ForceCreate)
        {
            var colsSelect = string.Join(", ", primaryKeyColumns.Select(x => x.EscapedColumnName));
            if (TryReadExistingRecord(
                    idType,
                    connection,
                    $"SELECT {colsSelect} FROM `{tableName}` LIMIT 1;",
                    null, out var existingResult))
            {
                return existingResult!;
            }
        }

        var stringComparer = dummyOptions.ColumnsCaseSensitive
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;
        var columnValueLookup = new Dictionary<string, object?>(dummyOptions.ColumnValues, stringComparer);
        var foreignKeyLookup = new Dictionary<string, object>(dummyOptions.ForeignKeys, stringComparer);

        var random = dummyOptions.RandomSeed.HasValue ? new Random(dummyOptions.RandomSeed.Value) : new Random();

        var requiredColumns = FilterRequiredColumns(columnSchema, dummyOptions);

        var colNameList = requiredColumns.Select(x => x.EscapedColumnName);

        var paramNameList = requiredColumns.Select(x => x.ParamName);

        var colNamePart = string.Join(", ", colNameList);
        var paramNamePart = string.Join(", ", paramNameList);

        var insertPart = $"INSERT INTO `{tableName}` ({colNamePart}) ";
        var valuesPart = $"VALUES ({paramNamePart})";
        var dynamicParameters = new Dictionary<string, object?>();

        foreach (var column in requiredColumns)
        {
            var hasOverride = columnValueLookup.TryGetValue(column.Name, out var overrideValue);

            object? value = overrideValue;

            if (!hasOverride)
            {
                switch (column.DataType.ToLowerInvariant())
                {
                    case "int":
                    case "double":
                    case "bigint":
                        if (foreignKeyLookup.TryGetValue(column.Name, out var matchId))
                        {
                            value = matchId;
                            break;
                        }

                        var referencedFk =
                            foreignKeySchema.FirstOrDefault(x => string.Equals(x.ColumnName, column.Name));

                        if (referencedFk != null)
                        {
                            if (previouslyVisitedTables.TryGetValue(referencedFk.TargetTableName, out var fkId))
                            {
                                value = fkId;
                                break;
                            }

                            var isSelfReferential = string.Equals(referencedFk.TargetTableName, tableName);
                            var foreignKeyCreateOptions = new DummyOptions<int>
                            {
                                DatabaseName = dummyOptions.DatabaseName,
                                DefaultUrl = dummyOptions.DefaultUrl,
                                DefaultEmailDomain = dummyOptions.DefaultEmailDomain,
                                ForceCreate = dummyOptions.ForceCreate,
                                ForcePopulateOptionalColumns = !isSelfReferential && dummyOptions.ForcePopulateOptionalColumns,
                                RandomSeed = dummyOptions.RandomSeed
                            };

                            if (referencePath.Contains(referencedFk.TargetTableName))
                            {
                                if (column.IsActuallyNullable)
                                {
                                    value = DBNull.Value;
                                    break;
                                }

                                if (referencePath.Count(x => x == referencedFk.TargetTableName) > 1)
                                {
                                    var tablesList = string.Join(" > ", referencePath);

                                    throw new InvalidOperationException(
                                        "Schema contains unresolvable circular reference. " +
                                        $"Column {column.Name} on table {tableName} refers back to a table in the path ({tablesList}) which requires this table to be populated. " +
                                        "Consider making this key or one of the others nullable.");
                                }
                            }

                            var generated = GetOrCreateIdRecursive(
                                typeof(int),
                                connection,
                                referencedFk.TargetTableName,
                                foreignKeyCreateOptions,
                                previouslyVisitedTables,
                                new List<string>(referencePath)
                                {
                                    tableName
                                });

                            previouslyVisitedTables[referencedFk.TargetTableName] = generated;

                            value = generated;

                            break;
                        }

                        var uniqueFor = schema.UniqueConstraints.FirstOrDefault(x => ReferenceEquals(x.SelectedUniqueColumn, column));

                        if (uniqueFor != null)
                        {
                            value = random.Next(0, 100000);
                            do
                            {
                                value = ((int)value) + 3;
                            } while (uniqueFor.DisallowedValues.Contains(value));
                        }
                        else if (FactoryIfColumnNamed(column, "Amount", random.Next(500, 10000), ref value))
                        {
                        }
                        else if (FactoryIfColumnNamed(column, "Month", random.Next(1, 13), ref value))
                        {
                        }
                        else if (FactoryIfColumnNamed(column, "Year", random.Next(2017, 2038), ref value))
                        {
                        }
                        else
                        {
                            value = random.Next(0, 100);
                        }

                        break;
                    case "varchar":
                    case "text":
                    case "longtext":
                    case "char":
                        if (column.IsPrimary && (column.MaxLength >= 32 || !column.MaxLength.HasValue))
                        {
                            value = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
                        }
                        else if (FactoryIfColumnNamed(column, "Country", "US", ref value))
                        {
                        }
                        else if (FactoryIfColumnNamed(column, "Currency", "USD", ref value))
                        {
                        }
                        else if (FactoryIfColumnNamed(column, "Culture", "en-US", ref value))
                        {
                        }
                        else if (FactoryIfColumnNamed(
                                     column,
                                     "Email",
                                     GenerateRandomStringOfLengthOrLower(20, random, false) + dummyOptions.DefaultEmailDomain,
                                     ref value))
                        {
                        }
                        else if (FactoryIfColumnNamed(column, "Guid", Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture), ref value))
                        {
                        }
                        else if (FactoryIfColumnNamed(column, "Url", dummyOptions.DefaultUrl, ref value)
                                 || FactoryIfColumnNamed(column, "Link", dummyOptions.DefaultUrl, ref value))
                        {
                        }
                        else if (FactoryIfColumnNamed(column, "Iban", "DK5000400440116243", ref value))
                        {
                        }
                        else if (string.Equals(column.DataType, "char", StringComparison.OrdinalIgnoreCase))
                        {
                            value = GenerateRandomStringOfLength((int)column.MaxLength.GetValueOrDefault(), random, false);
                        }
                        else
                        {
                            value = (column.MaxLength.HasValue && column.MaxLength < 200)
                                ? GenerateRandomStringOfLengthOrLower(column.MaxLength.Value, random, true)
                                : GenerateRandomStringOfLengthOrLower(90, random, true);
                        }
                        break;
                    case "json":
                        value = "{ \"id\": 1 }";
                        break;
                    case "tinyint":
                    case "bit":
                        value = random.Next(2);
                        break;
                    case "timestamp":
                    case "datetime":
                        value = DateTime.UtcNow;
                        break;
                    case "date":
                        value = DateTime.UtcNow.Date;
                        break;
                    case "binary":
                        if (foreignKeyLookup.TryGetValue(column.Name, out matchId))
                        {
                            value = matchId;
                            break;
                        }

                        referencedFk =
                            foreignKeySchema.FirstOrDefault(x => string.Equals(x.ColumnName, column.Name));

                        if (referencedFk != null)
                        {
                            if (previouslyVisitedTables.TryGetValue(referencedFk.TargetTableName, out var fkId))
                            {
                                value = fkId;
                                break;
                            }

                            var isSelfReferential = string.Equals(referencedFk.TargetTableName, tableName);

                            var foreignKeyCreateOptions = new DummyOptions(typeof(Guid))
                            {
                                DatabaseName = dummyOptions.DatabaseName,
                                DefaultUrl = dummyOptions.DefaultUrl,
                                DefaultEmailDomain = dummyOptions.DefaultEmailDomain,
                                ForceCreate = dummyOptions.ForceCreate,
                                ForcePopulateOptionalColumns = !isSelfReferential && dummyOptions.ForcePopulateOptionalColumns,
                                RandomSeed = dummyOptions.RandomSeed
                            };

                            var generated = GetOrCreateId(
                                typeof(Guid),
                                connection,
                                referencedFk.TargetTableName,
                                foreignKeyCreateOptions);

                            previouslyVisitedTables[referencedFk.TargetTableName] = generated;

                            value = generated;

                            break;
                        }

                        if (column.MaxLength == 16)
                        {
                            value = Guid.NewGuid().ToByteArray();
                        }
                        else if (column.MaxLength.HasValue)
                        {
                            value = new byte[column.MaxLength.Value];
                        }
                        break;
                }
            }

            dynamicParameters.Add(column.Name, value);
        }

        return CreateFinal(
            idType,
            connection,
            primaryKeyColumns,
            insertPart,
            valuesPart,
            dynamicParameters,
            tableName);
    }

    private static bool TryReadExistingRecord(
        Type type,
        IDbConnection connection,
        string sql,
        Dictionary<string, object?>? parameters,
        out object? result)
    {
        result = default;

        using var idCommand = PrepareCommand(connection, sql, parameters);

        using var reader = idCommand.ExecuteReader();

        while (reader.Read())
        {
            if (IdMapper.TryReadOptional(type, reader, out result))
            {
                return true;
            }
        }

        return false;
    }

    private static object CreateFinal(
        Type type,
        IDbConnection connection,
        IReadOnlyList<ColumnSchemaEntry> primaryKeys,
        string insertPart,
        string valuesPart,
        Dictionary<string, object?> dynamicParameters,
        string tableName)
    {
        var useLastInsert = primaryKeys.Count == 1 && primaryKeys[0].Auto;

        string insertCommand = string.Empty;
        try
        {
            if (useLastInsert)
            {
                insertCommand = $"{insertPart}{valuesPart}; SELECT LAST_INSERT_ID();";
            }
            else
            {
                var colsList = string.Join(", ", primaryKeys.Select(x => x.EscapedColumnName));
                var filters = string.Join(" AND ", primaryKeys.Select(x => $"{x.EscapedColumnName} = {x.ParamName}"));
                var selectPostInsert = $"SELECT {colsList} FROM {tableName} WHERE {filters};";
                insertCommand = $"{insertPart}{valuesPart};{selectPostInsert}";
            }

            using var finalInsertCommand = PrepareCommand(connection, insertCommand, dynamicParameters);
            using var insertReader = finalInsertCommand.ExecuteReader();

            if (!insertReader.Read())
            {
                throw new InvalidOperationException(
                    $"Failed to execute insert into {tableName} with statement {insertCommand}.");
            }

            if (!IdMapper.TryReadOptional(type, insertReader, out var finalId))
            {
                throw new InvalidOperationException($"Failed to execute insert into {tableName} with statement {insertCommand}.");
            }

            return finalId!;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to execute insert into {tableName} with statement {insertCommand}.", ex);
        }
    }

    private static List<ColumnSchemaEntry> FilterRequiredColumns(
        List<ColumnSchemaEntry> columns,
        DummyOptions dummyOptions)
    {
        var isComposite = columns.Count(x => x.IsPrimary) > 1;

        var result = new List<ColumnSchemaEntry>();

        var stringComparer = dummyOptions.ColumnsCaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        foreach (var column in columns)
        {
            if (isComposite && column.IsPrimary)
            {
                result.Add(column);
            }
            else if (column.IsPrimary && !column.Auto)
            {
                result.Add(column);
            }

            if (column.IsPrimary)
            {
                continue;
            }

            if (dummyOptions.ForcePopulateOptionalColumns)
            {
                result.Add(column);
            }
            else if (!column.IsActuallyNullable && string.IsNullOrWhiteSpace(column.ColumnDefault))
            {
                result.Add(column);
            }
            else if (dummyOptions.ColumnValues.Any(x =>
                         string.Equals(x.Key, column.Name, stringComparer)))
            {
                result.Add(column);
            }
            else if (dummyOptions.ForeignKeys.Any(x => string.Equals(x.Key, column.Name, stringComparer)))
            {
                result.Add(column);
            }
        }

        return result;
    }

    private static TableSchema GetTableSchema(
        IDbConnection connection,
        Dictionary<string, object?> parameters,
        string tableName)
    {
        var columnSchema = new List<ColumnSchemaEntry>();
        var foreignKeySchema = new List<ForeignKeySchemaEntry>();
        var uniqueConstraints = new List<UniqueConstraint>();

        using var sharedCommand = PrepareCommand(connection, GetColumnSchemaSql, parameters);
        {
            using (var reader = sharedCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    var colName = reader.GetString(0);
                    var colDefault = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var isNullable = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var dataType = reader.GetString(3);
                    var maxLength = reader.IsDBNull(4) ? default : reader.GetInt64(4);
                    var isPrimary = reader.GetBoolean(5);
                    var auto = reader.GetBoolean(6);

                    var entry = new ColumnSchemaEntry(colName, dataType)
                    {
                        ColumnDefault = colDefault,
                        IsNullable = isNullable,
                        MaxLength = maxLength,
                        IsPrimary = isPrimary,
                        Auto = auto
                    };

                    columnSchema.Add(entry);
                }
            }

            sharedCommand.CommandText = GetForeignKeySchemaSql;
            using (var reader = sharedCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    var colName = reader.GetString(0);
                    var tabName = reader.GetString(1);

                    var entry = new ForeignKeySchemaEntry(colName, tabName);

                    foreignKeySchema.Add(entry);
                }
            }

            var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            sharedCommand.CommandText = GetUniqueConstraintSchemaSql;
            using (var reader = sharedCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    var constraintName = reader.GetString(0);
                    var columnName = reader.GetString(1);

                    if (!dict.TryGetValue(constraintName, out var cols))
                    {
                        cols = new List<string>();
                        dict[constraintName] = cols;
                    }

                    cols.Add(columnName);
                }
            }

            foreach (var (name, colNames) in dict)
            {
                // This doesn't support foreign keys as part of the unique constraint currently which is a big gap...

                // For each set of unique constraints find the column that can be made unique most easily.
                var cols = colNames.Select(x => columnSchema.Single(y => string.Equals(x, y.Name))).ToList();

                var existing = new List<object>();
                ColumnSchemaEntry? selected = null;
                foreach (var col in cols)
                {

                    var canBeSufficientlyRandomised =
                        col.IsBasicNumeric || string.Equals(col.DataType, "varchar",
                                               StringComparison.OrdinalIgnoreCase)
                                           || string.Equals(col.DataType, "text",
                                               StringComparison.OrdinalIgnoreCase)
                                           || string.Equals(col.DataType, "longtext",
                                               StringComparison.OrdinalIgnoreCase);

                    if (canBeSufficientlyRandomised)
                    {
                        sharedCommand.CommandText = $"SELECT {col.Name} FROM {tableName};";

                        using (var reader = sharedCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                existing.Add(reader.GetValue(0));
                            }
                        }

                        selected = col;
                        break;
                    }

                }

                if (selected == null)
                {
                    throw new InvalidOperationException(
                        $"Could not find a column to ensure uniqueness for the unique constraint {name} in {tableName}.");
                }

                uniqueConstraints.Add(new UniqueConstraint(
                    name,
                    cols,
                    selected,
                    existing));
            }
        }

        return new TableSchema(columnSchema, foreignKeySchema, uniqueConstraints);
    }

    private static string GenerateRandomStringOfLengthOrLower(long length, Random random, bool allowWhitespace)
    {
        int len;
        if (length > int.MaxValue)
        {
            len = 10000;
        }
        else
        {
            len = (int)length;
        }

        var lengthToFill = random.Next(len / 2, len);

        return GenerateRandomStringOfLength(lengthToFill, random, allowWhitespace);
    }

    private static string GenerateRandomStringOfLength(int length, Random random, bool allowWhitespace)
    {
        // Alphabetical characters with duplicates for English letter distribution plus some bonus Unicode values.
        const string chars = "aabcdeeeefghhijklmmnnoopqrrsstttuvwxyz";
        const string specialChars = "øüéà";

        const int multiwordTextLength = 30;

        var isPreviousSpace = false;
        var result = string.Empty;
        for (var i = 0; i < length; i++)
        {
            var isWhitespace = allowWhitespace && i > 0 && i < length - 2 && !isPreviousSpace && (random.Next(0, 20) >= 15 || i % multiwordTextLength == 0);
            if (isWhitespace)
            {
                isPreviousSpace = true;
                result += " ";
                continue;
            }

            var isSpecial = random.Next(0, 100) > 97;

            var character = isSpecial ? specialChars[random.Next(0, specialChars.Length)] : chars[random.Next(0, chars.Length)];

            if (i == 0 && length > multiwordTextLength)
            {
                character = char.ToUpperInvariant(character);
            }

            isPreviousSpace = false;
            result += character;
        }

        if (result.Length != length)
        {
            throw new InvalidOperationException($"Tried to create string of length {length} but was '{result}' ({result.Length})");
        }

        return result;
    }

    private static bool FactoryIfColumnNamed(ColumnSchemaEntry column, string snippet, object newValue, ref object? obj)
    {
        if (column.MaxLength.HasValue && newValue is string strVal && column.MaxLength.Value < strVal.Length)
        {
            return false;
        }

        if (column.Name.Contains(snippet, StringComparison.OrdinalIgnoreCase))
        {
            obj = newValue;
            return true;
        }

        return false;
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private class ColumnSchemaEntry
    {
        public string Name { get; }

        public string? ColumnDefault { get; set; }

        // ReSharper disable once MemberCanBePrivate.Local
        public string? IsNullable { get; set; }

        public string DataType { get; }

        public long? MaxLength { get; set; }

        public bool IsPrimary { get; set; }

        public string ParamName => $"@{Name}";

        public string EscapedColumnName => $"`{Name}`";

        public bool Auto { get; set; }

        public bool IsActuallyNullable
        {
            get
            {
                if (bool.TryParse(IsNullable, out var isNullable))
                {
                    return isNullable;
                }

                return string.Equals("Yes", IsNullable, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsBasicNumeric => string.Equals(DataType, "int", StringComparison.OrdinalIgnoreCase)
                                      || string.Equals(DataType, "bigint", StringComparison.OrdinalIgnoreCase)
                                      || string.Equals(DataType, "double", StringComparison.OrdinalIgnoreCase);

        public ColumnSchemaEntry(string name, string dataType)
        {
            Name = name;
            DataType = dataType;
        }

        public override string ToString()
        {
            return $"[{Name}] {DataType}";
        }
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private class ForeignKeySchemaEntry
    {
        public string ColumnName { get; }

        public string TargetTableName { get; }

        public ForeignKeySchemaEntry(string columnName, string targetTableName)
        {
            ColumnName = columnName;
            TargetTableName = targetTableName;
        }
    }

    private class UniqueConstraint
    {
        public string Name { get; }

        public List<ColumnSchemaEntry> Columns { get; }

        public ColumnSchemaEntry SelectedUniqueColumn { get; }

        public List<object> DisallowedValues { get; }

        public UniqueConstraint(
            string name,
            List<ColumnSchemaEntry> columns,
            ColumnSchemaEntry selectedUniqueColumn,
            List<object> disallowedValues)
        {
            Name = name;
            Columns = columns;
            SelectedUniqueColumn = selectedUniqueColumn;
            DisallowedValues = disallowedValues;
        }
    }

    private class TableSchema
    {
        public List<ColumnSchemaEntry> Columns { get; }

        public List<ForeignKeySchemaEntry> ForeignKeys { get; }

        public List<UniqueConstraint> UniqueConstraints { get; }

        public TableSchema(
            List<ColumnSchemaEntry> columns,
            List<ForeignKeySchemaEntry> foreignKeys,
            List<UniqueConstraint> uniqueConstraints)
        {
            Columns = columns;
            ForeignKeys = foreignKeys;
            UniqueConstraints = uniqueConstraints;
        }
    }
}