namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 服务任务类型
/// </summary>
public enum ServiceTaskType
{
    /// <summary>HTTP 请求</summary>
    HttpRequest = 0,

    /// <summary>SQL 查询</summary>
    SqlQuery = 1,

    /// <summary>脚本执行</summary>
    ScriptExecution = 2,

    /// <summary>消息发送</summary>
    MessageSend = 3,

    /// <summary>数据赋值</summary>
    DataAssignment = 4
}
