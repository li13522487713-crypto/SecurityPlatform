using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;
using Atlas.Domain.Audit.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 证据链导出服务：按业务 Key 聚合审批实例 + 审计日志，生成 JSON 或将来扩展为 PDF
/// </summary>
public sealed class EvidenceChainService
{
    private readonly IApprovalRuntimeQueryService _approvalQuery;
    private readonly ISqlSugarClient _db;

    public EvidenceChainService(
        IApprovalRuntimeQueryService approvalQuery,
        ISqlSugarClient db)
    {
        _approvalQuery = approvalQuery;
        _db = db;
    }

    /// <summary>按业务 Key 聚合证据链</summary>
    public async Task<EvidenceChain> BuildAsync(
        TenantId tenantId,
        string businessKey,
        CancellationToken cancellationToken = default)
    {
        var approvalResult = await _approvalQuery.GetInstancesPagedAsync(
            tenantId,
            new PagedRequest(1, 100, null, null, false),
            businessKey: businessKey,
            cancellationToken: cancellationToken);

        var auditRecords = await _db.Queryable<AuditRecord>()
            .Where(r => r.TenantIdValue == tenantId.Value && r.Target.Contains(businessKey))
            .OrderBy(r => r.OccurredAt)
            .ToListAsync(cancellationToken);

        return new EvidenceChain(
            BusinessKey: businessKey,
            TenantId: tenantId.ToString()!,
            ExportedAt: DateTimeOffset.UtcNow,
            ApprovalInstances: approvalResult.Items.Select(i => new ApprovalEvidence(
                i.Id, i.DefinitionId, i.BusinessKey, i.Status.ToString(),
                i.StartedAt, i.EndedAt, i.FlowName, null)).ToList(),
            AuditLogs: auditRecords.Select(r => new AuditEvidence(
                r.Id, r.Actor, r.Action, r.Result, r.Target,
                r.IpAddress, r.OccurredAt)).ToList());
    }
}

public sealed record EvidenceChain(
    string BusinessKey,
    string TenantId,
    DateTimeOffset ExportedAt,
    IReadOnlyList<ApprovalEvidence> ApprovalInstances,
    IReadOnlyList<AuditEvidence> AuditLogs);

public sealed record ApprovalEvidence(
    long InstanceId,
    long DefinitionId,
    string BusinessKey,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    string? FlowName,
    string? DataJson);

public sealed record AuditEvidence(
    long Id,
    string Actor,
    string Action,
    string Result,
    string Target,
    string IpAddress,
    DateTimeOffset OccurredAt);
