namespace Atlas.Application.AiPlatform.Models;

public enum KnowledgeTableColumnDataType
{
    String = 0,
    Number = 1,
    Boolean = 2,
    Date = 3
}

public sealed record KnowledgeTableColumnDto(
    long Id,
    long KnowledgeBaseId,
    long DocumentId,
    int Ordinal,
    string Name,
    bool IsIndexColumn,
    KnowledgeTableColumnDataType DataType);

public sealed record KnowledgeTableRowDto(
    long Id,
    long KnowledgeBaseId,
    long DocumentId,
    int RowIndex,
    string CellsJson,
    long? ChunkId = null);
