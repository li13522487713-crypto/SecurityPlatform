namespace Atlas.Infrastructure.BatchProcess.Options;

public sealed class BatchProcessRuntimeOptions
{
    public const string SectionName = "BatchProcess";

    /// <summary>
    /// 批处理执行链路仍处于 experimental，默认关闭主动触发。
    /// </summary>
    public bool EnableExecution { get; set; } = false;
}
