using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Audit;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Transactions;
using Atlas.Domain.Microflows.Entities;

namespace Atlas.Application.Microflows.Services;

public sealed class MicroflowTestRunService : IMicroflowTestRunService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int DefaultRunTimeoutSeconds = 300;

    private readonly IMicroflowResourceRepository _resourceRepository;
    private readonly IMicroflowSchemaSnapshotRepository _schemaSnapshotRepository;
    private readonly IMicroflowRunRepository _runRepository;
    private readonly IMicroflowStorageTransaction _storageTransaction;
    private readonly IMicroflowValidationService _validationService;
    private readonly IMicroflowMetadataService _metadataService;
    private readonly IMicroflowRuntimeEngine _runner;
    private readonly IMicroflowExecutionPlanLoader _executionPlanLoader;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;
    private readonly IMicroflowAuditWriter _auditWriter;
    private readonly IMicroflowClock _clock;
    private readonly IMicroflowRunCancellationRegistry _cancellationRegistry;
    private readonly IMicroflowRunOwnershipGuard _ownershipGuard;

    public MicroflowTestRunService(
        IMicroflowResourceRepository resourceRepository,
        IMicroflowSchemaSnapshotRepository schemaSnapshotRepository,
        IMicroflowRunRepository runRepository,
        IMicroflowStorageTransaction storageTransaction,
        IMicroflowValidationService validationService,
        IMicroflowMetadataService metadataService,
        IMicroflowRuntimeEngine runner,
        IMicroflowExecutionPlanLoader executionPlanLoader,
        IMicroflowRequestContextAccessor requestContextAccessor,
        IMicroflowAuditWriter auditWriter,
        IMicroflowClock clock,
        IMicroflowRunCancellationRegistry cancellationRegistry,
        IMicroflowRunOwnershipGuard ownershipGuard)
    {
        _resourceRepository = resourceRepository;
        _schemaSnapshotRepository = schemaSnapshotRepository;
        _runRepository = runRepository;
        _storageTransaction = storageTransaction;
        _validationService = validationService;
        _metadataService = metadataService;
        _runner = runner;
        _executionPlanLoader = executionPlanLoader;
        _requestContextAccessor = requestContextAccessor;
        _auditWriter = auditWriter;
        _clock = clock;
        _cancellationRegistry = cancellationRegistry;
        _ownershipGuard = ownershipGuard;
    }

    public async Task<TestRunMicroflowApiResponse> TestRunAsync(
        string resourceId,
        TestRunMicroflowApiRequest request,
        CancellationToken cancellationToken)
    {
        var resource = await _resourceRepository.GetByIdAsync(resourceId, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流资源不存在。", 404);
        var (schema, schemaId) = await ResolveSchemaAsync(resource, request.Schema, request.SchemaId, cancellationToken);
        var input = request.Inputs ?? request.Input ?? new Dictionary<string, JsonElement>();
        var options = request.Options ?? new MicroflowTestRunOptionsDto();

        var validation = await _validationService.ValidateAsync(
            resourceId,
            new ValidateMicroflowRequestDto
            {
                Schema = schema,
                Mode = "testRun",
                IncludeWarnings = true,
                IncludeInfo = true
            },
            cancellationToken);

        if (validation.Summary.ErrorCount > 0)
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowValidationFailed,
                "微流试运行被后端校验阻止。",
                422,
                validationIssues: validation.Issues);
        }

        var executionPlan = await TryPreloadExecutionPlanAsync(resource, schema, cancellationToken);

        var metadata = await _metadataService.GetCatalogAsync(
            new GetMicroflowMetadataRequestDto
            {
                WorkspaceId = resource.WorkspaceId ?? _requestContextAccessor.Current.WorkspaceId,
                IncludeSystem = true,
                IncludeArchived = true
            },
            cancellationToken);

        // P0-6: 注册 cancellation handle 与全局 RunTimeoutSeconds 联动；
        // 同一 traceId/runId 既写入 trace context 也作为 registry key，
        // 这样 CancelAsync(runId) 可以真正中断正在跑的引擎主循环。
        var runId = !string.IsNullOrWhiteSpace(_requestContextAccessor.Current.TraceId)
            ? _requestContextAccessor.Current.TraceId
            : Guid.NewGuid().ToString("N");
        var runTimeoutSeconds = ResolveRunTimeoutSeconds();
        using var registryCts = _cancellationRegistry.Register(runId, cancellationToken);
        if (runTimeoutSeconds > 0)
        {
            registryCts.CancelAfter(TimeSpan.FromSeconds(runTimeoutSeconds));
        }

        MicroflowRunSessionDto session;
        try
        {
            session = await _runner.RunAsync(
                new MicroflowExecutionRequest
                {
                    ResourceId = resource.Id,
                    SchemaId = schemaId,
                    Version = request.Version ?? resource.Version,
                    Schema = schema,
                    ExecutionPlan = executionPlan,
                    Input = input,
                    Options = options,
                    Metadata = metadata,
                    RequestContext = _requestContextAccessor.Current with { TraceId = runId },
                    CorrelationId = request.CorrelationId,
                    DebugSessionId = request.DebugSessionId,
                    MaxCallDepth = 10
                },
                registryCts.Token);
        }
        finally
        {
            _cancellationRegistry.Unregister(runId);
        }

        try
        {
            await _storageTransaction.ExecuteAsync(
                async () =>
                {
                    await PersistSessionGraphAsync(session, resource, cancellationToken);
                    await _resourceRepository.UpdateLastRunAsync(
                        resource.Id,
                        session.Status,
                        session.EndedAt ?? session.StartedAt,
                        cancellationToken);
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not MicroflowApiException)
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowStorageError,
                "微流试运行记录持久化失败。",
                500,
                details: ex.Message,
                innerException: ex);
        }

        await SafeAuditAsync(new MicroflowAuditEvent
        {
            Action = "microflow.test_run",
            Result = string.Equals(session.Status, "success", StringComparison.OrdinalIgnoreCase) ? "success" : "failure",
            ResourceId = resource.Id,
            ResourceName = resource.Name,
            WorkspaceId = resource.WorkspaceId,
            Target = $"{resource.ModuleId}/{resource.Name}#{session.Id}",
            ErrorCode = session.Error?.Code,
            Details = new Dictionary<string, object?>
            {
                ["runId"] = session.Id,
                ["status"] = session.Status,
                ["durationMs"] = (session.EndedAt is { } endedAt
                    ? (long)(endedAt - session.StartedAt).TotalMilliseconds
                    : 0)
            }
        }, cancellationToken);

        return ToApiResponse(session, _requestContextAccessor.Current.TraceId);
    }

    private async Task SafeAuditAsync(MicroflowAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        try
        {
            await _auditWriter.WriteAsync(auditEvent, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // best-effort
        }
    }

    private async Task PersistSessionGraphAsync(MicroflowRunSessionDto session, MicroflowResourceEntity fallbackResource, CancellationToken cancellationToken)
    {
        var resource = await _resourceRepository.GetByIdAsync(session.ResourceId, cancellationToken) ?? fallbackResource;
        var entities = ToEntities(session, resource, _requestContextAccessor.Current);
        await _runRepository.InsertSessionAsync(entities.Session, cancellationToken);
        await _runRepository.InsertTraceFramesAsync(entities.TraceFrames, cancellationToken);
        await _runRepository.InsertLogsAsync(entities.Logs, cancellationToken);

        foreach (var child in session.ChildRuns)
        {
            await PersistSessionGraphAsync(child, resource, cancellationToken);
        }
    }

    public async Task<CancelMicroflowRunResponse> CancelAsync(string runId, CancellationToken cancellationToken)
    {
        var session = await _ownershipGuard.EnsureRunOwnedAsync(runId, cancellationToken);

        // P0-6: 真正中断正在执行的引擎主循环。即使 cancellation registry 没记录
        // (例如 run 已经返回还没写库时的极少数竞态)，也仍然落 DB 状态以保证最终一致。
        _cancellationRegistry.Cancel(runId);

        if (!string.Equals(session.Status, "cancelled", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(session.Status, "success", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(session.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            session.Status = "cancelled";
            session.EndedAt = _clock.UtcNow;
            await _runRepository.UpdateSessionStatusAsync(runId, session.Status, session.EndedAt, cancellationToken);
        }

        return new CancelMicroflowRunResponse
        {
            RunId = runId,
            Status = session.Status == "cancelled" ? "cancelled" : session.Status
        };
    }

    public async Task<MicroflowRunSessionDto> GetRunSessionAsync(string runId, CancellationToken cancellationToken)
    {
        _ = await _ownershipGuard.EnsureRunOwnedAsync(runId, cancellationToken);
        return await BuildRunSessionGraphAsync(runId, cancellationToken);
    }

    public async Task<MicroflowRunSessionDto> GetRunSessionAsync(
        string resourceId,
        string runId,
        CancellationToken cancellationToken)
    {
        _ = await _ownershipGuard.EnsureRunOwnedAsync(runId, cancellationToken);
        var session = await BuildRunSessionGraphAsync(runId, cancellationToken);
        if (!string.Equals(session.ResourceId, resourceId, StringComparison.Ordinal))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流运行会话不存在。", 404);
        }

        return session;
    }

    public async Task<ListMicroflowRunsResponse> ListRunsAsync(
        string resourceId,
        ListMicroflowRunsRequest request,
        CancellationToken cancellationToken)
    {
        _ = await _resourceRepository.GetByIdAsync(resourceId, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流资源不存在。", 404);
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);
        var requestedStatus = NormalizeRunHistoryStatusFilter(request.Status);
        string[]? repositoryStatuses = requestedStatus switch
        {
            "unsupported" => new[] { "failed", "cancelled" },
            null => null,
            "all" => null,
            _ => new[] { requestedStatus }
        };

        var sessions = await _runRepository.ListSessionsByResourceIdAsync(
            resourceId,
            pageIndex,
            pageSize,
            repositoryStatuses,
            cancellationToken);
        var filtered = sessions
            .Select(session => ToRunHistoryItem(resourceId, session))
            .Where(item => requestedStatus is null or "all" || string.Equals(item.Status, requestedStatus, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var total = await _runRepository.CountSessionsByResourceIdAsync(resourceId, repositoryStatuses, cancellationToken);
        if (requestedStatus is "unsupported")
        {
            total = await CountRunsByStatusAsync(resourceId, "unsupported", cancellationToken);
        }

        return new ListMicroflowRunsResponse
        {
            Items = filtered,
            Total = total
        };
    }

    public async Task<GetMicroflowRunTraceResponse> GetRunTraceAsync(string runId, CancellationToken cancellationToken)
    {
        _ = await _ownershipGuard.EnsureRunOwnedAsync(runId, cancellationToken);
        var frames = await _runRepository.ListTraceFramesAsync(runId, cancellationToken);
        var logs = await _runRepository.ListLogsAsync(runId, cancellationToken);
        return new GetMicroflowRunTraceResponse
        {
            RunId = runId,
            Trace = frames.Select(ToFrameDto).ToArray(),
            Logs = logs.Select(ToLogDto).ToArray()
        };
    }

    private async Task<MicroflowExecutionPlan?> TryPreloadExecutionPlanAsync(MicroflowResourceEntity resource, JsonElement schema, CancellationToken cancellationToken)
    {
        try
        {
            return await _executionPlanLoader.LoadFromSchemaAsync(
                schema,
                new MicroflowExecutionPlanLoadOptions
                {
                    ResourceId = resource.Id,
                    Version = resource.Version,
                    Mode = MicroflowExecutionPlanMode.TestRun,
                    IncludeDiagnostics = true,
                    FailOnUnsupported = false,
                    WorkspaceId = resource.WorkspaceId,
                    TenantId = _requestContextAccessor.Current.TenantId,
                    UserId = _requestContextAccessor.Current.UserId
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // 第 48 轮只做只读 ExecutionPlan 预热，不能改变既有 TestRun 行为。
            return null;
        }
    }

    private async Task<(JsonElement Schema, string SchemaId)> ResolveSchemaAsync(
        MicroflowResourceEntity resource,
        JsonElement? draftSchema,
        string? requestedSchemaId,
        CancellationToken cancellationToken)
    {
        if (draftSchema.HasValue)
        {
            if (draftSchema.Value.ValueKind != JsonValueKind.Object)
            {
                throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowSchemaInvalid, "试运行 schema 必须是对象。", 400);
            }

            return (draftSchema.Value.Clone(), resource.CurrentSchemaSnapshotId ?? resource.SchemaId ?? string.Empty);
        }

        var snapshot = !string.IsNullOrWhiteSpace(requestedSchemaId)
            ? await _schemaSnapshotRepository.GetByIdAsync(requestedSchemaId!, cancellationToken)
            : null;
        snapshot ??= !string.IsNullOrWhiteSpace(resource.CurrentSchemaSnapshotId)
            ? await _schemaSnapshotRepository.GetByIdAsync(resource.CurrentSchemaSnapshotId, cancellationToken)
            : await _schemaSnapshotRepository.GetLatestByResourceIdAsync(resource.Id, cancellationToken);
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.SchemaJson))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowSchemaInvalid, "微流当前 Schema 不存在。", 400);
        }

        return (MicroflowSchemaJsonHelper.ParseRequired(snapshot.SchemaJson), snapshot.Id);
    }

    private static int ResolveRunTimeoutSeconds()
    {
        var configured = Environment.GetEnvironmentVariable("Microflow__Runtime__RunTimeoutSeconds")
            ?? Environment.GetEnvironmentVariable("MICROFLOW_RUNTIME_RUN_TIMEOUT_SECONDS");
        if (int.TryParse(configured, out var seconds))
        {
            return Math.Max(0, seconds);
        }

        return DefaultRunTimeoutSeconds;
    }

    private static TestRunMicroflowApiResponse ToApiResponse(MicroflowRunSessionDto session, string traceId)
    {
        var durationMs = session.EndedAt.HasValue
            ? Math.Max(0, (int)(session.EndedAt.Value - session.StartedAt).TotalMilliseconds)
            : 0;
        var errorCode = session.Error?.Code ?? session.Trace.FirstOrDefault(frame => frame.Error is not null)?.Error?.Code;
        var status = !string.IsNullOrWhiteSpace(errorCode) && errorCode.Contains("UNSUPPORTED", StringComparison.OrdinalIgnoreCase)
            ? "unsupported"
            : string.Equals(session.Status, "success", StringComparison.OrdinalIgnoreCase)
                ? "succeeded"
                : session.Status;
        return new TestRunMicroflowApiResponse
        {
            Session = session,
            RunId = session.Id,
            MicroflowId = session.ResourceId,
            Status = status,
            Result = session.Output,
            ErrorCode = errorCode,
            ErrorMessage = session.Error?.Message ?? session.Trace.FirstOrDefault(frame => frame.Error is not null)?.Error?.Message,
            DurationMs = durationMs,
            StartedAt = session.StartedAt,
            CompletedAt = session.EndedAt,
            TraceId = traceId,
            Logs = session.Logs,
            NodeResults = session.Trace,
            CallStack = session.CallStack
        };
    }

    private static (MicroflowRunSessionEntity Session, IReadOnlyList<MicroflowRunTraceFrameEntity> TraceFrames, IReadOnlyList<MicroflowRunLogEntity> Logs) ToEntities(
        MicroflowRunSessionDto session,
        MicroflowResourceEntity resource,
        MicroflowRequestContext context)
    {
        var entity = new MicroflowRunSessionEntity
        {
            Id = session.Id,
            ResourceId = session.ResourceId,
            WorkspaceId = resource.WorkspaceId,
            TenantId = resource.TenantId,
            SchemaSnapshotId = session.SchemaId,
            Status = session.Status,
            InputJson = JsonSerializer.Serialize(session.Input, JsonOptions),
            OutputJson = session.Output.HasValue ? session.Output.Value.GetRawText() : null,
            ErrorJson = session.Error is null ? null : JsonSerializer.Serialize(session.Error, JsonOptions),
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            CreatedBy = context.UserId,
            Mode = "testRun",
            TraceFrameCount = session.Trace.Count,
            LogCount = session.Logs.Count,
            ExtraJson = JsonSerializer.Serialize(new
            {
                session.Version,
                session.ParentRunId,
                session.RootRunId,
                session.CallFrameId,
                session.CallDepth,
                session.CorrelationId,
                session.CallStack,
                variables = session.Variables,
                transactionSummary = session.TransactionSummary,
                childRunIds = session.ChildRunIds.Count > 0 ? session.ChildRunIds : session.ChildRuns.Select(child => child.Id).ToArray()
            }, JsonOptions)
        };

        var frames = session.Trace.Select((frame, index) => new MicroflowRunTraceFrameEntity
        {
            Id = frame.Id,
            RunId = session.Id,
            WorkspaceId = resource.WorkspaceId,
            TenantId = resource.TenantId,
            Sequence = index + 1,
            ObjectId = frame.ObjectId,
            ActionId = frame.ActionId,
            CollectionId = frame.CollectionId,
            IncomingFlowId = frame.IncomingFlowId,
            OutgoingFlowId = frame.OutgoingFlowId,
            SelectedCaseValueJson = frame.SelectedCaseValue.HasValue ? frame.SelectedCaseValue.Value.GetRawText() : null,
            LoopIterationJson = frame.LoopIteration.HasValue ? frame.LoopIteration.Value.GetRawText() : null,
            Status = frame.Status,
            StartedAt = frame.StartedAt,
            EndedAt = frame.EndedAt,
            DurationMs = frame.DurationMs,
            InputJson = frame.Input.HasValue ? frame.Input.Value.GetRawText() : null,
            OutputJson = frame.Output.HasValue ? frame.Output.Value.GetRawText() : null,
            ErrorJson = frame.Error is null ? null : JsonSerializer.Serialize(frame.Error, JsonOptions),
            VariablesSnapshotJson = frame.VariablesSnapshot is null ? null : JsonSerializer.Serialize(frame.VariablesSnapshot, JsonOptions),
            Message = frame.Message,
            ExtraJson = JsonSerializer.Serialize(new TraceFrameExtra
            {
                ErrorHandlerVisited = frame.ErrorHandlerVisited,
                ParentRunId = frame.ParentRunId,
                RootRunId = frame.RootRunId,
                CallFrameId = frame.CallFrameId,
                CallDepth = frame.CallDepth,
                CallerObjectId = frame.CallerObjectId,
                CallerActionId = frame.CallerActionId,
                MicroflowId = frame.MicroflowId
            }, JsonOptions)
        }).ToArray();

        var logs = session.Logs.Select(log => new MicroflowRunLogEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            RunId = session.Id,
            WorkspaceId = resource.WorkspaceId,
            TenantId = resource.TenantId,
            Timestamp = log.Timestamp,
            Level = log.Level,
            ObjectId = log.ObjectId,
            ActionId = log.ActionId,
            Message = log.Message,
            ExtraJson = JsonSerializer.Serialize(new LogExtra
            {
                LogNodeName = log.LogNodeName,
                TraceId = log.TraceId,
                VariablesPreview = log.VariablesPreview,
                StructuredFieldsJson = log.StructuredFieldsJson
            }, JsonOptions)
        }).ToArray();

        return (entity, frames, logs);
    }

    private static MicroflowRunSessionDto ToDto(
        MicroflowRunSessionEntity session,
        IReadOnlyList<MicroflowRunTraceFrameEntity> frames,
        IReadOnlyList<MicroflowRunLogEntity> logs,
        IReadOnlyList<MicroflowRunSessionDto>? childRuns = null)
    {
        var extra = ReadSessionExtra(session.ExtraJson);
        var error = string.IsNullOrWhiteSpace(session.ErrorJson)
            ? null
            : Deserialize<MicroflowRuntimeErrorDto>(session.ErrorJson);
        return new MicroflowRunSessionDto
        {
            Id = session.Id,
            SchemaId = session.SchemaSnapshotId ?? string.Empty,
            ResourceId = session.ResourceId,
            Version = extra.Version,
            ParentRunId = extra.ParentRunId,
            RootRunId = extra.RootRunId,
            CallFrameId = extra.CallFrameId,
            CallDepth = extra.CallDepth,
            CorrelationId = extra.CorrelationId,
            CallStack = extra.CallStack,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            Status = session.Status,
            Input = Deserialize<Dictionary<string, JsonElement>>(session.InputJson) ?? new Dictionary<string, JsonElement>(),
            Output = ParseOptional(session.OutputJson),
            Error = error,
            Trace = frames.Select(ToFrameDto).ToArray(),
            Logs = logs.Select(ToLogDto).ToArray(),
            Variables = extra.Variables,
            TransactionSummary = extra.TransactionSummary,
            ChildRuns = childRuns ?? Array.Empty<MicroflowRunSessionDto>(),
            ChildRunIds = extra.ChildRunIds
        };
    }

    private async Task<MicroflowRunSessionDto> BuildRunSessionGraphAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        return await BuildRunSessionGraphCoreAsync(runId, visited, 0, cancellationToken);
    }

    private async Task<MicroflowRunSessionDto> BuildRunSessionGraphCoreAsync(
        string runId,
        HashSet<string> visited,
        int depth,
        CancellationToken cancellationToken)
    {
        if (!visited.Add(runId) || depth > 16)
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowStorageError, "微流运行调用栈深度异常。", 500);
        }

        var session = await _runRepository.GetSessionAsync(runId, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流运行会话不存在。", 404);
        var frames = await _runRepository.ListTraceFramesAsync(runId, cancellationToken);
        var logs = await _runRepository.ListLogsAsync(runId, cancellationToken);
        var sessionExtra = ReadSessionExtra(session.ExtraJson);
        var childRuns = new List<MicroflowRunSessionDto>();
        foreach (var childRunId in sessionExtra.ChildRunIds)
        {
            if (string.IsNullOrWhiteSpace(childRunId))
            {
                continue;
            }

            var child = await BuildRunSessionGraphCoreAsync(childRunId, visited, depth + 1, cancellationToken);
            childRuns.Add(child);
        }

        return ToDto(session, frames, logs, childRuns);
    }

    private static string? NormalizeRunHistoryStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var normalized = status.Trim().ToLowerInvariant();
        return normalized switch
        {
            "all" => "all",
            "succeeded" => "success",
            "success" => "success",
            "failed" => "failed",
            "unsupported" => "unsupported",
            "cancelled" => "cancelled",
            _ => null
        };
    }

    private async Task<int> CountRunsByStatusAsync(string resourceId, string status, CancellationToken cancellationToken)
    {
        var statuses = status switch
        {
            "unsupported" => new[] { "failed", "cancelled" },
            _ => new[] { status }
        };
        var sessions = await _runRepository.ListSessionsByResourceIdAsync(resourceId, 1, 5000, statuses, cancellationToken);
        return sessions.Select(session => ToRunHistoryItem(resourceId, session))
            .Count(item => string.Equals(item.Status, status, StringComparison.OrdinalIgnoreCase));
    }

    private static MicroflowRunHistoryItemDto ToRunHistoryItem(string resourceId, MicroflowRunSessionEntity session)
    {
        var endedAt = session.EndedAt;
        var durationMs = endedAt.HasValue
            ? Math.Max(0, (int)(endedAt.Value - session.StartedAt).TotalMilliseconds)
            : 0;
        var error = string.IsNullOrWhiteSpace(session.ErrorJson)
            ? null
            : Deserialize<MicroflowRuntimeErrorDto>(session.ErrorJson);
        var status = NormalizeRunHistoryStatus(session.Status, error?.Code);
        var summary = status switch
        {
            "success" => "Run succeeded",
            "unsupported" => "Run failed on unsupported action",
            "cancelled" => "Run cancelled",
            _ => "Run failed"
        };

        return new MicroflowRunHistoryItemDto
        {
            RunId = session.Id,
            MicroflowId = resourceId,
            Status = status,
            DurationMs = durationMs,
            StartedAt = session.StartedAt,
            CompletedAt = endedAt,
            ErrorMessage = error?.Message,
            Summary = summary
        };
    }

    private static string NormalizeRunHistoryStatus(string rawStatus, string? errorCode)
    {
        if (string.Equals(rawStatus, "success", StringComparison.OrdinalIgnoreCase))
        {
            return "success";
        }

        if (string.Equals(rawStatus, "cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return "cancelled";
        }

        if (!string.IsNullOrWhiteSpace(errorCode)
            && errorCode.Contains("UNSUPPORTED", StringComparison.OrdinalIgnoreCase))
        {
            return "unsupported";
        }

        return "failed";
    }

    private static MicroflowTraceFrameDto ToFrameDto(MicroflowRunTraceFrameEntity frame)
        => new()
        {
            Id = frame.Id,
            RunId = frame.RunId,
            MicroflowId = ReadTraceFrameExtra(frame.ExtraJson).MicroflowId,
            ObjectId = frame.ObjectId,
            ActionId = frame.ActionId,
            CollectionId = frame.CollectionId,
            IncomingFlowId = frame.IncomingFlowId,
            OutgoingFlowId = frame.OutgoingFlowId,
            SelectedCaseValue = ParseOptional(frame.SelectedCaseValueJson),
            LoopIteration = ParseOptional(frame.LoopIterationJson),
            Status = frame.Status,
            StartedAt = frame.StartedAt,
            EndedAt = frame.EndedAt,
            DurationMs = frame.DurationMs,
            Input = ParseOptional(frame.InputJson),
            Output = ParseOptional(frame.OutputJson),
            Error = string.IsNullOrWhiteSpace(frame.ErrorJson) ? null : Deserialize<MicroflowRuntimeErrorDto>(frame.ErrorJson),
            VariablesSnapshot = string.IsNullOrWhiteSpace(frame.VariablesSnapshotJson)
                ? null
                : Deserialize<Dictionary<string, MicroflowRuntimeVariableValueDto>>(frame.VariablesSnapshotJson),
            Message = frame.Message,
            ErrorHandlerVisited = ReadTraceFrameExtra(frame.ExtraJson).ErrorHandlerVisited,
            ParentRunId = ReadTraceFrameExtra(frame.ExtraJson).ParentRunId,
            RootRunId = ReadTraceFrameExtra(frame.ExtraJson).RootRunId,
            CallFrameId = ReadTraceFrameExtra(frame.ExtraJson).CallFrameId,
            CallDepth = ReadTraceFrameExtra(frame.ExtraJson).CallDepth,
            CallerObjectId = ReadTraceFrameExtra(frame.ExtraJson).CallerObjectId,
            CallerActionId = ReadTraceFrameExtra(frame.ExtraJson).CallerActionId
        };

    private static MicroflowRuntimeLogDto ToLogDto(MicroflowRunLogEntity log)
    {
        var extra = ReadLogExtra(log.ExtraJson);
        return new MicroflowRuntimeLogDto
        {
            Id = log.Id,
            Timestamp = log.Timestamp,
            Level = log.Level,
            ObjectId = log.ObjectId,
            ActionId = log.ActionId,
            Message = log.Message,
            LogNodeName = extra.LogNodeName,
            TraceId = extra.TraceId,
            VariablesPreview = extra.VariablesPreview,
            StructuredFieldsJson = extra.StructuredFieldsJson
        };
    }

    private static JsonElement? ParseOptional(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static T? Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, JsonOptions);

    private static SessionExtra ReadSessionExtra(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SessionExtra();
        }

        try
        {
            return JsonSerializer.Deserialize<SessionExtra>(json, JsonOptions) ?? new SessionExtra();
        }
        catch (JsonException)
        {
            return new SessionExtra();
        }
    }

    private static LogExtra ReadLogExtra(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new LogExtra();
        }

        try
        {
            return JsonSerializer.Deserialize<LogExtra>(json, JsonOptions) ?? new LogExtra();
        }
        catch (JsonException)
        {
            return new LogExtra();
        }
    }

    private sealed record SessionExtra
    {
        public string Version { get; init; } = string.Empty;

        public IReadOnlyList<MicroflowVariableSnapshotDto> Variables { get; init; } = Array.Empty<MicroflowVariableSnapshotDto>();

        public MicroflowRuntimeTransactionSummary? TransactionSummary { get; init; }

        public string? ParentRunId { get; init; }

        public string? RootRunId { get; init; }

        public string? CallFrameId { get; init; }

        public int? CallDepth { get; init; }

        public string? CorrelationId { get; init; }

        public IReadOnlyList<string> CallStack { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> ChildRunIds { get; init; } = Array.Empty<string>();
    }

    private sealed record LogExtra
    {
        public string? LogNodeName { get; init; }
        public string? TraceId { get; init; }
        public JsonElement? VariablesPreview { get; init; }
        public string? StructuredFieldsJson { get; init; }
    }

    private static TraceFrameExtra ReadTraceFrameExtra(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new TraceFrameExtra();
        }

        try
        {
            return JsonSerializer.Deserialize<TraceFrameExtra>(json, JsonOptions) ?? new TraceFrameExtra();
        }
        catch (JsonException)
        {
            return new TraceFrameExtra();
        }
    }

    private sealed record TraceFrameExtra
    {
        public bool? ErrorHandlerVisited { get; init; }

        public string? ParentRunId { get; init; }

        public string? RootRunId { get; init; }

        public string? CallFrameId { get; init; }

        public int? CallDepth { get; init; }

        public string? CallerObjectId { get; init; }

        public string? CallerActionId { get; init; }

        public string? MicroflowId { get; init; }
    }
}
