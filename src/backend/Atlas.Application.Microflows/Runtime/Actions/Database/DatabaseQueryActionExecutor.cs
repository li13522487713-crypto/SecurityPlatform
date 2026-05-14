using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Actions.Database;

/// <summary>
/// 执行 queryExternalDatabase 动作：参数化 SQL → 外部数据库 → 按输出类型映射 → 写回变量。
/// </summary>
public sealed class DatabaseQueryActionExecutor : IMicroflowActionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMicroflowDatabaseQueryService _queryService;

    public DatabaseQueryActionExecutor(IMicroflowDatabaseQueryService queryService)
    {
        _queryService = queryService;
    }

    public string ActionKind => "queryExternalDatabase";

    public string Category => MicroflowActionRuntimeCategory.ServerExecutable;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public async Task<MicroflowActionExecutionResult> ExecuteAsync(
        MicroflowActionExecutionContext context,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        // --- 读取配置 ---
        var config = context.ActionConfig;
        var sourceId = ReadString(config, "databaseSourceId");
        var driverCode = ReadString(config, "driverCode") ?? "SQLite";
        var sql = ReadString(config, "sql");

        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return Failed(stopwatch, RuntimeErrorCode.RuntimeValidationBlocked, "数据库节点缺少 databaseSourceId 配置。");
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            return Failed(stopwatch, RuntimeErrorCode.RuntimeValidationBlocked, "数据库节点缺少 SQL 语句。");
        }

        var outputConfig = ReadObject(config, "output");
        var outputVariableName = ReadString(outputConfig, "variableName");
        var outputKind = ReadString(outputConfig, "kind") ?? "list";
        var arrayColumn = ReadString(outputConfig, "column");

        var globalTarget = ReadString(ReadObject(config, "globalAssignment"), "target");

        var advancedConfig = ReadObject(config, "advanced");
        var timeoutSeconds = ReadInt(advancedConfig, "timeoutSeconds", 30);
        var errorMode = ReadString(advancedConfig, "errorMode") ?? "fail";
        var maxRows = ReadInt(advancedConfig, "maxRows", 1000);

        var tenantId = context.RuntimeSecurityContext.TenantId;

        // --- 占位符解析 ---
        string parameterizedSql;
        IReadOnlyList<MicroflowDatabaseSqlParameter> parameters;
        try
        {
            (parameterizedSql, parameters) = PlaceholderResolver.Resolve(sql!, driverCode, context);
        }
        catch (Exception ex)
        {
            return HandleError(stopwatch, errorMode, $"SQL 变量替换失败: {ex.Message}");
        }

        // --- 执行 SQL ---
        MicroflowDatabaseQueryResult result;
        try
        {
            result = await _queryService.ExecuteAsync(new MicroflowDatabaseQueryRequest
            {
                SourceId = sourceId!,
                Sql = parameterizedSql,
                Parameters = parameters,
                TenantId = tenantId,
                TimeoutSeconds = timeoutSeconds,
                MaxRows = maxRows,
                Mode = MicroflowDatabaseQueryMode.Auto
            }, ct);
        }
        catch (Exception ex)
        {
            return HandleError(stopwatch, errorMode, $"数据库执行异常: {ex.Message}");
        }

        if (!result.Success)
        {
            return HandleError(stopwatch, errorMode, result.ErrorMessage ?? "数据库执行失败。");
        }

        // --- 映射输出 ---
        object? outputValue;
        try
        {
            outputValue = MapOutput(result, outputKind, arrayColumn);
        }
        catch (Exception ex)
        {
            return HandleError(stopwatch, errorMode, $"输出映射失败: {ex.Message}");
        }

        var outputJson = JsonSerializer.SerializeToElement(outputValue, JsonOptions);
        var rawValueJson = JsonSerializer.Serialize(outputValue, JsonOptions);

        // --- 写回局部变量 ---
        if (!string.IsNullOrWhiteSpace(outputVariableName))
        {
            context.VariableStore.Define(new MicroflowVariableDefinition
            {
                Name = outputVariableName!,
                DataTypeJson = InferDataTypeJson(outputKind),
                RawValueJson = rawValueJson,
                ValuePreview = MicroflowVariableStore.Preview(rawValueJson),
                SourceKind = MicroflowVariableSourceKind.ActionOutput,
                SourceObjectId = context.ObjectId,
                SourceActionId = context.ActionId,
                ScopeKind = MicroflowVariableScopeKind.Action,
                AllowRedeclare = true
            });
        }

        // --- 写回全局变量 ---
        if (!string.IsNullOrWhiteSpace(globalTarget))
        {
            if (context.VariableStore.TryGet(globalTarget!, out var existing) && existing is not null)
            {
                context.VariableStore.Set(
                    globalTarget!,
                    existing with
                    {
                        RawValueJson = rawValueJson,
                        ValuePreview = MicroflowVariableStore.Preview(rawValueJson),
                        SourceKind = MicroflowVariableSourceKind.ActionOutput,
                        SourceObjectId = context.ObjectId,
                        SourceActionId = context.ActionId
                    });
            }
            else
            {
                context.VariableStore.Define(new MicroflowVariableDefinition
                {
                    Name = globalTarget!,
                    DataTypeJson = InferDataTypeJson(outputKind),
                    RawValueJson = rawValueJson,
                    ValuePreview = MicroflowVariableStore.Preview(rawValueJson),
                    SourceKind = MicroflowVariableSourceKind.ActionOutput,
                    SourceObjectId = context.ObjectId,
                    SourceActionId = context.ActionId,
                    ScopeKind = MicroflowVariableScopeKind.Global,
                    AllowRedeclare = true
                });
            }
        }

        stopwatch.Stop();
        var producedVariables = new List<MicroflowRuntimeVariableValueDto>();
        if (!string.IsNullOrWhiteSpace(outputVariableName))
        {
            producedVariables.Add(new MicroflowRuntimeVariableValueDto
            {
                Name = outputVariableName!,
                Type = MicroflowVariableStore.ToJsonElement(InferDataTypeJson(outputKind)),
                RawValue = outputJson,
                RawValueJson = rawValueJson,
                ValuePreview = MicroflowVariableStore.Preview(rawValueJson),
                Source = MicroflowVariableSourceKind.ActionOutput,
                ScopeKind = MicroflowVariableScopeKind.Action
            });
        }

        if (!string.IsNullOrWhiteSpace(globalTarget))
        {
            producedVariables.Add(new MicroflowRuntimeVariableValueDto
            {
                Name = globalTarget!,
                Type = MicroflowVariableStore.ToJsonElement(InferDataTypeJson(outputKind)),
                RawValue = outputJson,
                RawValueJson = rawValueJson,
                ValuePreview = MicroflowVariableStore.Preview(rawValueJson),
                Source = MicroflowVariableSourceKind.ActionOutput,
                ScopeKind = MicroflowVariableScopeKind.Global
            });
        }

        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = outputJson,
            OutputPreview = $"rows={result.Rows.Count}, elapsed={result.ElapsedMs}ms",
            ProducedVariables = producedVariables,
            DurationMs = (int)stopwatch.ElapsedMilliseconds
        };
    }

    private static object? MapOutput(MicroflowDatabaseQueryResult result, string kind, string? column)
    {
        switch (kind.ToLowerInvariant())
        {
            case "object":
                return result.Rows.Count > 0 ? (object)result.Rows[0] : null;

            case "array":
                if (string.IsNullOrWhiteSpace(column))
                {
                    throw new InvalidOperationException("Array 输出类型需要指定 column 列名。");
                }

                return result.Rows.Select(row =>
                    row.TryGetValue(column!, out var val) ? val : null).ToList();

            default: // "list"
                return result.Rows.ToList();
        }
    }

    private static string InferDataTypeJson(string kind)
        => kind.ToLowerInvariant() switch
        {
            "object" => """{"kind":"object"}""",
            "array" => """{"kind":"list","itemType":{"kind":"primitive","primitiveType":"String"}}""",
            _ => """{"kind":"list","itemType":{"kind":"object"}}"""
        };

    private static MicroflowActionExecutionResult HandleError(
        Stopwatch stopwatch,
        string errorMode,
        string message)
    {
        stopwatch.Stop();
        return errorMode switch
        {
            "continue" => new MicroflowActionExecutionResult
            {
                Status = MicroflowActionExecutionStatus.Success,
                OutputJson = JsonSerializer.SerializeToElement<object?>(null, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                OutputPreview = $"error(continue): {message}",
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            },
            "empty" => new MicroflowActionExecutionResult
            {
                Status = MicroflowActionExecutionStatus.Success,
                OutputJson = JsonSerializer.SerializeToElement<object?>(null, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                OutputPreview = $"error(empty): {message}",
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            },
            _ => Failed(stopwatch, RuntimeErrorCode.RuntimeUnknownError, message)
        };
    }

    private static MicroflowActionExecutionResult Failed(Stopwatch stopwatch, string code, string message)
    {
        stopwatch.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Failed,
            Error = new MicroflowRuntimeErrorDto { Code = code, Message = message },
            Message = message,
            DurationMs = (int)stopwatch.ElapsedMilliseconds,
            ShouldContinueNormalFlow = false,
            ShouldEnterErrorHandler = true
        };
    }

    private static string? ReadString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var val)
            && val.ValueKind == JsonValueKind.String
                ? val.GetString()
                : null;

    private static JsonElement ReadObject(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var val)
            && val.ValueKind == JsonValueKind.Object
                ? val
                : default;

    private static int ReadInt(JsonElement element, string property, int fallback)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var val)
            && val.ValueKind == JsonValueKind.Number
            && val.TryGetInt32(out var n))
        {
            return n;
        }

        return fallback;
    }
}
