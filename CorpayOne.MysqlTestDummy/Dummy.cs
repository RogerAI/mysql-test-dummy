using System.Data;
using System.Data.Common;
using System.Globalization;

namespace CorpayOne.MysqlTestDummy;

public static class Dummy
{
    // TODO: composite PKs?
    private const string GetPrimaryKey =
        @"      SELECT  COLUMN_NAME
                    FROM    INFORMATION_SCHEMA.COLUMNS
                    WHERE   TABLE_SCHEMA = @schema
                    AND     TABLE_NAME = @table
                    AND COLUMN_KEY = 'PRI'
                    LIMIT 1;";

    private const string GetColumnSchema =
        @"      SELECT  COLUMN_NAME as Name,
                            COLUMN_DEFAULT as ColumnDefault,
                            IS_NULLABLE as IsNullable,
                            DATA_TYPE as DataType,
                            CHARACTER_MAXIMUM_LENGTH as MaxLength
                    FROM    INFORMATION_SCHEMA.COLUMNS
                    WHERE   TABLE_NAME = @table
                    AND     TABLE_SCHEMA = @schema;";

    private const string GetForeignKeySchema =
        @"      SELECT  COLUMN_NAME as ColumnName,
                            REFERENCED_TABLE_NAME as TargetTableName
                    FROM    INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                    WHERE   TABLE_NAME = @table
                    AND     TABLE_SCHEMA = @schema
                    AND     REFERENCED_TABLE_NAME IS NOT NULL;";

    private static string GetSchemaName<TId>(IDbConnection connection, DummyOptions<TId>? dummyOptions = null)
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
                $"Cannot establish the desired database name from the provided connection, you can provide it in {nameof(DummyOptions<TId>.DatabaseName)} instead.");
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

    private static bool TryGetFromOptional<TId>(object? value, out TId? id)
    {
        id = default;

        if (value == null)
        {
            return false;
        }

        var idType = typeof(TId);

        if (idType == typeof(int))
        {
            switch (value)
            {
                case int i:
                    id = (TId)(object)i;
                    break;
                case long l:
                    id = (TId)(object)(int)l;
                    break;
                case uint u:
                    id = (TId)(object)(int)u;
                    break;
                case ulong ul:
                    id = (TId)(object)(int)ul;
                    break;
                case short sh:
                    id = (TId)(object)(int)sh;
                    break;
                case ushort us:
                    id = (TId)(object)(int)us;
                    break;
                default:
                    return false;
            }

            return true;
        }

        if (idType == typeof(long))
        {
            switch (value)
            {
                case int i:
                    id = (TId)(object)(long)i;
                    break;
                case long l:
                    id = (TId)(object)l;
                    break;
                case uint u:
                    id = (TId)(object)(long)u;
                    break;
                case ulong ul:
                    id = (TId)(object)(long)ul;
                    break;
                case short sh:
                    id = (TId)(object)(long)sh;
                    break;
                case ushort us:
                    id = (TId)(object)(long)us;
                    break;
                default:
                    return false;
            }

            return true;
        }

        if (idType == typeof(string) && value is string s)
        {
            id = (TId)(object)s;
            return !string.IsNullOrWhiteSpace(s);
        }

        // TODO: Guids.
        if (idType == typeof(Guid))
        {
            return false;
        }

        return false;
    }

    public static TId GetOrCreateId<TId>(IDbConnection connection, string tableName, DummyOptions<TId>? dummyOptions = null)
    {
        dummyOptions ??= new DummyOptions<TId>();

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

        using var command = PrepareCommand(connection, GetPrimaryKey, parameters);

        var primaryKeyName = command.ExecuteScalar() as string;

        if (string.IsNullOrWhiteSpace(primaryKeyName))
        {
            throw new InvalidOperationException($"Could not find primary key column name on {schemaName}.{tableName}");
        }

        if (!dummyOptions.ForceCreate)
        {
            using var idCommand = PrepareCommand(connection, $"SELECT {primaryKeyName} FROM {tableName} LIMIT 1;");

            var id = idCommand.ExecuteScalar();

            if (TryGetFromOptional<TId>(id, out var result))
            {
                return result!;
            }
        }

        var (columnSchema, foreignKeySchema) = GetTableSchema(connection, parameters);

        var random = dummyOptions.RandomSeed.HasValue ? new Random(dummyOptions.RandomSeed.Value) : new Random();

        var requiredColumns = FilterRequiredColumns(columnSchema, primaryKeyName, dummyOptions);

        var isGuidPk = typeof(TId) == typeof(Guid);
        var idInsertPartString = isGuidPk ? $"`{primaryKeyName}`, " : string.Empty;
        var idValuesPartString = isGuidPk ? $"@{primaryKeyName}, " : string.Empty;

        var insertPart = $"INSERT INTO {tableName} ({idInsertPartString}{string.Join(", ", requiredColumns.Select(x => $"`{x.Name}`"))}) ";

        var valuesPart = $"VALUES ({idValuesPartString}{string.Join(", ", requiredColumns.Select(x => $"@{x.Name}"))})";

        var previouslyLocatedFks = new Dictionary<string, TId>(StringComparer.OrdinalIgnoreCase);

        var dynamicParameters = new Dictionary<string, object?>();

        Guid? returnedGuid = null;
        if (isGuidPk)
        {
            // TODO: MySQL 8 supports auto-generation.
            returnedGuid = Guid.NewGuid();
            dynamicParameters[primaryKeyName] = returnedGuid.Value.ToByteArray();
        }

        foreach (var column in requiredColumns)
        {
            var hasOverride = dummyOptions.ColumnValues.TryGetValue(column.Name, out var overrideValue);

            object? value = overrideValue;

            if (!hasOverride)
            {
                switch (column.DataType.ToLowerInvariant())
                {
                    case "int":
                    case "double":
                    case "bigint":
                        if (dummyOptions.ForeignKeys.TryGetValue(column.Name, out var matchId))
                        {
                            value = matchId;
                            break;
                        }

                        var referencedFk =
                            foreignKeySchema.FirstOrDefault(
                                x => string.Equals(x.ColumnName, column.Name)
                                     && !string.Equals(x.TargetTableName, tableName));

                        if (referencedFk != null)
                        {
                            if (previouslyLocatedFks.TryGetValue(referencedFk.TargetTableName, out var fkId))
                            {
                                value = fkId;
                                break;
                            }

                            var generated = GetOrCreateId(
                                connection,
                                referencedFk.TargetTableName,
                                new DummyOptions<TId>
                                {
                                    DatabaseName = dummyOptions.DatabaseName,
                                    ForceCreate = dummyOptions.ForceCreate,
                                    RandomSeed = dummyOptions.RandomSeed,
                                    DefaultUrl = dummyOptions.DefaultUrl,
                                    DefaultEmailDomain = dummyOptions.DefaultEmailDomain
                                });

                            previouslyLocatedFks[referencedFk.TargetTableName] = generated;

                            value = generated;

                            break;
                        }

                        if (FactoryIfColumnNamed(column, "Amount", random.Next(500, 10000), ref value))
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
                        if (FactoryIfColumnNamed(column, "Country", "US", ref value))
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
                        else
                        {
                            value = (column.MaxLength.HasValue && column.MaxLength < 200)
                                ? GenerateRandomStringOfLengthOrLower(column.MaxLength.Value, random, true)
                                : GenerateRandomStringOfLengthOrLower(90, random, true);
                        }

                        break;
                    case "char":
                        value = GenerateRandomStringOfLength(column.MaxLength.GetValueOrDefault(), random, false);
                        break;
                    case "tinyint":
                        value = 0;
                        break;
                    case "timestamp":
                    case "datetime":
                        value = DateTime.UtcNow;
                        break;
                    case "date":
                        value = DateTime.UtcNow.Date;
                        break;
                }
            }

            dynamicParameters.Add(column.Name, value);
        }

        var insertCommand = $"{insertPart}{valuesPart}; SELECT LAST_INSERT_ID();";

        using var finalInsertCommand = PrepareCommand(connection, insertCommand, dynamicParameters);

        object? finalResult;
        try
        {
            finalResult = finalInsertCommand.ExecuteScalar();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to execute insert into {tableName} with statement {insertCommand}.", ex);
        }

        if (isGuidPk)
        {
            return (TId)(object)returnedGuid!.Value;
        }

        if (!TryGetFromOptional<TId>(finalResult, out var finalId))
        {
            throw new InvalidOperationException($"Failed to execute insert into {tableName} with statement {insertCommand}.");
        }

        return finalId!;
    }

    public static TId CreateId<TId>(IDbConnection connection, string tableName, DummyOptions<TId>? dummyOptions = null)
    {
        dummyOptions ??= new DummyOptions<TId>();

        dummyOptions.ForceCreate = true;

        return GetOrCreateId(connection, tableName, dummyOptions);
    }

    private static List<ColumnSchemaEntry> FilterRequiredColumns<TId>(List<ColumnSchemaEntry> columns, string primaryKeyName, DummyOptions<TId> dummyOptions)
    {
        if (dummyOptions.ForcePopulateOptionalColumns)
        {
            return columns.Where(x => !string.Equals(x.Name, primaryKeyName)).ToList();
        }

        var result = new List<ColumnSchemaEntry>();

        foreach (var column in columns)
        {
            if (string.Equals(column.Name, primaryKeyName))
            {
                continue;
            }

            if (!column.IsActuallyNullable && string.IsNullOrWhiteSpace(column.ColumnDefault))
            {
                result.Add(column);
            }
            else if (dummyOptions.ColumnValues.Any(x => string.Equals(x.Key, column.Name)))
            {
                result.Add(column);
            }
            else if (dummyOptions.ForeignKeys.Any(x => string.Equals(x.Key, column.Name)))
            {
                result.Add(column);
            }
        }

        return result;
    }

    private static (List<ColumnSchemaEntry> columns, List<ForeignKeySchemaEntry> foreignKeys) GetTableSchema(IDbConnection connection, Dictionary<string, object?> parameters)
    {
        var columnSchema = new List<ColumnSchemaEntry>();
        var foreignKeySchema = new List<ForeignKeySchemaEntry>();

        using var sharedCommand = PrepareCommand(connection, GetColumnSchema, parameters);
        {
            using (var reader = sharedCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    var colName = reader.GetString(0);
                    var colDefault = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var isNullable = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var dataType = reader.GetString(3);
                    var maxLength = reader.IsDBNull(4) ? default : reader.GetInt32(4);

                    var entry = new ColumnSchemaEntry(colName, dataType)
                    {
                        ColumnDefault = colDefault,
                        IsNullable = isNullable,
                        MaxLength = maxLength
                    };

                    columnSchema.Add(entry);
                }
            }

            sharedCommand.CommandText = GetForeignKeySchema;
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
        }

        return (columnSchema, foreignKeySchema);
    }

    private static string GenerateRandomStringOfLengthOrLower(int length, Random random, bool allowWhitespace)
    {
        var lengthToFill = random.Next(length / 2, length);

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

        public int? MaxLength { get; set; }

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
}