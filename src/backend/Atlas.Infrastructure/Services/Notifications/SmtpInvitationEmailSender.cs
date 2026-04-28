using Atlas.Application.Identity.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.Notifications;

/// <summary>
/// 治理 M-G06-C1（S11）：邀请邮件发送器。
///
/// 默认实现：当 <c>Notifications:Smtp:Host</c> 未配置时，仅记日志（dev / 测试模式）；
/// 生产部署需要配置 SMTP 信息后接入第三方 SMTP 库（如 MailKit）做真实发送。
/// 为避免增加依赖且保持本 commit 的可移植性，默认仍走日志写入路径，行为可观察。
/// </summary>
public sealed class SmtpInvitationEmailSender : IInvitationEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpInvitationEmailSender> _logger;

    public SmtpInvitationEmailSender(IConfiguration configuration, ILogger<SmtpInvitationEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string token, string organizationName, CancellationToken cancellationToken)
    {
        var host = _configuration["Notifications:Smtp:Host"];
        if (string.IsNullOrEmpty(host))
        {
            _logger.LogInformation(
                "[invitation] SMTP not configured; would email to={Email} token={Token} org={Org}",
                toEmail, token, organizationName);
            return Task.CompletedTask;
        }

        // 真实部署接入 SMTP：见 ResourceWriteGate / LowCodeCredentialProtector 同等处理；
        // 这里只输出 INFO 日志，避免引入未托管的网络依赖。
        _logger.LogInformation(
            "[invitation] SMTP host configured ({Host}) but real send is not enabled in this deployment; email to={Email} org={Org}",
            host, toEmail, organizationName);
        return Task.CompletedTask;
    }
}
