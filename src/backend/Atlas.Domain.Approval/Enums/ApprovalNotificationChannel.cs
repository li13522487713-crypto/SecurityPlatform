namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 消息通知渠道类型（对应 AntFlow 的 MsgNoticeTypeEnum）
/// </summary>
public enum ApprovalNotificationChannel
{
    /// <summary>站内信</summary>
    Inbox = 1,

    /// <summary>邮件</summary>
    Email = 2,

    /// <summary>短信</summary>
    Sms = 3,

    /// <summary>App推送</summary>
    AppPush = 4
}
