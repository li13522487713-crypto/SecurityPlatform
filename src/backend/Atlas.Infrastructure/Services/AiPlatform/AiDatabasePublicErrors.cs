using Atlas.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>
/// AI 数据库：对用户 / 导入任务可见的错误文案与异常脱敏（详情仅打日志）。
/// </summary>
internal static class AiDatabasePublicErrors
{
    public const string UnexpectedRow = "该行处理失败，请检查数据格式。";
    public const string UnexpectedJob = "后台任务失败，请稍后重试或联系管理员。";
    public const string UnexpectedImport = "导入任务失败，请稍后重试。";
    public const string BulkPayloadInvalid = "批量任务数据格式无效，无法解析。";
    public const string InlineJobEmptyPayload = "批量任务数据为空。";
    public const string ImportTargetMissing = "数据库或导入任务不存在，无法继续处理。";

    public static string ForRow(Exception ex, ILogger logger, int index, long databaseId)
    {
        if (ex is BusinessException bex)
        {
            return bex.Message;
        }

        logger.LogWarning(ex, "AiDatabase 行级处理异常 index={Index} db={DatabaseId}", index, databaseId);
        return UnexpectedRow;
    }

    public static string ForJob(Exception ex, ILogger logger, long databaseId, long taskId)
    {
        logger.LogError(ex, "AiDatabase 后台任务异常 db={DatabaseId} task={TaskId}", databaseId, taskId);
        return UnexpectedJob;
    }

    public static string ForImport(Exception ex, ILogger logger, long databaseId, long taskId)
    {
        logger.LogError(ex, "AiDatabase 文件导入任务异常 db={DatabaseId} task={TaskId}", databaseId, taskId);
        return UnexpectedImport;
    }
}
