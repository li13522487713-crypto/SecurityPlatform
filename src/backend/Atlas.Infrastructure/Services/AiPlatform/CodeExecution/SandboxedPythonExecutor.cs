using System.Text.RegularExpressions;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform.CodeExecution;

/// <summary>
/// 沙箱编排层，根据 <see cref="CodeExecutionOptions.Mode"/> 路由到对应执行器：
/// <list type="bullet">
///   <item><term>Direct</term><description>直接调用宿主机 Python3（默认，恢复旧行为）</description></item>
///   <item><term>Docker</term><description>严格 Docker 容器隔离；Docker 不可用时拒绝执行</description></item>
///   <item><term>Sandbox</term><description>优先 Docker，Docker 不可用时回落宿主机 Python3</description></item>
/// </list>
/// </summary>
public sealed class SandboxedPythonExecutor : ICodeExecutionService
{
    // 检测标准 import / from...import 语句
    private static readonly Regex ImportRegex = new(
        @"(^|\n)\s*(?:from\s+([a-zA-Z0-9_\.]+)\s+import|import\s+([a-zA-Z0-9_\.]+))",
        RegexOptions.Compiled);

    // 检测动态导入与危险内置：__import__、importlib、eval、exec
    private static readonly Regex DangerousCallRegex = new(
        @"__import__\s*\(|importlib\s*\.|eval\s*\(|exec\s*\(",
        RegexOptions.Compiled);

    private readonly DockerPythonExecutor _dockerExecutor;
    private readonly DirectPythonExecutor _directExecutor;
    private readonly CodeExecutionOptions _options;
    private readonly ILogger<SandboxedPythonExecutor> _logger;

    public SandboxedPythonExecutor(
        DockerPythonExecutor dockerExecutor,
        DirectPythonExecutor directExecutor,
        IOptions<CodeExecutionOptions> options,
        ILogger<SandboxedPythonExecutor> logger)
    {
        _dockerExecutor = dockerExecutor;
        _directExecutor = directExecutor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CodeExecutionResult> ExecuteAsync(CodeExecutionRequest request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new CodeExecutionResult(
                false,
                null,
                "代码执行功能未启用。请联系平台管理员开启。",
                false,
                0);
        }

        var blockedModule = FindBlockedModule(request.Code);
        if (blockedModule is not null)
        {
            return new CodeExecutionResult(
                false,
                null,
                $"检测到受限模块导入：{blockedModule}",
                false,
                0);
        }

        // 检测危险动态调用（绕过模块过滤的常见方式）
        if (ContainsDangerousCall(request.Code))
        {
            return new CodeExecutionResult(
                false,
                null,
                "检测到危险调用（__import__、importlib、eval 或 exec 等），禁止执行。",
                false,
                0);
        }

        var timeout = request.TimeoutSeconds > 0
            ? Math.Min(request.TimeoutSeconds, _options.TimeoutSeconds)
            : _options.TimeoutSeconds;
        var sandboxRequest = request with { TimeoutSeconds = timeout };

        // Direct 模式：直接使用宿主机 Python3，跳过 Docker 检测（恢复旧默认行为）
        if (string.Equals(_options.Mode, "Direct", StringComparison.OrdinalIgnoreCase))
        {
            return await _directExecutor.ExecuteAsync(sandboxRequest, cancellationToken);
        }

        // Docker 模式：严格 Docker Only，不可用时拒绝执行
        if (string.Equals(_options.Mode, "Docker", StringComparison.OrdinalIgnoreCase))
        {
            var dockerAvailable = _options.Docker.AutoDetect
                ? await DockerPythonExecutor.IsDockerAvailableAsync()
                : true;

            if (!dockerAvailable)
            {
                _logger.LogError("Docker 沙箱不可用，且当前模式为 Docker Only，拒绝执行代码。");
                return new CodeExecutionResult(
                    false,
                    null,
                    "代码执行沙箱（Docker）不可用，请联系平台管理员。",
                    false,
                    0);
            }

            return await _dockerExecutor.ExecuteAsync(sandboxRequest, cancellationToken);
        }

        // Sandbox 模式：优先 Docker，不可用时回落到宿主机 Python3
        if (_options.Docker.AutoDetect)
        {
            var dockerAvailable = await DockerPythonExecutor.IsDockerAvailableAsync();
            if (dockerAvailable)
            {
                _logger.LogDebug("Docker 可用，使用容器沙箱执行代码。");
                return await _dockerExecutor.ExecuteAsync(sandboxRequest, cancellationToken);
            }

            _logger.LogWarning("Docker 不可用，回落到宿主机 Python3 执行（安全性较低）。");
        }

        return await _directExecutor.ExecuteAsync(sandboxRequest, cancellationToken);
    }

    private static bool ContainsDangerousCall(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }
        return DangerousCallRegex.IsMatch(code);
    }

    private string? FindBlockedModule(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var matches = ImportRegex.Matches(code);
        foreach (Match match in matches)
        {
            var module = match.Groups[2].Success
                ? match.Groups[2].Value
                : match.Groups[3].Value;
            if (string.IsNullOrWhiteSpace(module))
            {
                continue;
            }

            var root = module.Split('.', 2)[0];
            if (_options.BlockedModules.Any(x => string.Equals(x, root, StringComparison.OrdinalIgnoreCase)))
            {
                return root;
            }
        }

        return null;
    }
}
