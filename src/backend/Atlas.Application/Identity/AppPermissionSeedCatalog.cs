namespace Atlas.Application.Identity;

public static class AppPermissionSeedCatalog
{
    public static IReadOnlyList<string> AllPermissionCodes { get; } =
    [
        PermissionCodes.AppUser,
        PermissionCodes.AppAdmin,
        PermissionCodes.AppsView,
        PermissionCodes.AppsUpdate,
        PermissionCodes.AppMembersView,
        PermissionCodes.AppMembersUpdate,
        PermissionCodes.AppRolesView,
        PermissionCodes.AppRolesUpdate,
        PermissionCodes.UsersView,
        PermissionCodes.UsersCreate,
        PermissionCodes.UsersUpdate,
        PermissionCodes.UsersDelete,
        PermissionCodes.UsersAssignRoles,
        PermissionCodes.UsersAssignDepartments,
        PermissionCodes.UsersAssignPositions,
        PermissionCodes.RolesView,
        PermissionCodes.RolesCreate,
        PermissionCodes.RolesUpdate,
        PermissionCodes.RolesDelete,
        PermissionCodes.RolesAssignPermissions,
        PermissionCodes.RolesAssignMenus,
        PermissionCodes.PermissionsView,
        PermissionCodes.PermissionsCreate,
        PermissionCodes.PermissionsUpdate,
        PermissionCodes.DepartmentsView,
        PermissionCodes.DepartmentsAll,
        PermissionCodes.DepartmentsCreate,
        PermissionCodes.DepartmentsUpdate,
        PermissionCodes.DepartmentsDelete,
        PermissionCodes.PositionsView,
        PermissionCodes.PositionsCreate,
        PermissionCodes.PositionsUpdate,
        PermissionCodes.PositionsDelete,
        PermissionCodes.MenusView,
        PermissionCodes.MenusAll,
        PermissionCodes.MenusCreate,
        PermissionCodes.MenusUpdate,
        PermissionCodes.MenusDelete,
        PermissionCodes.DataScopeManage,
        PermissionCodes.AgentView,
        PermissionCodes.AgentCreate,
        PermissionCodes.AgentUpdate,
        PermissionCodes.AgentDelete,
        PermissionCodes.KnowledgeBaseView,
        PermissionCodes.KnowledgeBaseCreate,
        PermissionCodes.KnowledgeBaseUpdate,
        PermissionCodes.KnowledgeBaseDelete,
        PermissionCodes.ModelConfigView,
        PermissionCodes.ModelConfigCreate,
        PermissionCodes.ModelConfigUpdate,
        PermissionCodes.ModelConfigDelete,
        PermissionCodes.AiWorkflowView,
        PermissionCodes.AiWorkflowCreate,
        PermissionCodes.AiWorkflowUpdate,
        PermissionCodes.AiWorkflowDelete,
        PermissionCodes.AiWorkflowExecute,
        PermissionCodes.AiWorkflowDebug,
        PermissionCodes.WorkflowView,
        PermissionCodes.WorkflowDesign,
        PermissionCodes.ApprovalFlowView,
        PermissionCodes.ApprovalFlowManage,
        PermissionCodes.ApprovalFlowCreate,
        PermissionCodes.ApprovalFlowUpdate,
        PermissionCodes.ApprovalFlowPublish,
        PermissionCodes.ApprovalFlowDelete,
        PermissionCodes.ApprovalFlowDisable,
        PermissionCodes.DebugView,
        PermissionCodes.DebugRun,
        PermissionCodes.DebugManage,
        PermissionCodes.AuditView,
        PermissionCodes.LoginLogView,
        PermissionCodes.AssetsView,
        PermissionCodes.AssetsCreate,
        PermissionCodes.FileUpload,
        PermissionCodes.FileDownload,
        PermissionCodes.FileDelete,
        PermissionCodes.AiMarketplaceView
    ];

    public static IReadOnlyList<string> GetPermissionCodesForRole(string roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
        {
            return [];
        }

        if (string.Equals(roleCode, "AppAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return AllPermissionCodes;
        }

        if (string.Equals(roleCode, "AppMember", StringComparison.OrdinalIgnoreCase))
        {
            return MemberPermissionCodes;
        }

        if (string.Equals(roleCode, "SecurityAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return SecurityAdminPermissionCodes;
        }

        if (string.Equals(roleCode, "AuditAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return AuditAdminPermissionCodes;
        }

        if (string.Equals(roleCode, "AssetAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return AssetAdminPermissionCodes;
        }

        if (string.Equals(roleCode, "ApprovalAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return ApprovalAdminPermissionCodes;
        }

        return MemberPermissionCodes;
    }

    private static readonly IReadOnlyList<string> MemberPermissionCodes =
    [
        PermissionCodes.AppUser,
        PermissionCodes.AppsView
    ];

    private static readonly IReadOnlyList<string> SecurityAdminPermissionCodes =
    [
        PermissionCodes.AppUser,
        PermissionCodes.AppsView,
        PermissionCodes.AppMembersView,
        PermissionCodes.AppMembersUpdate,
        PermissionCodes.AppRolesView,
        PermissionCodes.AppRolesUpdate,
        PermissionCodes.UsersView,
        PermissionCodes.UsersCreate,
        PermissionCodes.UsersUpdate,
        PermissionCodes.UsersDelete,
        PermissionCodes.UsersAssignRoles,
        PermissionCodes.UsersAssignDepartments,
        PermissionCodes.UsersAssignPositions,
        PermissionCodes.RolesView,
        PermissionCodes.RolesCreate,
        PermissionCodes.RolesUpdate,
        PermissionCodes.RolesDelete,
        PermissionCodes.RolesAssignPermissions,
        PermissionCodes.RolesAssignMenus,
        PermissionCodes.PermissionsView,
        PermissionCodes.PermissionsCreate,
        PermissionCodes.PermissionsUpdate,
        PermissionCodes.DepartmentsView,
        PermissionCodes.DepartmentsAll,
        PermissionCodes.DepartmentsCreate,
        PermissionCodes.DepartmentsUpdate,
        PermissionCodes.DepartmentsDelete,
        PermissionCodes.PositionsView,
        PermissionCodes.PositionsCreate,
        PermissionCodes.PositionsUpdate,
        PermissionCodes.PositionsDelete,
        PermissionCodes.MenusView,
        PermissionCodes.MenusAll,
        PermissionCodes.MenusCreate,
        PermissionCodes.MenusUpdate,
        PermissionCodes.MenusDelete,
        PermissionCodes.DataScopeManage
    ];

    private static readonly IReadOnlyList<string> AuditAdminPermissionCodes =
    [
        PermissionCodes.AppUser,
        PermissionCodes.AppsView,
        PermissionCodes.AuditView,
        PermissionCodes.LoginLogView
    ];

    private static readonly IReadOnlyList<string> AssetAdminPermissionCodes =
    [
        PermissionCodes.AppUser,
        PermissionCodes.AppsView,
        PermissionCodes.AssetsView,
        PermissionCodes.AssetsCreate,
        PermissionCodes.FileUpload,
        PermissionCodes.FileDownload,
        PermissionCodes.FileDelete
    ];

    private static readonly IReadOnlyList<string> ApprovalAdminPermissionCodes =
    [
        PermissionCodes.AppUser,
        PermissionCodes.AppsView,
        PermissionCodes.AiWorkflowView,
        PermissionCodes.AiWorkflowCreate,
        PermissionCodes.AiWorkflowUpdate,
        PermissionCodes.AiWorkflowDelete,
        PermissionCodes.AiWorkflowExecute,
        PermissionCodes.AiWorkflowDebug,
        PermissionCodes.WorkflowView,
        PermissionCodes.WorkflowDesign,
        PermissionCodes.ApprovalFlowView,
        PermissionCodes.ApprovalFlowManage,
        PermissionCodes.ApprovalFlowCreate,
        PermissionCodes.ApprovalFlowUpdate,
        PermissionCodes.ApprovalFlowPublish,
        PermissionCodes.ApprovalFlowDelete,
        PermissionCodes.ApprovalFlowDisable
    ];
}
