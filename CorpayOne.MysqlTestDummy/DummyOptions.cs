namespace CorpayOne.MysqlTestDummy;

public class DummyOptions<TId>
{
    public string? DatabaseName { get; set; }

    public List<ColumnValue> ColumnValues { get; set; } = new ();

    public List<ForeignKeyValue> ForeignKeys { get; set; } = new ();

    public int? RandomSeed { get; set; }

    public bool ForceCreate { get; set; }

    public string DefaultUrl { get; set; } = "https://www.wikipedia.org/";

    public string DefaultEmailDomain { get; set; } = "@mailinator.com";

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