using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Microsoft.Extensions.DependencyInjection;
using Atlas.WebApi.Helpers;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 审批代理人控制器
/// </summary>
[ApiController]
[Route("api/v1/approval/agents")]
[Authorize]
public sealed class ApprovalAgentController : ControllerBase
{
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    // 需要注入 IApprovalAgentRepository

    public ApprovalAgentController(
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    /// <summary>
    /// 获取我的代理设置
    /// </summary>
    [HttpGet]
    public async Task<ApiResponse<List<ApprovalAgentConfig>>> GetMyAgentsAsync(CancellationToken cancellationToken = default)
    {
        // var configs = await _agentRepository.GetByPrincipalIdAsync(...)
        return ApiResponse<List<ApprovalAgentConfig>>.Ok(new List<ApprovalAgentConfig>(), HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 创建代理设置
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<string>> CreateAgentAsync(
        [FromBody] ApprovalAgentConfig config,
        CancellationToken cancellationToken = default)
    {
        // await _agentRepository.AddAsync(config);
        return ApiResponse<string>.Ok("已创建", HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// 删除代理设置
    /// </summary>
    [HttpDelete("{id:long}")]
    public async Task<ApiResponse<string>> DeleteAgentAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        // await _agentRepository.DeleteAsync(id);
        return ApiResponse<string>.Ok("已删除", HttpContext.TraceIdentifier);
    }
}
