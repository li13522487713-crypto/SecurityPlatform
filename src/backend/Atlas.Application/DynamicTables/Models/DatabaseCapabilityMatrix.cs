namespace Atlas.Application.DynamicTables.Models;

/// <summary>
/// 数据库能力矩阵元数据，描述不同 DBMS 对 DDL 特性的支持范围。
/// 由 DDL 生成器和兼容性检查器引用，以裁剪/告警不支持的操作。
/// </summary>
public sealed class DatabaseCapabilityMatrix
{
    public static readonly DatabaseCapabilityMatrix Sqlite = new(
        DbType: "Sqlite",
        SupportsAlterColumnType: false,
        SupportsDropColumn: true,
        SupportsRenameColumn: true,
        SupportsAddColumnWithDefault: true,
        SupportsComputedColumns: false,
        SupportsCheckConstraints: true,
        SupportsForeignKeys: true,
        SupportsPartialIndex: true,
        SupportsFullTextIndex: false,
        SupportsJsonColumn: false,
        SupportsSequence: false,
        MaxIdentifierLength: 128,
        MaxColumnsPerTable: 2000,
        MaxIndexesPerTable: 64,
        SupportsOnlineSchemaChange: false,
        SupportsTransactionalDdl: true,
        SupportedFieldTypes: new[]
        {
            "Int", "Long", "Decimal", "String", "Text", "Bool",
            "DateTime", "Date", "Time", "Guid"
        });

    public static readonly DatabaseCapabilityMatrix SqlServer = new(
        DbType: "SqlServer",
        SupportsAlterColumnType: true,
        SupportsDropColumn: true,
        SupportsRenameColumn: true,
        SupportsAddColumnWithDefault: true,
        SupportsComputedColumns: true,
        SupportsCheckConstraints: true,
        SupportsForeignKeys: true,
        SupportsPartialIndex: true,
        SupportsFullTextIndex: true,
        SupportsJsonColumn: true,
        SupportsSequence: true,
        MaxIdentifierLength: 128,
        MaxColumnsPerTable: 1024,
        MaxIndexesPerTable: 999,
        SupportsOnlineSchemaChange: true,
        SupportsTransactionalDdl: true,
        SupportedFieldTypes: new[]
        {
            "Int", "Long", "Decimal", "String", "Text", "Bool",
            "DateTime", "Date", "Time", "Enum", "File", "Image",
            "Json", "Guid"
        });

    public static readonly DatabaseCapabilityMatrix MySql = new(
        DbType: "MySql",
        SupportsAlterColumnType: true,
        SupportsDropColumn: true,
        SupportsRenameColumn: true,
        SupportsAddColumnWithDefault: true,
        SupportsComputedColumns: true,
        SupportsCheckConstraints: true,
        SupportsForeignKeys: true,
        SupportsPartialIndex: false,
        SupportsFullTextIndex: true,
        SupportsJsonColumn: true,
        SupportsSequence: false,
        MaxIdentifierLength: 64,
        MaxColumnsPerTable: 1017,
        MaxIndexesPerTable: 64,
        SupportsOnlineSchemaChange: true,
        SupportsTransactionalDdl: false,
        SupportedFieldTypes: new[]
        {
            "Int", "Long", "Decimal", "String", "Text", "Bool",
            "DateTime", "Date", "Time", "Enum", "File", "Image",
            "Json", "Guid"
        });

    public static readonly DatabaseCapabilityMatrix PostgreSql = new(
        DbType: "PostgreSql",
        SupportsAlterColumnType: true,
        SupportsDropColumn: true,
        SupportsRenameColumn: true,
        SupportsAddColumnWithDefault: true,
        SupportsComputedColumns: true,
        SupportsCheckConstraints: true,
        SupportsForeignKeys: true,
        SupportsPartialIndex: true,
        SupportsFullTextIndex: true,
        SupportsJsonColumn: true,
        SupportsSequence: true,
        MaxIdentifierLength: 63,
        MaxColumnsPerTable: 1600,
        MaxIndexesPerTable: int.MaxValue,
        SupportsOnlineSchemaChange: true,
        SupportsTransactionalDdl: true,
        SupportedFieldTypes: new[]
        {
            "Int", "Long", "Decimal", "String", "Text", "Bool",
            "DateTime", "Date", "Time", "Enum", "File", "Image",
            "Json", "Guid"
        });

    private DatabaseCapabilityMatrix(
        string DbType,
        bool SupportsAlterColumnType,
        bool SupportsDropColumn,
        bool SupportsRenameColumn,
        bool SupportsAddColumnWithDefault,
        bool SupportsComputedColumns,
        bool SupportsCheckConstraints,
        bool SupportsForeignKeys,
        bool SupportsPartialIndex,
        bool SupportsFullTextIndex,
        bool SupportsJsonColumn,
        bool SupportsSequence,
        int MaxIdentifierLength,
        int MaxColumnsPerTable,
        int MaxIndexesPerTable,
        bool SupportsOnlineSchemaChange,
        bool SupportsTransactionalDdl,
        IReadOnlyList<string> SupportedFieldTypes)
    {
        this.DbType = DbType;
        this.SupportsAlterColumnType = SupportsAlterColumnType;
        this.SupportsDropColumn = SupportsDropColumn;
        this.SupportsRenameColumn = SupportsRenameColumn;
        this.SupportsAddColumnWithDefault = SupportsAddColumnWithDefault;
        this.SupportsComputedColumns = SupportsComputedColumns;
        this.SupportsCheckConstraints = SupportsCheckConstraints;
        this.SupportsForeignKeys = SupportsForeignKeys;
        this.SupportsPartialIndex = SupportsPartialIndex;
        this.SupportsFullTextIndex = SupportsFullTextIndex;
        this.SupportsJsonColumn = SupportsJsonColumn;
        this.SupportsSequence = SupportsSequence;
        this.MaxIdentifierLength = MaxIdentifierLength;
        this.MaxColumnsPerTable = MaxColumnsPerTable;
        this.MaxIndexesPerTable = MaxIndexesPerTable;
        this.SupportsOnlineSchemaChange = SupportsOnlineSchemaChange;
        this.SupportsTransactionalDdl = SupportsTransactionalDdl;
        this.SupportedFieldTypes = SupportedFieldTypes;
    }

    public string DbType { get; }
    public bool SupportsAlterColumnType { get; }
    public bool SupportsDropColumn { get; }
    public bool SupportsRenameColumn { get; }
    public bool SupportsAddColumnWithDefault { get; }
    public bool SupportsComputedColumns { get; }
    public bool SupportsCheckConstraints { get; }
    public bool SupportsForeignKeys { get; }
    public bool SupportsPartialIndex { get; }
    public bool SupportsFullTextIndex { get; }
    public bool SupportsJsonColumn { get; }
    public bool SupportsSequence { get; }
    public int MaxIdentifierLength { get; }
    public int MaxColumnsPerTable { get; }
    public int MaxIndexesPerTable { get; }
    public bool SupportsOnlineSchemaChange { get; }
    public bool SupportsTransactionalDdl { get; }
    public IReadOnlyList<string> SupportedFieldTypes { get; }

    public static DatabaseCapabilityMatrix ForDbType(string dbType)
    {
        return dbType switch
        {
            "Sqlite" => Sqlite,
            "SqlServer" => SqlServer,
            "MySql" => MySql,
            "PostgreSql" => PostgreSql,
            _ => Sqlite
        };
    }
}
