using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform.CodeExecution;

/// <summary>
/// 通过 Docker restricted 容器执行 Python 代码，隔离宿主机资源。
/// 容器参数：--network none --read-only --memory {limit} --cpus {quota}，禁止出网和写磁盘。
/// </summary>
public sealed class DockerPythonExecutor : ICodeExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CodeExecutionOptions _options;
    private readonly ILogger<DockerPythonExecutor> _logger;

    public DockerPythonExecutor(IOptions<CodeExecutionOptions> options, ILogger<DockerPythonExecutor> logger)
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

        var timeoutSeconds = request.TimeoutSeconds > 0
            ? Math.Min(request.TimeoutSeconds, _options.TimeoutSeconds)
            : _options.TimeoutSeconds;

        var variablesJson = JsonSerializer.Serialize(request.Variables, JsonOptions);
        var codeB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(code));
        var varsB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(variablesJson));

        var docker = _options.Docker;
        var dockerArgs = BuildDockerArgs(docker, codeB64, varsB64, timeoutSeconds);

        try
        {
            var result = await RunDockerAsync(dockerArgs, timeoutSeconds, cancellationToken);
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
            _logger.LogWarning(ex, "Docker 沙箱执行失败");
            return new CodeExecutionResult(false, null, $"沙箱执行失败：{ex.Message}", false, watch.ElapsedMilliseconds);
        }
    }

    private static string BuildDockerArgs(DockerSandboxOptions docker, string codeB64, string varsB64, int timeoutSeconds)
    {
        var script = BuildPythonScript();
        var scriptB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(script));

        return string.Join(" ",
            "run --rm",
            "--network none",
            "--read-only",
            $"--memory {docker.MemoryLimit}",
            $"--cpus {docker.CpuQuota.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            "--cap-drop ALL",
            "--security-opt no-new-privileges",
            $"-e ATLAS_CODE_B64={codeB64}",
            $"-e ATLAS_VARS_B64={varsB64}",
            $"-e ATLAS_SCRIPT_B64={scriptB64}",
            docker.Image,
            $"sh -c \"echo $ATLAS_SCRIPT_B64 | base64 -d | python3 -\"");
    }

    private async Task<CodeExecutionResult> RunDockerAsync(
        string dockerArgs,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = dockerArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds + 5));
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

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var errorMsg = string.IsNullOrWhiteSpace(stderr) ? "Docker 容器执行失败" : stderr;
            if (errorMsg.Contains("Cannot connect to the Docker daemon") || errorMsg.Contains("docker: not found"))
            {
                throw new InvalidOperationException("Docker 服务不可用，无法启动沙箱容器。");
            }

            return new CodeExecutionResult(false, null, TrimOutput(errorMsg), false, 0);
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

    private static string BuildPythonScript()
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

    private string TrimOutput(string output)
    {
        return output.Length <= _options.MaxOutputLength
            ? output
            : output[.._options.MaxOutputLength];
    }

    /// <summary>检测当前环境中 Docker 是否可用。</summary>
    public static async Task<bool> IsDockerAvailableAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "info",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private sealed record PythonExecutionOutput(bool Success, object? Output, string? Error);
}
