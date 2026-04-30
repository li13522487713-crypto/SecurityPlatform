using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Core.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Microflows.Controllers;

[Route("api/v1/microflow-apps/{appId}/domain-model/modules/{moduleId}")]
public sealed class MendixDomainModelController : MicroflowApiControllerBase
{
    private readonly IMendixDomainModelService _service;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public MendixDomainModelController(
        IMendixDomainModelService service,
        IMicroflowRequestContextAccessor requestContextAccessor,
        ICurrentUserAccessor currentUserAccessor)
        : base(requestContextAccessor)
    {
        _service = service;
        _requestContextAccessor = requestContextAccessor;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(MicroflowApiResponse<MendixDomainModelDocumentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MendixDomainModelDocumentDto>>> GetDocument(
        string appId,
        string moduleId,
        [FromQuery] string? workspaceId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetOrCreateAsync(appId, ResolveWorkspaceId(workspaceId), moduleId, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPut]
    [ProducesResponseType(typeof(MicroflowApiResponse<MendixDomainModelDocumentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MendixDomainModelDocumentDto>>> SaveDocument(
        string appId,
        string moduleId,
        [FromQuery] string? workspaceId,
        [FromBody] MendixDomainModelDocumentDto document,
        CancellationToken cancellationToken)
    {
        var result = await _service.SaveAsync(appId, ResolveWorkspaceId(workspaceId), moduleId, document, CurrentUserId(), cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPut("bindings")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MendixDomainModelDocumentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MendixDomainModelDocumentDto>>> UpdateBindings(
        string appId,
        string moduleId,
        [FromQuery] string? workspaceId,
        [FromBody] IReadOnlyList<MendixDomainModelBindingDto> bindings,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateBindingsAsync(appId, ResolveWorkspaceId(workspaceId), moduleId, bindings, CurrentUserId(), cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPost("import-tables")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MendixDomainModelImportResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MendixDomainModelImportResultDto>>> ImportTables(
        string appId,
        string moduleId,
        [FromQuery] string? workspaceId,
        [FromBody] MendixDomainModelImportTablesRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _service.ImportTablesAsync(appId, ResolveWorkspaceId(workspaceId), moduleId, request, CurrentUserId(), cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPost("preview-sync")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MendixDomainModelSyncPlanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MendixDomainModelSyncPlanDto>>> PreviewSync(
        string appId,
        string moduleId,
        [FromQuery] string? workspaceId,
        CancellationToken cancellationToken)
    {
        var result = await _service.PreviewSyncAsync(appId, ResolveWorkspaceId(workspaceId), moduleId, cancellationToken);
        return MicroflowOk(result);
    }

    [HttpPost("sync-draft")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MendixDomainModelSyncResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<MicroflowApiResponse<MendixDomainModelSyncResultDto>>> SyncDraft(
        string appId,
        string moduleId,
        [FromQuery] string? workspaceId,
        CancellationToken cancellationToken)
    {
        var result = await _service.SyncDraftAsync(appId, ResolveWorkspaceId(workspaceId), moduleId, CurrentUserId(), cancellationToken);
        return MicroflowOk(result);
    }

    private string ResolveWorkspaceId(string? workspaceId)
    {
        var resolved = workspaceId ?? _requestContextAccessor.Current.WorkspaceId;
        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new InvalidOperationException("缺少工作区上下文。");
        }

        return resolved;
    }

    private long? CurrentUserId()
        => _currentUserAccessor.GetCurrentUser()?.UserId;
}
