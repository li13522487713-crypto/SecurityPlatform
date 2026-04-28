using System.Globalization;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Authorize]
public sealed class CozeLibraryResourceCompatController : ControllerBase
{
    private readonly IAiDatabaseService _databaseService;
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly ITenantProvider _tenantProvider;

    public CozeLibraryResourceCompatController(
        IAiDatabaseService databaseService,
        IKnowledgeBaseService knowledgeBaseService,
        ITenantProvider tenantProvider)
    {
        _databaseService = databaseService;
        _knowledgeBaseService = knowledgeBaseService;
        _tenantProvider = tenantProvider;
    }

    [HttpPost("/api/memory/database/delete")]
    public async Task<ActionResult<object>> DeleteDatabase(
        [FromBody] CozeDeleteDatabaseRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(request?.id, out var id))
        {
            return Ok(Fail("id is invalid"));
        }

        await _databaseService.DeleteAsync(_tenantProvider.GetTenantId(), id, cancellationToken);
        return Ok(SuccessWithoutData());
    }

    [HttpPost("/api/knowledge/update")]
    public async Task<ActionResult<object>> UpdateDataset(
        [FromBody] CozeUpdateDatasetRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(request?.dataset_id, out var id))
        {
            return Ok(Fail("dataset_id is invalid"));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var existing = await _knowledgeBaseService.GetByIdAsync(tenantId, id, cancellationToken);
        if (existing is null)
        {
            return Ok(Fail("dataset not found"));
        }

        var updateRequest = new KnowledgeBaseUpdateRequest(
            string.IsNullOrWhiteSpace(request?.name) ? existing.Name : request!.name!,
            request?.description ?? existing.Description,
            existing.Type,
            existing.WorkspaceId,
            existing.Kind,
            existing.Provider,
            existing.ProviderConfigId,
            existing.ChunkingProfile,
            existing.RetrievalProfile,
            existing.Tags);

        await _knowledgeBaseService.UpdateAsync(tenantId, id, updateRequest, cancellationToken);
        return Ok(SuccessWithoutData());
    }

    [HttpPost("/api/knowledge/delete")]
    public async Task<ActionResult<object>> DeleteDataset(
        [FromBody] CozeDeleteDatasetRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(request?.dataset_id, out var id))
        {
            return Ok(Fail("dataset_id is invalid"));
        }

        await _knowledgeBaseService.DeleteAsync(_tenantProvider.GetTenantId(), id, cancellationToken);
        return Ok(SuccessWithoutData());
    }

    private static object SuccessWithoutData()
    {
        return new
        {
            code = 0,
            msg = "success",
            BaseResp = new { }
        };
    }

    private static object Fail(string message)
    {
        return new
        {
            code = 400,
            msg = message,
            BaseResp = new { }
        };
    }

    private static bool TryParsePositiveId(string? raw, out long value)
    {
        value = 0;
        return !string.IsNullOrWhiteSpace(raw)
               && long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
               && value > 0;
    }
}

public sealed record CozeDeleteDatabaseRequest(string? id);

public sealed record CozeDeleteDatasetRequest(string? dataset_id);

public sealed record CozeUpdateDatasetRequest(
    string? dataset_id,
    string? name,
    string? icon_uri,
    string? description,
    int? status);
