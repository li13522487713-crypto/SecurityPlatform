using System.Text.RegularExpressions;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform.CodeExecution;

public sealed class SandboxedPythonExecutor : ICodeExecutionService
{
    private static readonly Regex ImportRegex = new(
        @"(^|\n)\s*(?:from\s+([a-zA-Z0-9_\.]+)\s+import|import\s+([a-zA-Z0-9_\.]+))",
        RegexOptions.Compiled);

    private readonly DirectPythonExecutor _innerExecutor;
    private readonly CodeExecutionOptions _options;

    public SandboxedPythonExecutor(
        DirectPythonExecutor innerExecutor,
        IOptions<CodeExecutionOptions> options)
    {
        _innerExecutor = innerExecutor;
        _options = options.Value;
    }

    public Task<CodeExecutionResult> ExecuteAsync(CodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var blockedModule = FindBlockedModule(request.Code);
        if (blockedModule is not null)
        {
            return Task.FromResult(new CodeExecutionResult(
                false,
                null,
                $"检测到受限模块导入：{blockedModule}",
                false,
                0));
        }

        var timeout = request.TimeoutSeconds > 0
            ? Math.Min(request.TimeoutSeconds, _options.TimeoutSeconds)
            : _options.TimeoutSeconds;
        var sandboxRequest = request with { TimeoutSeconds = timeout };
        return _innerExecutor.ExecuteAsync(sandboxRequest, cancellationToken);
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
