namespace CorpayOne.MysqlTestDummy;

/// <summary>
/// Options used to generate or retrieve a dummy table value.
/// </summary>
/// <typeparam name="TId">The type of the primary key for this table.</typeparam>
public class DummyOptions<TId>
{
    /// <summary>
    /// The schema/database name to use, will be automatically detected from the connection if not provided.
    /// </summary>
    public string? DatabaseName { get; set; }

    /// <summary>
    /// Override values for any columns that would otherwise be automatically populated.
    /// </summary>
    public List<ColumnValue> ColumnValues { get; set; } = new ();

    /// <summary>
    /// Override any foreign keys that would otherwise be automatically populated.
    /// </summary>
    public List<ForeignKeyValue> ForeignKeys { get; set; } = new ();

    /// <summary>
    /// Overrides the seed used for random data generation to populate columns.
    /// </summary>
    public int? RandomSeed { get; set; }

    /// <summary>
    /// Whether the entity should be created even if one exists.
    /// </summary>
    public bool ForceCreate { get; set; }

    /// <summary>
    /// The default URL used for Link or URL named columns.
    /// </summary>
    public string DefaultUrl { get; set; } = "https://www.wikipedia.org/";

    /// <summary>
    /// The default email domain appended to email fields.
    /// </summary>
    public string DefaultEmailDomain { get; set; } = "@mailinator.com";

    /// <summary>
    /// Include test data in all columns which are marked as nullable.
    /// </summary>
    public bool ForcePopulateOptionalColumns { get; set; }

    /// <summary>
    /// Used to hardcode the value of the foreign key if you have an existing entity you want linked.
    /// </summary>
    public class ForeignKeyValue
    {
        /// <summary>
        /// The column for the foreign key to hardcode.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// The identifier for the foreign key to hardcode.
        /// </summary>
        public TId Value { get; }

        /// <summary>
        /// Create a new <see cref="ForeignKeyValue"/>.
        /// </summary>
        public ForeignKeyValue(string columnName, TId value)
        {
            ColumnName = columnName;
            Value = value;
        }
    }

    public class ColumnValue
    {
        public string ColumnName { get; }

        public object? Value { get; }

        public ColumnValue(string columnName, object? value)
        {
            ColumnName = columnName;
            Value = value;
        }
    }
}