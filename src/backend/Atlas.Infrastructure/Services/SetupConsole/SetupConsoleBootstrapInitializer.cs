using Atlas.Application.Options;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.SetupConsole;

/// <summary>
/// AppHost 启动钩子（M8/A3）：把 appsettings 中的 BootstrapAdmin 明文密码哈希后落库，
/// 后续 SetupRecoveryKeyService 二次认证时用 PBKDF2 比对，不再明文比对。
///
/// - Setup 完成（已有 atlas.db）后才会执行；否则跳过等下次重启。
/// - 哈希结果与现有 hash 不一致时（说明配置已轮换密码），自动覆盖；一致时跳过。
/// </summary>
public sealed class SetupConsoleBootstrapInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BootstrapAdminOptions> _bootstrapAdmin;
    private readonly ILogger<SetupConsoleBootstrapInitializer> _logger;

    public SetupConsoleBootstrapInitializer(
        IServiceScopeFactory scopeFactory,
        IOptions<BootstrapAdminOptions> bootstrapAdmin,
        ILogger<SetupConsoleBootstrapInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _bootstrapAdmin = bootstrapAdmin;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _bootstrapAdmin.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Password))
        {
            return;
        }

        if (!Guid.TryParse(options.TenantId, out var tenantGuid))
        {
            _logger.LogWarning(
                "[SetupConsole] BootstrapAdmin TenantId 配置无效 ({TenantId})，跳过 bootstrap 密码哈希落库",
                options.TenantId);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetService<SetupRecoveryKeyService>();
            if (service is null)
            {
                _logger.LogDebug("[SetupConsole] SetupRecoveryKeyService not registered as concrete type; skip bootstrap hash");
                return;
            }

            // Hosted Service 启动期没有 HttpContext，TenantProvider/IdGenerator 取不到租户。
            // 显式以 BootstrapAdmin 配置的 TenantId 建立系统级 AppContext 作用域。
            var appContextAccessor = scope.ServiceProvider.GetRequiredService<IAppContextAccessor>();
            var systemContext = new AppContextSnapshot(
                new TenantId(tenantGuid),
                appContextAccessor.GetAppId(),
                CurrentUser: null,
                ClientContext: new ClientContext(
                    ClientType.Backend,
                    ClientPlatform.Web,
                    ClientChannel.App,
                    ClientAgent.Other),
                TraceId: null);
            using var appContextScope = appContextAccessor.BeginScope(systemContext);

            await service.EnsureBootstrapPasswordHashAsync(options.Password, force: false, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation("[SetupConsole] BootstrapAdmin password hash ensured");
        }
        catch (Exception ex)
        {
            // 不阻塞启动；首装阶段 setup_system_state 表可能尚未建好，下次重启会再试。
            _logger.LogWarning(ex, "[SetupConsole] failed to ensure bootstrap password hash; will retry next startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
