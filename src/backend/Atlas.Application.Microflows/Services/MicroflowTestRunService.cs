using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Runtime.Transactions;
using Atlas.Domain.Microflows.Entities;

namespace Atlas.Application.Microflows.Services;

public sealed class MicroflowTestRunService : IMicroflowTestRunService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMicroflowResourceRepository _resourceRepository;
    private readonly IMicroflowSchemaSnapshotRepository _schemaSnapshotRepository;
    private readonly IMicroflowRunRepository _runRepository;
    private readonly IMicroflowStorageTransaction _storageTransaction;
    private readonly IMicroflowValidationService _validationService;
    private readonly IMicroflowMetadataService _metadataService;
    private readonly IMicroflowMockRuntimeRunner _runner;
    private readonly IMicroflowExecutionPlanLoader _executionPlanLoader;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;
    private readonly IMicroflowClock _clock;

    public MicroflowTestRunService(
        IMicroflowResourceRepository resourceRepository,
        IMicroflowSchemaSnapshotRepository schemaSnapshotRepository,
        IMicroflowRunRepository runRepository,
        IMicroflowStorageTransaction storageTransaction,
        IMicroflowValidationService validationService,
        IMicroflowMetadataService metadataService,
        IMicroflowMockRuntimeRunner runner,
        IMicroflowExecutionPlanLoader executionPlanLoader,
        IMicroflowRequestContextAccessor requestContextAccessor,
        IMicroflowClock clock)
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
        _clock = clock;
    }

    public async Task<TestRunMicroflowApiResponse> TestRunAsync(
        string resourceId,
        TestRunMicroflowApiRequest request,
        CancellationToken cancellationToken)
    {
        var resource = await _resourceRepository.GetByIdAsync(resourceId, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流资源不存在。", 404);
        var (schema, schemaId) = await ResolveSchemaAsync(resource, request.Schema, cancellationToken);

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

        var session = await _runner.RunAsync(
            new MicroflowMockRuntimeRequest
            {
                ResourceId = resource.Id,
                SchemaId = schemaId,
                Version = resource.Version,
                Schema = schema,
                ExecutionPlan = executionPlan,
                Input = request.Input ?? new Dictionary<string, JsonElement>(),
                Options = request.Options ?? new MicroflowTestRunOptionsDto(),
                Metadata = metadata,
                RequestContext = _requestContextAccessor.Current
            },
            cancellationToken);

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

        return new TestRunMicroflowApiResponse { Session = session };
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
        var session = await _runRepository.GetSessionAsync(runId, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流运行会话不存在。", 404);

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
        var session = await _runRepository.GetSessionAsync(runId, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流运行会话不存在。", 404);
        var frames = await _runRepository.ListTraceFramesAsync(runId, cancellationToken);
        var logs = await _runRepository.ListLogsAsync(runId, cancellationToken);
        return ToDto(session, frames, logs);
    }

    public async Task<GetMicroflowRunTraceResponse> GetRunTraceAsync(string runId, CancellationToken cancellationToken)
    {
        _ = await _runRepository.GetSessionAsync(runId, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流运行会话不存在。", 404);
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
            // 第 48 轮只做只读 ExecutionPlan 预热，不能改变既有 Mock TestRun 行为。
            return null;
        }
    }

    private async Task<(JsonElement Schema, string SchemaId)> ResolveSchemaAsync(
        MicroflowResourceEntity resource,
        JsonElement? draftSchema,
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

        var snapshot = !string.IsNullOrWhiteSpace(resource.CurrentSchemaSnapshotId)
            ? await _schemaSnapshotRepository.GetByIdAsync(resource.CurrentSchemaSnapshotId, cancellationToken)
            : await _schemaSnapshotRepository.GetLatestByResourceIdAsync(resource.Id, cancellationToken);
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.SchemaJson))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowSchemaInvalid, "微流当前 Schema 不存在。", 400);
        }

        return (MicroflowSchemaJsonHelper.ParseRequired(snapshot.SchemaJson), snapshot.Id);
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
                variables = session.Variables,
                transactionSummary = session.TransactionSummary,
                childRunIds = session.ChildRunIds.Count > 0 ? session.ChildRunIds : session.ChildRuns.Select(child => child.Id).ToArray()
            }, JsonOptions)
        };

        var frames = session.Trace.Select((frame, index) => new MicroflowRunTraceFrameEntity
        {
            Id = frame.Id,
            RunId = session.Id,
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
                CallerActionId = frame.CallerActionId
            }, JsonOptions)
        }).ToArray();

        var logs = session.Logs.Select(log => new MicroflowRunLogEntity
        {
            Id = log.Id,
            RunId = session.Id,
            Timestamp = log.Timestamp,
            Level = log.Level,
            ObjectId = log.ObjectId,
            ActionId = log.ActionId,
            Message = log.Message
        }).ToArray();

        return (entity, frames, logs);
    }

    private static MicroflowRunSessionDto ToDto(
        MicroflowRunSessionEntity session,
        IReadOnlyList<MicroflowRunTraceFrameEntity> frames,
        IReadOnlyList<MicroflowRunLogEntity> logs)
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
            ChildRunIds = extra.ChildRunIds
        };
    }

    private static MicroflowTraceFrameDto ToFrameDto(MicroflowRunTraceFrameEntity frame)
        => new()
        {
            Id = frame.Id,
            RunId = frame.RunId,
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
        => new()
        {
            Id = log.Id,
            Timestamp = log.Timestamp,
            Level = log.Level,
            ObjectId = log.ObjectId,
            ActionId = log.ActionId,
            Message = log.Message
        };

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

    private sealed record SessionExtra
    {
        public string Version { get; init; } = string.Empty;

        public IReadOnlyList<MicroflowVariableSnapshotDto> Variables { get; init; } = Array.Empty<MicroflowVariableSnapshotDto>();

        public MicroflowRuntimeTransactionSummary? TransactionSummary { get; init; }

        public string? ParentRunId { get; init; }

        public string? RootRunId { get; init; }

        public string? CallFrameId { get; init; }

        public int? CallDepth { get; init; }

        public IReadOnlyList<string> ChildRunIds { get; init; } = Array.Empty<string>();
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
    }
}
