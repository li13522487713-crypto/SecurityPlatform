using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.Platform;

public sealed class AppOrganizationQueryService : IAppOrganizationQueryService
{
    private readonly ITenantAppMemberQueryService _memberQueryService;
    private readonly ITenantAppRoleQueryService _roleQueryService;
    private readonly IAppOrgQueryService _appOrgQueryService;

    public AppOrganizationQueryService(
        ITenantAppMemberQueryService memberQueryService,
        ITenantAppRoleQueryService roleQueryService,
        IAppOrgQueryService appOrgQueryService)
    {
        _memberQueryService = memberQueryService;
        _roleQueryService = roleQueryService;
        _appOrgQueryService = appOrgQueryService;
    }

    public async Task<AppOrganizationWorkspaceResponse> GetWorkspaceAsync(
        TenantId tenantId,
        long appId,
        PagedRequest request,
        long? roleId = null,
        CancellationToken cancellationToken = default)
    {
        var memberQuery = new PagedRequest(request.PageIndex, request.PageSize, request.Keyword);
        var membersTask = roleId.HasValue
            ? _memberQueryService.QueryByRoleAsync(tenantId, appId, roleId.Value, memberQuery, cancellationToken)
            : _memberQueryService.QueryAsync(tenantId, appId, memberQuery, cancellationToken);
        var roleOverviewTask = _roleQueryService.GetGovernanceOverviewAsync(tenantId, appId, cancellationToken);
        var rolesTask = _roleQueryService.QueryAsync(tenantId, appId, new PagedRequest(1, 200, null), isSystem: null, cancellationToken);
        var departmentsTask = _appOrgQueryService.GetAllDepartmentsAsync(tenantId, appId, cancellationToken);
        var positionsTask = _appOrgQueryService.GetAllPositionsAsync(tenantId, appId, cancellationToken);
        var projectsTask = _appOrgQueryService.GetAllProjectsAsync(tenantId, appId, cancellationToken);

        await Task.WhenAll(membersTask, roleOverviewTask, rolesTask, departmentsTask, positionsTask, projectsTask);

        var rolesPaged = await rolesTask;
        return new AppOrganizationWorkspaceResponse(
            appId.ToString(),
            await membersTask,
            await roleOverviewTask,
            rolesPaged.Items,
            await departmentsTask,
            await positionsTask,
            await projectsTask);
    }
}

public sealed class AppOrganizationCommandService : IAppOrganizationCommandService
{
    private readonly ITenantAppMemberQueryService _memberQueryService;
    private readonly ITenantAppMemberCommandService _memberCommandService;
    private readonly ITenantAppRoleCommandService _roleCommandService;
    private readonly IAppOrgCommandService _appOrgCommandService;
    private readonly IUserCommandService _userCommandService;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public AppOrganizationCommandService(
        ITenantAppMemberQueryService memberQueryService,
        ITenantAppMemberCommandService memberCommandService,
        ITenantAppRoleCommandService roleCommandService,
        IAppOrgCommandService appOrgCommandService,
        IUserCommandService userCommandService,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _memberQueryService = memberQueryService;
        _memberCommandService = memberCommandService;
        _roleCommandService = roleCommandService;
        _appOrgCommandService = appOrgCommandService;
        _userCommandService = userCommandService;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<long> CreateMemberUserAsync(
        TenantId tenantId,
        long appId,
        AppOrganizationCreateMemberUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _idGeneratorAccessor.NextId();
        var createUserRequest = new UserCreateRequest(
            request.Username,
            request.Password,
            request.DisplayName,
            request.Email,
            request.PhoneNumber,
            request.IsActive,
            Array.Empty<long>(),
            Array.Empty<long>(),
            Array.Empty<long>());

        var createdUserId = await _userCommandService.CreateAsync(
            tenantId,
            createUserRequest,
            userId,
            cancellationToken);

        var roleIds = ParseIdList(request.RoleIds, "roleIds");
        var departmentIds = ParseNullableIdList(request.DepartmentIds, "departmentIds");
        var positionIds = ParseNullableIdList(request.PositionIds, "positionIds");
        var projectIds = ParseNullableIdList(request.ProjectIds, "projectIds");
        await _memberCommandService.AddMembersAsync(
            tenantId,
            appId,
            createdUserId,
            new TenantAppMemberAssignRequest(new[] { createdUserId }, roleIds, departmentIds, positionIds, projectIds),
            cancellationToken);

        return createdUserId;
    }

    public async Task AddMembersAsync(
        TenantId tenantId,
        long appId,
        long operatorUserId,
        AppOrganizationAssignMembersRequest request,
        CancellationToken cancellationToken = default)
    {
        var userIds = ParseIdList(request.UserIds, "userIds");
        var roleIds = ParseIdList(request.RoleIds, "roleIds");
        var departmentIds = ParseNullableIdList(request.DepartmentIds, "departmentIds");
        var positionIds = ParseNullableIdList(request.PositionIds, "positionIds");
        var projectIds = ParseNullableIdList(request.ProjectIds, "projectIds");
        await _memberCommandService.AddMembersAsync(
            tenantId,
            appId,
            operatorUserId,
            new TenantAppMemberAssignRequest(userIds, roleIds, departmentIds, positionIds, projectIds),
            cancellationToken);
    }

    public async Task UpdateMemberRolesAsync(
        TenantId tenantId,
        long appId,
        string userId,
        AppOrganizationUpdateMemberRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        await _memberCommandService.UpdateMemberRolesAsync(
            tenantId,
            appId,
            ParseId(userId, "userId"),
            new TenantAppMemberUpdateRolesRequest(
                ParseIdList(request.RoleIds, "roleIds"),
                ParseNullableIdList(request.DepartmentIds, "departmentIds"),
                ParseNullableIdList(request.PositionIds, "positionIds"),
                ParseNullableIdList(request.ProjectIds, "projectIds")),
            cancellationToken);
    }

    public async Task ResetMemberPasswordAsync(
        TenantId tenantId,
        long appId,
        string userId,
        AppOrganizationResetMemberPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var parsedUserId = ParseId(userId, "userId");
        var isMember = await _memberQueryService.GetByUserIdAsync(tenantId, appId, parsedUserId, cancellationToken);
        if (isMember is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "OrganizationMemberNotFound");
        }

        await _userCommandService.ResetPasswordAsync(
            tenantId,
            parsedUserId,
            request.NewPassword,
            cancellationToken);
    }

    public async Task UpdateMemberProfileAsync(
        TenantId tenantId,
        long appId,
        string userId,
        AppOrganizationUpdateMemberProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var parsedUserId = ParseId(userId, "userId");
        var member = await _memberQueryService.GetByUserIdAsync(tenantId, appId, parsedUserId, cancellationToken);
        if (member is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "OrganizationMemberNotFound");
        }

        await _userCommandService.UpdateBasicInfoAsync(
            tenantId,
            parsedUserId,
            request.DisplayName,
            request.Email,
            request.PhoneNumber,
            request.IsActive,
            cancellationToken);
    }

    public async Task RemoveMemberAsync(
        TenantId tenantId,
        long appId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await _memberCommandService.RemoveMemberAsync(tenantId, appId, ParseId(userId, "userId"), cancellationToken);
    }

    public async Task<long> CreateRoleAsync(
        TenantId tenantId,
        long appId,
        long operatorUserId,
        AppOrganizationCreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _roleCommandService.CreateAsync(
            tenantId,
            appId,
            operatorUserId,
            new TenantAppRoleCreateRequest(
                request.Code,
                request.Name,
                request.Description,
                request.PermissionCodes?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray() ?? Array.Empty<string>()),
            cancellationToken);
    }

    public async Task UpdateRoleAsync(
        TenantId tenantId,
        long appId,
        string roleId,
        long operatorUserId,
        AppOrganizationUpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        await _roleCommandService.UpdateAsync(
            tenantId,
            appId,
            ParseId(roleId, "roleId"),
            operatorUserId,
            new TenantAppRoleUpdateRequest(request.Name, request.Description),
            cancellationToken);
    }

    public async Task DeleteRoleAsync(
        TenantId tenantId,
        long appId,
        string roleId,
        CancellationToken cancellationToken = default)
    {
        await _roleCommandService.DeleteAsync(tenantId, appId, ParseId(roleId, "roleId"), cancellationToken);
    }

    public async Task<long> CreateDepartmentAsync(
        TenantId tenantId,
        long appId,
        AppOrganizationCreateDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _appOrgCommandService.CreateDepartmentAsync(
            tenantId,
            appId,
            new AppDepartmentCreateRequest(request.Name, request.Code, ParseNullableId(request.ParentId, "parentId"), request.SortOrder),
            cancellationToken);
    }

    public async Task UpdateDepartmentAsync(
        TenantId tenantId,
        long appId,
        string id,
        AppOrganizationUpdateDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        await _appOrgCommandService.UpdateDepartmentAsync(
            tenantId,
            appId,
            ParseId(id, "id"),
            new AppDepartmentUpdateRequest(request.Name, request.Code, ParseNullableId(request.ParentId, "parentId"), request.SortOrder),
            cancellationToken);
    }

    public async Task DeleteDepartmentAsync(
        TenantId tenantId,
        long appId,
        string id,
        CancellationToken cancellationToken = default)
    {
        await _appOrgCommandService.DeleteDepartmentAsync(tenantId, appId, ParseId(id, "id"), cancellationToken);
    }

    public async Task<long> CreatePositionAsync(
        TenantId tenantId,
        long appId,
        AppOrganizationCreatePositionRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _appOrgCommandService.CreatePositionAsync(
            tenantId,
            appId,
            new AppPositionCreateRequest(request.Name, request.Code, request.Description, request.IsActive, request.SortOrder),
            cancellationToken);
    }

    public async Task UpdatePositionAsync(
        TenantId tenantId,
        long appId,
        string id,
        AppOrganizationUpdatePositionRequest request,
        CancellationToken cancellationToken = default)
    {
        await _appOrgCommandService.UpdatePositionAsync(
            tenantId,
            appId,
            ParseId(id, "id"),
            new AppPositionUpdateRequest(request.Name, request.Description, request.IsActive, request.SortOrder),
            cancellationToken);
    }

    public async Task DeletePositionAsync(
        TenantId tenantId,
        long appId,
        string id,
        CancellationToken cancellationToken = default)
    {
        await _appOrgCommandService.DeletePositionAsync(tenantId, appId, ParseId(id, "id"), cancellationToken);
    }

    public async Task<long> CreateProjectAsync(
        TenantId tenantId,
        long appId,
        AppOrganizationCreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _appOrgCommandService.CreateProjectAsync(
            tenantId,
            appId,
            new AppProjectCreateRequest(request.Code, request.Name, request.Description, request.IsActive),
            cancellationToken);
    }

    public async Task UpdateProjectAsync(
        TenantId tenantId,
        long appId,
        string id,
        AppOrganizationUpdateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        await _appOrgCommandService.UpdateProjectAsync(
            tenantId,
            appId,
            ParseId(id, "id"),
            new AppProjectUpdateRequest(request.Name, request.Description, request.IsActive),
            cancellationToken);
    }

    public async Task DeleteProjectAsync(
        TenantId tenantId,
        long appId,
        string id,
        CancellationToken cancellationToken = default)
    {
        await _appOrgCommandService.DeleteProjectAsync(tenantId, appId, ParseId(id, "id"), cancellationToken);
    }

    private static long ParseId(string rawId, string fieldName)
    {
        if (!long.TryParse(rawId, out var id) || id <= 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, $"Organization{fieldName}Invalid");
        }

        return id;
    }

    private static long? ParseNullableId(string? rawId, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(rawId))
        {
            return null;
        }

        return ParseId(rawId, fieldName);
    }

    private static long[] ParseIdList(IReadOnlyList<string> rawIds, string fieldName)
    {
        return rawIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => ParseId(id, fieldName))
            .Distinct()
            .ToArray();
    }

    private static long[]? ParseNullableIdList(IReadOnlyList<string>? rawIds, string fieldName)
    {
        if (rawIds is null)
        {
            return null;
        }

        return ParseIdList(rawIds, fieldName);
    }
}
