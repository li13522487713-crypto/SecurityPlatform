namespace Atlas.Application.AiPlatform.Models;

/// <summary>
/// 知识库形态枚举（v5 §32-44 复刻）。
/// 字符串值与前端 <c>KnowledgeBaseKind</c> 保持一致，方便后续 OpenAPI 透传。
/// </summary>
public enum KnowledgeBaseKind
{
    Text = 0,
    Table = 1,
    Image = 2
}

/// <summary>
/// 知识库底层 RAG / 向量提供方（v5 §32-44 计划字面命名）。
/// </summary>
public enum KnowledgeBaseProvider
{
    Builtin = 0,
    Qdrant = 1,
    External = 2
}

/// <summary>
/// 兼容别名：旧代码使用 <c>KnowledgeBaseProviderKind</c>；新代码请使用 <see cref="KnowledgeBaseProvider"/>。
/// </summary>
[Obsolete("Renamed to KnowledgeBaseProvider; will be removed in next major version.")]
public enum KnowledgeBaseProviderKind
{
    Builtin = 0,
    Qdrant = 1,
    External = 2
}

/// <summary>解析模式：Quick=轻量文本；Precise=精准（含图表 OCR）。</summary>
public enum ParsingType
{
    Quick = 0,
    Precise = 1
}

/// <summary>图片知识库的 Caption 来源。</summary>
public enum ImageCaptionType
{
    AutoVlm = 0,
    Manual = 1,
    Filename = 2
}

/// <summary>切片模式：与 v5 报告 §36/§37 对齐。</summary>
public enum ChunkingProfileMode
{
    Fixed = 0,
    Semantic = 1,
    TableRow = 2,
    ImageItem = 3
}

/// <summary>
/// 解析策略对象（v5 §35 issue #847）。所有字段都可选，未配置时由后端按 KB 类型推断默认值。
/// </summary>
public sealed record ParsingStrategy(
    ParsingType ParsingType = ParsingType.Quick,
    bool ExtractImage = false,
    bool ExtractTable = false,
    bool ImageOcr = false,
    string? FilterPages = null,
    string? SheetId = null,
    int? HeaderLine = null,
    int? DataStartLine = null,
    int? RowsCount = null,
    ImageCaptionType? CaptionType = null);

/// <summary>切片策略对象。</summary>
public sealed record ChunkingProfile(
    ChunkingProfileMode Mode = ChunkingProfileMode.Fixed,
    int Size = 512,
    int Overlap = 64,
    IReadOnlyList<string>? Separators = null,
    IReadOnlyList<string>? IndexColumns = null);

/// <summary>检索策略对象，包含 TopK/min_score/重排/混合检索/查询改写。</summary>
public sealed record RetrievalProfile(
    int TopK = 5,
    float MinScore = 0f,
    bool EnableRerank = false,
    string? RerankModel = null,
    bool EnableHybrid = true,
    RetrievalWeights? Weights = null,
    bool EnableQueryRewrite = false);

/// <summary>混合检索权重，对应 v5 报告 §38 候选源（vector / bm25 / table / image）。</summary>
public sealed record RetrievalWeights(
    float Vector = 0.6f,
    float Bm25 = 0.4f,
    float Table = 0f,
    float Image = 0f);
