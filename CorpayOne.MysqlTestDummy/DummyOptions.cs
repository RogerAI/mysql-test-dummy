using System.Diagnostics;

namespace CorpayOne.MysqlTestDummy;

/// <summary>
/// Options used to generate or retrieve a dummy table value.
/// </summary>
/// <typeparam name="TId">The type of the primary key for this table.</typeparam>
public class DummyOptions<TId> : DummyOptions
{
    [DebuggerStepThrough]
    public DummyOptions() : base(typeof(TId))
    {
    }

    [DebuggerStepThrough]
    public DummyOptions<TId> WithForeignKey(string columnName, TId value)
    {
        ForeignKeys ??= new Dictionary<string, object>();

        ForeignKeys[columnName] = value!;

        return this;
    }

    [DebuggerStepThrough]
    public DummyOptions<TId> WithColumnValue(string columnName, object? value)
    {
        ColumnValues ??= new();

        ColumnValues[columnName] = value;
        return this;
    }

    [DebuggerStepThrough]
    public DummyOptions<TId> WithRandomSeed(int seed)
    {
        RandomSeed = seed;
        return this;
    }

    [DebuggerStepThrough]
    public DummyOptions<TId> MustForceCreate()
    {
        ForceCreate = true;
        return this;
    }

    [DebuggerStepThrough]
    public DummyOptions<TId> MustForcePopulateOptionalColumns()
    {
        ForcePopulateOptionalColumns = true;
        return this;
    }

    public virtual DummyOptions<TId> Clone() =>
        new DummyOptions<TId>()
        {
            ColumnValues = new Dictionary<string, object?>(ColumnValues ?? new()),
            DatabaseName = DatabaseName,
            DefaultEmailDomain = DefaultEmailDomain,
            DefaultUrl = DefaultUrl,
            ForceCreate = ForceCreate,
            ForcePopulateOptionalColumns = ForcePopulateOptionalColumns,
            ForeignKeys = new Dictionary<string, object>(ForeignKeys ?? new()),
            RandomSeed = RandomSeed
        };
}

public class DummyOptions
{
    /// <summary>
    /// The type of the id for the table.
    /// </summary>
    public Type IdType { get; }

    /// <summary>
    /// The schema/database name to use, will be automatically detected from the connection if not provided.
    /// </summary>
    public string? DatabaseName { get; set; }

    /// <summary>
    /// Override values for any columns that would otherwise be automatically populated.
    /// </summary>
    public Dictionary<string, object?> ColumnValues { get; set; } = new();

    /// <summary>
    /// Override any foreign keys that would otherwise be automatically populated.
    /// </summary>
    public Dictionary<string, object> ForeignKeys { get; set; } = new();

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

    public DummyOptions(Type idType)
    {
        IdType = idType;
    }
}