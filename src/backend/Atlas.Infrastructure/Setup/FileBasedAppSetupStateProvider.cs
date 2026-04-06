using System.Text.Json;
using Atlas.Core.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Setup;

/// <summary>
/// 应用级安装状态提供器（基于 app-setup-state.json 文件）。
/// </summary>
public sealed class FileBasedAppSetupStateProvider : IAppSetupStateProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;
    private readonly ILogger<FileBasedAppSetupStateProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private AppSetupStateInfo _state;

    public FileBasedAppSetupStateProvider(
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<FileBasedAppSetupStateProvider> logger)
    {
        _logger = logger;
        var customPath = configuration["AppSetup:StateFilePath"];
        _filePath = string.IsNullOrWhiteSpace(customPath)
            ? Path.Combine(environment.ContentRootPath, "app-setup-state.json")
            : customPath;
        _state = LoadFromFile();

        if (_state.Status == AppSetupState.Ready)
        {
            _logger.LogInformation("[AppSetup] 检测到 app-setup-state.json 状态为 Ready");
        }
        else
        {
            _logger.LogWarning("[AppSetup] 当前应用安装状态: {Status}", _state.Status);
        }
    }

    public bool IsReady => GetState().Status == AppSetupState.Ready;

    public AppSetupStateInfo GetState()
    {
        _state = LoadFromFile();
        return _state;
    }

    public async Task TransitionAsync(AppSetupState target, string? failureMessage = null, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _state.Status = target;
            if (target == AppSetupState.Failed)
            {
                _state.FailedAt = DateTimeOffset.UtcNow;
                _state.FailureMessage = failureMessage;
            }
            if (target == AppSetupState.Ready)
            {
                _state.CompletedAt = DateTimeOffset.UtcNow;
                _state.FailureMessage = null;
                _state.FailedAt = null;
            }
            await SaveToFileAsync(cancellationToken);
            _logger.LogInformation("[AppSetup] 状态转换到: {Status}", target);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task CompleteSetupAsync(string appName, string adminUsername, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _state.Status = AppSetupState.Ready;
            _state.CompletedAt = DateTimeOffset.UtcNow;
            _state.AppName = appName;
            _state.AdminUsername = adminUsername;
            _state.FailureMessage = null;
            _state.FailedAt = null;
            await SaveToFileAsync(cancellationToken);
            _logger.LogInformation("[AppSetup] 应用安装完成，AppName={AppName}", appName);
        }
        finally
        {
            _lock.Release();
        }
    }

    private AppSetupStateInfo LoadFromFile()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new AppSetupStateInfo();
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
                return new AppSetupStateInfo();
            return JsonSerializer.Deserialize<AppSetupStateInfo>(json, JsonOptions) ?? new AppSetupStateInfo();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AppSetup] 读取 app-setup-state.json 失败");
            return new AppSetupStateInfo();
        }
    }

    private async Task SaveToFileAsync(CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(_state, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }
}
