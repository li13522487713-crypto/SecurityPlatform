using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 应用变量定义（M01 落地，对应 docx §10.2.3 VariableSchema）。
/// 注意：page 级变量随 PageSchema JSON 内联存储，<see cref="AppVariable"/> 仅承载 app / system 两个作用域。
/// </summary>
public sealed class AppVariable : TenantEntity
{
#pragma warning disable CS8618
    public AppVariable()
        : base(TenantId.Empty)
    {
        Code = string.Empty;
        DisplayName = string.Empty;
        Scope = "app";
        ValueType = "string";
        DefaultValueJson = "null";
    }
#pragma warning restore CS8618

    public AppVariable(
        TenantId tenantId,
        long id,
        long appId,
        string code,
        string displayName,
        string scope,
        string valueType)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        Code = code;
        DisplayName = displayName;
        Scope = scope;
        ValueType = valueType;
        DefaultValueJson = "null";
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long AppId { get; private set; }

    /// <summary>变量编码（应用内唯一），表达式中以 <c>app.code</c> / <c>system.code</c> 引用。</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string Code { get; private set; }

    [SugarColumn(Length = 200, IsNullable = false)]
    public string DisplayName { get; private set; }

    /// <summary>作用域：app / system（page 级变量内联在 PageSchema 中）。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Scope { get; private set; }

    /// <summary>值类型（9 类）：string / number / boolean / date / array / object / file / image / any（docx §10.2.3）。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string ValueType { get; private set; }

    /// <summary>是否只读（system 作用域强制 true）。</summary>
    public bool IsReadOnly { get; private set; }

    /// <summary>是否持久化（跨会话保留，true 时落 KV 存储）。</summary>
    public bool IsPersisted { get; private set; }

    /// <summary>默认值 JSON。</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string DefaultValueJson { get; private set; }

    /// <summary>校验规则 JSON（FluentValidation 序列化或 zod 等价表达），可空。</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ValidationJson { get; private set; }

    /// <summary>描述 / 备注。</summary>
    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? Description { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string displayName, string valueType, bool isReadOnly, bool isPersisted, string defaultValueJson, string? validationJson, string? description)
    {
        DisplayName = displayName;
        ValueType = valueType;
        IsReadOnly = Scope == "system" || isReadOnly;
        IsPersisted = isPersisted;
        DefaultValueJson = string.IsNullOrWhiteSpace(defaultValueJson) ? "null" : defaultValueJson;
        ValidationJson = validationJson;
        Description = description;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
