using System.Text.Json;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// Unified assignee resolution service.
/// Extracts the duplicated assignee resolution logic from FlowEngine, BackToAnyNodeOperationHandler,
/// and GenerateCopyRecordsForNodeAsync into a single reusable service.
/// </summary>
public sealed class AssigneeResolver
{
    private readonly IApprovalUserQueryService _userQueryService;
    private readonly IApprovalDepartmentLeaderRepository _deptLeaderRepository;

    public AssigneeResolver(
        IApprovalUserQueryService userQueryService,
        IApprovalDepartmentLeaderRepository deptLeaderRepository)
    {
        _userQueryService = userQueryService;
        _deptLeaderRepository = deptLeaderRepository;
    }

    /// <summary>
    /// Resolve user IDs based on assignee type and value.
    /// </summary>
    public async Task<List<long>> ResolveUserIdsAsync(
        TenantId tenantId,
        long initiatorUserId,
        AssigneeType assigneeType,
        string assigneeValue,
        string? instanceDataJson,
        CancellationToken cancellationToken)
    {
        var userIds = new List<long>();

        switch (assigneeType)
        {
            case AssigneeType.User:
                var userIdStrings = ParseUserIds(assigneeValue);
                userIds = userIdStrings
                    .Select(x => long.TryParse(x, out var id) ? id : (long?)null)
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .ToList();
                break;

            case AssigneeType.Role:
                if (!string.IsNullOrEmpty(assigneeValue))
                {
                    var roleUserIds = await _userQueryService.GetUserIdsByRoleCodeAsync(tenantId, assigneeValue, cancellationToken);
                    userIds.AddRange(roleUserIds);
                }
                break;

            case AssigneeType.DepartmentLeader:
                if (long.TryParse(assigneeValue, out var deptId))
                {
                    var leaderId = await _deptLeaderRepository.GetLeaderUserIdAsync(tenantId, deptId, cancellationToken);
                    if (leaderId.HasValue) userIds.Add(leaderId.Value);
                }
                break;

            case AssigneeType.Loop:
                var loopUserIds = await _userQueryService.GetLoopApproversAsync(tenantId, initiatorUserId, cancellationToken: cancellationToken);
                userIds.AddRange(loopUserIds);
                break;

            case AssigneeType.Level:
                if (int.TryParse(assigneeValue, out var targetLevel) && targetLevel > 0)
                {
                    var levelUserId = await _userQueryService.GetLevelApproverAsync(tenantId, initiatorUserId, targetLevel, cancellationToken);
                    if (levelUserId.HasValue) userIds.Add(levelUserId.Value);
                }
                break;

            case AssigneeType.DirectLeader:
                var directLeaderId = await _userQueryService.GetDirectLeaderUserIdAsync(tenantId, initiatorUserId, cancellationToken);
                if (directLeaderId.HasValue) userIds.Add(directLeaderId.Value);
                break;

            case AssigneeType.StartUser:
                userIds.Add(initiatorUserId);
                break;

            case AssigneeType.Hrbp:
                var hrbpUserId = await _userQueryService.GetHrbpUserIdAsync(tenantId, initiatorUserId, cancellationToken);
                if (hrbpUserId.HasValue) userIds.Add(hrbpUserId.Value);
                break;

            case AssigneeType.Customize:
            case AssigneeType.BusinessTable:
            case AssigneeType.OutSideAccess:
                userIds = ParseUserIdsFromInstanceData(instanceDataJson, assigneeValue);
                break;
        }

        // Validate user IDs
        if (userIds.Count > 0)
        {
            userIds = (await _userQueryService.ValidateUserIdsAsync(tenantId, userIds, cancellationToken)).ToList();
        }

        return userIds;
    }

    /// <summary>
    /// Parse user ID list (supports comma-separated or JSON array format).
    /// </summary>
    public static List<string> ParseUserIds(string assigneeValue)
    {
        var userIds = new List<string>();
        if (string.IsNullOrEmpty(assigneeValue)) return userIds;

        try
        {
            using var doc = JsonDocument.Parse(assigneeValue);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var userId = element.ValueKind switch
                    {
                        JsonValueKind.Number => element.GetInt64().ToString(),
                        JsonValueKind.String => element.GetString(),
                        _ => null
                    };
                    if (!string.IsNullOrEmpty(userId)) userIds.Add(userId);
                }
                return userIds;
            }
        }
        catch
        {
            // Not JSON, try comma-separated
        }

        var parts = assigneeValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed)) userIds.Add(trimmed);
        }
        return userIds;
    }

    /// <summary>
    /// Parse user IDs from instance data JSON by field name.
    /// </summary>
    public static List<long> ParseUserIdsFromInstanceData(string? dataJson, string fieldName)
    {
        var userIds = new List<long>();
        if (string.IsNullOrEmpty(dataJson) || string.IsNullOrEmpty(fieldName)) return userIds;

        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(fieldName, out var fieldElement))
            {
                if (fieldElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in fieldElement.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var userId))
                            userIds.Add(userId);
                        else if (element.ValueKind == JsonValueKind.String && long.TryParse(element.GetString(), out var userIdStr))
                            userIds.Add(userIdStr);
                    }
                }
                else if (fieldElement.ValueKind == JsonValueKind.Number && fieldElement.TryGetInt64(out var singleUserId))
                    userIds.Add(singleUserId);
                else if (fieldElement.ValueKind == JsonValueKind.String && long.TryParse(fieldElement.GetString(), out var singleUserIdStr))
                    userIds.Add(singleUserIdStr);
            }
        }
        catch
        {
            // Parse failed, return empty
        }

        return userIds;
    }
}
