using Atlas.Application.Coze.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Controllers.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Authorize]
public sealed class CozeDebuggerCompatController : ControllerBase
{
    private readonly IWorkspaceTestsetService _workspaceTestsetService;
    private readonly ITenantProvider _tenantProvider;

    public CozeDebuggerCompatController(
        IWorkspaceTestsetService workspaceTestsetService,
        ITenantProvider tenantProvider)
    {
        _workspaceTestsetService = workspaceTestsetService;
        _tenantProvider = tenantProvider;
    }

    [HttpPost("/api/devops/debugger/v1/coze/testcase/casedata/mget")]
    public async Task<ActionResult<object>> MGetCaseData(
        [FromBody] CozeMGetCaseDataRequest? request,
        CancellationToken cancellationToken)
    {
        var workspaceId = request?.bizCtx?.bizSpaceID;
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return Ok(CozeCompatGatewaySupport.Success(new
            {
                cases = Array.Empty<object>(),
                hasNext = false,
                nextToken = string.Empty
            }));
        }

        var page = await _workspaceTestsetService.ListCaseDataAsync(
            _tenantProvider.GetTenantId(),
            workspaceId.Trim(),
            request?.bizComponentSubject?.parentComponentID,
            request?.caseName,
            request?.pageLimit ?? 30,
            request?.nextToken,
            cancellationToken);

        var cases = page.Cases.Select(item => new
        {
            caseBase = new
            {
                caseID = item.CaseBase.CaseId,
                name = item.CaseBase.Name,
                description = item.CaseBase.Description ?? string.Empty,
                input = item.CaseBase.Input,
                isDefault = item.CaseBase.IsDefault
            },
            creatorID = item.CreatorId,
            createTimeInSec = item.CreateTimeInSec,
            updateTimeInSec = item.UpdateTimeInSec,
            schemaIncompatible = false
        }).ToArray();

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            cases,
            hasNext = page.HasNext,
            nextToken = page.NextToken ?? string.Empty
        }));
    }
}

public sealed record CozeMGetCaseDataRequest(
    CozeDebuggerBizCtx? bizCtx,
    CozeDebuggerComponentSubject? bizComponentSubject,
    int? pageLimit,
    string? nextToken,
    string? caseName);

public sealed record CozeDebuggerBizCtx(
    string? connectorID,
    string? connectorUID,
    string? trafficCallerID,
    string? bizSpaceID);

public sealed record CozeDebuggerComponentSubject(
    string? componentID,
    int? componentType,
    string? parentComponentID,
    int? parentComponentType);
