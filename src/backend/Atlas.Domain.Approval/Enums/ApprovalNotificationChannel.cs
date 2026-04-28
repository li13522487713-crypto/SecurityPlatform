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
    AppPush = 4,

    /// <summary>企业微信应用消息（v4 报告 27-31 章 External Connector 接入）</summary>
    WeCom = 5,

    /// <summary>飞书消息（v4 报告 27-31 章 External Connector 接入）</summary>
    Feishu = 6,

    /// <summary>钉钉工作通知（v4 报告 27-31 章 External Connector 接入）</summary>
    DingTalk = 7,
}
