using Atlas.Application.Approval.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Services.ApprovalFlow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/approval/writeback-failures")]
[Authorize(Roles = "system:admin,approval:admin")]
public sealed class ApprovalWritebackFailuresController : ControllerBase
{
    private readonly IApprovalWritebackFailureRepository _repository;
    private readonly ApprovalStatusSyncHandler _syncHandler;
    private readonly ITenantProvider _tenantProvider;

    public ApprovalWritebackFailuresController(
        IApprovalWritebackFailureRepository repository,
        ApprovalStatusSyncHandler syncHandler,
        ITenantProvider tenantProvider)
    {
        _repository = repository;
        _syncHandler = syncHandler;
        _tenantProvider = tenantProvider;
    }

    /// <summary>
    /// 查询未解决的回写失败列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WritebackFailureDto>>>> GetUnresolved(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var items = await _repository.GetUnresolvedAsync(tenantId, limit, cancellationToken);
        var dtos = items.Select(x => new WritebackFailureDto(
            x.Id,
            x.BusinessKey,
            x.TargetStatus,
            x.RetryCount,
            x.ErrorMessage,
            x.FirstFailedAt,
            x.LastAttemptAt,
            x.IsResolved)).ToList();

        return Ok(ApiResponse<IReadOnlyList<WritebackFailureDto>>.Ok(dtos, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 手动重试指定失败记录
    /// </summary>
    [HttpPost("{id:long}/retry")]
    public async Task<ActionResult<ApiResponse<object?>>> Retry(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var failure = await _repository.GetByIdAsync(tenantId, id, cancellationToken);
        if (failure is null)
            return NotFound(ApiResponse<object?>.Fail("NOT_FOUND", "回写失败记录不存在", HttpContext.TraceIdentifier));

        try
        {
            await _syncHandler.SyncStatusAsync(tenantId, failure.BusinessKey, failure.TargetStatus, cancellationToken);
            failure.MarkResolved();
            await _repository.UpdateAsync(failure, cancellationToken);
            return Ok(ApiResponse<object?>.Ok(null, HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            failure.UpdateAttempt(failure.RetryCount + 1, ex.Message);
            await _repository.UpdateAsync(failure, cancellationToken);
            return Ok(ApiResponse<object?>.Fail("SERVER_ERROR", ex.Message, HttpContext.TraceIdentifier));
        }
    }
}

public sealed record WritebackFailureDto(
    long Id,
    string BusinessKey,
    string TargetStatus,
    int RetryCount,
    string ErrorMessage,
    DateTimeOffset FirstFailedAt,
    DateTimeOffset LastAttemptAt,
    bool IsResolved);
