using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform.CodeExecution;

public class DirectPythonExecutor : ICodeExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CodeExecutionOptions _options;
    private readonly ILogger<DirectPythonExecutor> _logger;

    public DirectPythonExecutor(
        IOptions<CodeExecutionOptions> options,
        ILogger<DirectPythonExecutor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CodeExecutionResult> ExecuteAsync(CodeExecutionRequest request, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var code = request.Code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            return new CodeExecutionResult(true, null, null, false, 0);
        }

        if (LooksLikeExpression(code))
        {
            try
            {
                using var table = new DataTable();
                var value = table.Compute(code, string.Empty);
                watch.Stop();
                return new CodeExecutionResult(true, value, null, false, watch.ElapsedMilliseconds);
            }
            catch
            {
                // fallback to python
            }
        }

        var timeoutSeconds = request.TimeoutSeconds > 0
            ? request.TimeoutSeconds
            : Math.Max(1, _options.TimeoutSeconds);
        try
        {
            var result = await ExecutePythonAsync(code, request.Variables, timeoutSeconds, cancellationToken);
            watch.Stop();
            return result with { DurationMs = watch.ElapsedMilliseconds };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            watch.Stop();
            return new CodeExecutionResult(false, null, "代码执行超时。", true, watch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            watch.Stop();
            _logger.LogWarning(ex, "执行 Python 代码失败");
            return new CodeExecutionResult(false, null, ex.Message, false, watch.ElapsedMilliseconds);
        }
    }

    private async Task<CodeExecutionResult> ExecutePythonAsync(
        string code,
        IReadOnlyDictionary<string, object?> variables,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"atlas-code-{Guid.NewGuid():N}.py");
        await File.WriteAllTextAsync(scriptPath, BuildPythonWrapperScript(), Encoding.UTF8, cancellationToken);

        var variablesJson = JsonSerializer.Serialize(variables, JsonOptions);
        var startInfo = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = $"\"{scriptPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["ATLAS_CODE_B64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(code));
        startInfo.Environment["ATLAS_VARS_B64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(variablesJson));

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }
        finally
        {
            try
            {
                File.Delete(scriptPath);
            }
            catch
            {
                // ignore
            }
        }

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            return new CodeExecutionResult(false, null, string.IsNullOrWhiteSpace(stderr) ? "Python 执行失败" : stderr, false, 0);
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            return new CodeExecutionResult(true, null, null, false, 0);
        }

        try
        {
            var output = JsonSerializer.Deserialize<PythonExecutionOutput>(stdout.Trim(), JsonOptions);
            if (output is null)
            {
                return new CodeExecutionResult(true, TrimOutput(stdout), null, false, 0);
            }

            return output.Success
                ? new CodeExecutionResult(true, output.Output, null, false, 0)
                : new CodeExecutionResult(false, null, output.Error ?? "代码执行失败。", false, 0);
        }
        catch (JsonException)
        {
            return new CodeExecutionResult(true, TrimOutput(stdout), null, false, 0);
        }
    }

    private string BuildPythonWrapperScript()
    {
        return
            """
            import base64
            import json
            import os

            code = base64.b64decode(os.environ.get("ATLAS_CODE_B64", "")).decode("utf-8")
            vars_json = base64.b64decode(os.environ.get("ATLAS_VARS_B64", "")).decode("utf-8")
            variables = json.loads(vars_json) if vars_json else {}
            local_scope = dict(variables)

            try:
                try:
                    compiled = compile(code, "<atlas>", "eval")
                    result = eval(compiled, {}, local_scope)
                except SyntaxError:
                    compiled = compile(code, "<atlas>", "exec")
                    exec(compiled, {}, local_scope)
                    result = local_scope.get("result")
                print(json.dumps({"success": True, "output": result}, ensure_ascii=False, default=str))
            except Exception as ex:
                print(json.dumps({"success": False, "error": str(ex)}, ensure_ascii=False))
            """;
    }

    private static bool LooksLikeExpression(string code)
    {
        if (code.Contains('\n') || code.Contains('\r'))
        {
            return false;
        }

        return !code.Contains("import ", StringComparison.OrdinalIgnoreCase)
               && !code.Contains("def ", StringComparison.OrdinalIgnoreCase)
               && !code.Contains("class ", StringComparison.OrdinalIgnoreCase);
    }

    private string TrimOutput(string stdout)
    {
        if (stdout.Length <= _options.MaxOutputLength)
        {
            return stdout;
        }

        return stdout[.._options.MaxOutputLength];
    }

    private sealed record PythonExecutionOutput(bool Success, object? Output, string? Error);
}
