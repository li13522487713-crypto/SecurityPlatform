using System.Text.Json;
using Atlas.Core.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Setup;

/// <summary>
/// 基于文件 (setup-state.json) 的安装状态提供器。
/// 不依赖数据库，进程启动即可工作。
/// 线程安全：所有状态变更通过锁序列化；WaitForReadyAsync 通过信号量实现。
/// </summary>
public sealed class FileBasedSetupStateProvider : ISetupStateProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;
    private readonly ILogger<FileBasedSetupStateProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly TaskCompletionSource _readySignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private SetupStateInfo _state;

    public FileBasedSetupStateProvider(IHostEnvironment environment, IConfiguration configuration, ILogger<FileBasedSetupStateProvider> logger)
    {
        _logger = logger;
        var customPath = configuration["Setup:StateFilePath"];
        _filePath = string.IsNullOrWhiteSpace(customPath)
            ? Path.Combine(environment.ContentRootPath, "setup-state.json")
            : customPath;
        _state = LoadFromFile();

        if (_state.Status == SetupState.Ready)
        {
            _readySignal.TrySetResult();
            _logger.LogInformation("[Setup] 检测到 setup-state.json 状态为 Ready，直接进入正常模式");
        }
        else
        {
            _logger.LogWarning("[Setup] 当前安装状态: {Status}，系统处于 Setup Mode", _state.Status);
        }
    }

    public bool IsReady => _state.Status == SetupState.Ready;

    public bool IsSetupInProgress => _state.Status is SetupState.Configuring or SetupState.Migrating or SetupState.Seeding;

    public SetupStateInfo GetState() => _state;

    public async Task TransitionAsync(SetupState target, string? failureMessage = null, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _state.Status = target;

            if (target == SetupState.Failed)
            {
                _state.FailedAt = DateTimeOffset.UtcNow;
                _state.FailureMessage = failureMessage;
            }

            if (target == SetupState.Ready)
            {
                _state.CompletedAt = DateTimeOffset.UtcNow;
                _state.PlatformSetupCompleted = true;
                _state.FailureMessage = null;
                _state.FailedAt = null;
            }

            await SaveToFileAsync(cancellationToken);

            _logger.LogInformation("[Setup] 状态转换到: {Status}", target);

            if (target == SetupState.Ready)
            {
                _readySignal.TrySetResult();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task CompleteSetupAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _state.Status = SetupState.Ready;
            _state.CompletedAt = DateTimeOffset.UtcNow;
            _state.PlatformSetupCompleted = true;
            _state.FailureMessage = null;
            _state.FailedAt = null;

            await SaveToFileAsync(cancellationToken);

            _logger.LogInformation("[Setup] 安装完成，状态转为 Ready");

            _readySignal.TrySetResult();
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task WaitForReadyAsync(CancellationToken cancellationToken)
    {
        if (IsReady)
        {
            return Task.CompletedTask;
        }

        return WaitCoreAsync(cancellationToken);
    }

    private async Task WaitCoreAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => _readySignal.TrySetCanceled(cancellationToken));
        await _readySignal.Task;
    }

    private SetupStateInfo LoadFromFile()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new SetupStateInfo();
            }

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new SetupStateInfo();
            }

            return JsonSerializer.Deserialize<SetupStateInfo>(json, JsonOptions) ?? new SetupStateInfo();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Setup] 读取 setup-state.json 失败，视为 NotConfigured");
            return new SetupStateInfo();
        }
    }

    private async Task SaveToFileAsync(CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(_state, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }
}
