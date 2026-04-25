namespace Atlas.Application.Identity;

public static class AppPermissionSeedCatalog
{
    public static IReadOnlyList<AppPermissionSeed> PermissionSeeds { get; } =
    [
        new(PermissionCodes.AppUser, "App User", "Api"),
        new(PermissionCodes.AppAdmin, "App Admin", "Api"),
        new(PermissionCodes.AppsView, "Apps View", "Api"),
        new(PermissionCodes.AppsUpdate, "Apps Update", "Api"),
        new(PermissionCodes.AppMembersView, "App Members View", "Api"),
        new(PermissionCodes.AppMembersUpdate, "App Members Update", "Api"),
        new(PermissionCodes.AppRolesView, "App Roles View", "Api"),
        new(PermissionCodes.AppRolesUpdate, "App Roles Update", "Api"),
        new(PermissionCodes.UsersView, "Users View", "Api"),
        new(PermissionCodes.UsersCreate, "Users Create", "Api"),
        new(PermissionCodes.UsersUpdate, "Users Update", "Api"),
        new(PermissionCodes.UsersDelete, "Users Delete", "Api"),
        new(PermissionCodes.UsersAssignRoles, "Users Assign Roles", "Api"),
        new(PermissionCodes.UsersAssignDepartments, "Users Assign Departments", "Api"),
        new(PermissionCodes.UsersAssignPositions, "Users Assign Positions", "Api"),
        new(PermissionCodes.RolesView, "Roles View", "Api"),
        new(PermissionCodes.RolesCreate, "Roles Create", "Api"),
        new(PermissionCodes.RolesUpdate, "Roles Update", "Api"),
        new(PermissionCodes.RolesDelete, "Roles Delete", "Api"),
        new(PermissionCodes.RolesAssignPermissions, "Roles Assign Permissions", "Api"),
        new(PermissionCodes.RolesAssignMenus, "Roles Assign Menus", "Api"),
        new(PermissionCodes.PermissionsView, "Permissions View", "Api"),
        new(PermissionCodes.PermissionsCreate, "Permissions Create", "Api"),
        new(PermissionCodes.PermissionsUpdate, "Permissions Update", "Api"),
        new(PermissionCodes.DepartmentsView, "Departments View", "Api"),
        new(PermissionCodes.DepartmentsAll, "Departments All", "Api"),
        new(PermissionCodes.DepartmentsCreate, "Departments Create", "Api"),
        new(PermissionCodes.DepartmentsUpdate, "Departments Update", "Api"),
        new(PermissionCodes.DepartmentsDelete, "Departments Delete", "Api"),
        new(PermissionCodes.PositionsView, "Positions View", "Api"),
        new(PermissionCodes.PositionsCreate, "Positions Create", "Api"),
        new(PermissionCodes.PositionsUpdate, "Positions Update", "Api"),
        new(PermissionCodes.PositionsDelete, "Positions Delete", "Api"),
        new(PermissionCodes.MenusView, "Menus View", "Api"),
        new(PermissionCodes.MenusAll, "Menus All", "Api"),
        new(PermissionCodes.MenusCreate, "Menus Create", "Api"),
        new(PermissionCodes.MenusUpdate, "Menus Update", "Api"),
        new(PermissionCodes.MenusDelete, "Menus Delete", "Api"),
        new(PermissionCodes.DataScopeManage, "Data Scope Manage", "Api"),
        new(PermissionCodes.DataSourcesView, "Data Sources View", "Api"),
        new(PermissionCodes.DataSourcesCreate, "Data Sources Create", "Api"),
        new(PermissionCodes.DataSourcesUpdate, "Data Sources Update", "Api"),
        new(PermissionCodes.DataSourcesDelete, "Data Sources Delete", "Api"),
        new(PermissionCodes.DataSourcesQuery, "Data Sources Query", "Api"),
        new(PermissionCodes.DataSourcesSchemaWrite, "Data Sources Schema Write", "Api"),
        new(PermissionCodes.AgentView, "Agent View", "Api"),
        new(PermissionCodes.AgentCreate, "Agent Create", "Api"),
        new(PermissionCodes.AgentUpdate, "Agent Update", "Api"),
        new(PermissionCodes.AgentDelete, "Agent Delete", "Api"),
        new(PermissionCodes.ConversationView, "Conversation View", "Api"),
        new(PermissionCodes.ConversationCreate, "Conversation Create", "Api"),
        new(PermissionCodes.ConversationDelete, "Conversation Delete", "Api"),
        new(PermissionCodes.KnowledgeBaseView, "Knowledge Base View", "Api"),
        new(PermissionCodes.KnowledgeBaseCreate, "Knowledge Base Create", "Api"),
        new(PermissionCodes.KnowledgeBaseUpdate, "Knowledge Base Update", "Api"),
        new(PermissionCodes.KnowledgeBaseDelete, "Knowledge Base Delete", "Api"),
        new(PermissionCodes.ModelConfigView, "Model Config View", "Api"),
        new(PermissionCodes.ModelConfigCreate, "Model Config Create", "Api"),
        new(PermissionCodes.ModelConfigUpdate, "Model Config Update", "Api"),
        new(PermissionCodes.ModelConfigDelete, "Model Config Delete", "Api"),
        new(PermissionCodes.AiWorkflowView, "AI Workflow View", "Api"),
        new(PermissionCodes.AiWorkflowCreate, "AI Workflow Create", "Api"),
        new(PermissionCodes.AiWorkflowUpdate, "AI Workflow Update", "Api"),
        new(PermissionCodes.AiWorkflowDelete, "AI Workflow Delete", "Api"),
        new(PermissionCodes.AiWorkflowExecute, "AI Workflow Execute", "Api"),
        new(PermissionCodes.AiWorkflowDebug, "AI Workflow Debug", "Api"),
        new(PermissionCodes.WorkflowView, "Workflow View", "Api"),
        new(PermissionCodes.WorkflowDesign, "Workflow Designer", "Menu"),
        new(PermissionCodes.AiDatabaseView, "AI Database View", "Api"),
        new(PermissionCodes.AiDatabaseCreate, "AI Database Create", "Api"),
        new(PermissionCodes.AiDatabaseUpdate, "AI Database Update", "Api"),
        new(PermissionCodes.AiDatabaseDelete, "AI Database Delete", "Api"),
        new(PermissionCodes.AiVariableView, "AI Variable View", "Api"),
        new(PermissionCodes.AiVariableCreate, "AI Variable Create", "Api"),
        new(PermissionCodes.AiVariableUpdate, "AI Variable Update", "Api"),
        new(PermissionCodes.AiVariableDelete, "AI Variable Delete", "Api"),
        new(PermissionCodes.AiPluginView, "AI Plugin View", "Api"),
        new(PermissionCodes.AiPluginCreate, "AI Plugin Create", "Api"),
        new(PermissionCodes.AiPluginUpdate, "AI Plugin Update", "Api"),
        new(PermissionCodes.AiPluginDelete, "AI Plugin Delete", "Api"),
        new(PermissionCodes.AiPluginPublish, "AI Plugin Publish", "Api"),
        new(PermissionCodes.AiPluginDebug, "AI Plugin Debug", "Api"),
        new(PermissionCodes.AiAppView, "AI App View", "Api"),
        new(PermissionCodes.AiAppCreate, "AI App Create", "Api"),
        new(PermissionCodes.AiAppUpdate, "AI App Update", "Api"),
        new(PermissionCodes.AiAppDelete, "AI App Delete", "Api"),
        new(PermissionCodes.AiAppPublish, "AI App Publish", "Api"),
        new(PermissionCodes.AiPromptView, "AI Prompt View", "Api"),
        new(PermissionCodes.AiPromptCreate, "AI Prompt Create", "Api"),
        new(PermissionCodes.AiPromptUpdate, "AI Prompt Update", "Api"),
        new(PermissionCodes.AiPromptDelete, "AI Prompt Delete", "Api"),
        new(PermissionCodes.AiMarketplaceView, "AI Marketplace View", "Api"),
        new(PermissionCodes.AiMarketplaceCreate, "AI Marketplace Create", "Api"),
        new(PermissionCodes.AiMarketplaceUpdate, "AI Marketplace Update", "Api"),
        new(PermissionCodes.AiMarketplaceDelete, "AI Marketplace Delete", "Api"),
        new(PermissionCodes.AiMarketplacePublish, "AI Marketplace Publish", "Api"),
        new(PermissionCodes.AiSearchView, "AI Search View", "Api"),
        new(PermissionCodes.AiSearchUpdate, "AI Search Update", "Api"),
        new(PermissionCodes.AiAdminConfigView, "AI Admin Config View", "Api"),
        new(PermissionCodes.AiAdminConfigUpdate, "AI Admin Config Update", "Api"),
        new(PermissionCodes.AiWorkspaceView, "AI Workspace View", "Api"),
        new(PermissionCodes.AiWorkspaceUpdate, "AI Workspace Update", "Api"),
        new(PermissionCodes.AiDevopsView, "AI DevOps View", "Api"),
        new(PermissionCodes.AiShortcutView, "AI Shortcut View", "Api"),
        new(PermissionCodes.AiShortcutManage, "AI Shortcut Manage", "Api"),
        new(PermissionCodes.PersonalAccessTokenView, "PAT View", "Api"),
        new(PermissionCodes.PersonalAccessTokenCreate, "PAT Create", "Api"),
        new(PermissionCodes.PersonalAccessTokenUpdate, "PAT Update", "Api"),
        new(PermissionCodes.PersonalAccessTokenDelete, "PAT Delete", "Api"),
        new(PermissionCodes.ApprovalFlowView, "Approval Flow View", "Api"),
        new(PermissionCodes.ApprovalFlowManage, "Approval Flow Manage", "Api"),
        new(PermissionCodes.ApprovalFlowCreate, "Approval Flow Create", "Api"),
        new(PermissionCodes.ApprovalFlowUpdate, "Approval Flow Update", "Api"),
        new(PermissionCodes.ApprovalFlowPublish, "Approval Flow Publish", "Api"),
        new(PermissionCodes.ApprovalFlowDelete, "Approval Flow Delete", "Api"),
        new(PermissionCodes.ApprovalFlowDisable, "Approval Flow Disable", "Api"),
        new(PermissionCodes.DebugView, "Debug View", "Api"),
        new(PermissionCodes.DebugRun, "Debug Run", "Api"),
        new(PermissionCodes.DebugManage, "Debug Manage", "Api"),
        new(PermissionCodes.AuditView, "Audit View", "Api"),
        new(PermissionCodes.LoginLogView, "Login Log View", "Api"),
        new(PermissionCodes.AssetsView, "Assets View", "Api"),
        new(PermissionCodes.AssetsCreate, "Assets Create", "Api"),
        new(PermissionCodes.FileUpload, "File Upload", "Api"),
        new(PermissionCodes.FileDownload, "File Download", "Api"),
        new(PermissionCodes.FileDelete, "File Delete", "Api")
    ];

    public static IReadOnlyList<string> AllPermissionCodes { get; } =
        PermissionSeeds.Select(seed => seed.Code).ToArray();

    private static readonly IReadOnlyDictionary<string, AppPermissionSeed> PermissionSeedMap =
        PermissionSeeds.ToDictionary(seed => seed.Code, StringComparer.OrdinalIgnoreCase);

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

    public static AppPermissionSeed ResolvePermissionSeed(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new AppPermissionSeed(string.Empty, string.Empty, "Api");
        }

        return PermissionSeedMap.TryGetValue(code, out var seed)
            ? seed
            : new AppPermissionSeed(code, code, "Api");
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
        PermissionCodes.AgentView,
        PermissionCodes.AgentCreate,
        PermissionCodes.AgentUpdate,
        PermissionCodes.AgentDelete,
        PermissionCodes.ConversationView,
        PermissionCodes.ConversationCreate,
        PermissionCodes.ConversationDelete,
        PermissionCodes.AppMembersView,
        PermissionCodes.AppMembersUpdate,
        PermissionCodes.AppRolesView,
        PermissionCodes.AppRolesUpdate,
        PermissionCodes.KnowledgeBaseView,
        PermissionCodes.KnowledgeBaseCreate,
        PermissionCodes.KnowledgeBaseUpdate,
        PermissionCodes.KnowledgeBaseDelete,
        PermissionCodes.ModelConfigView,
        PermissionCodes.ModelConfigCreate,
        PermissionCodes.ModelConfigUpdate,
        PermissionCodes.ModelConfigDelete,
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
        PermissionCodes.DataSourcesView,
        PermissionCodes.DataSourcesCreate,
        PermissionCodes.DataSourcesUpdate,
        PermissionCodes.DataSourcesDelete,
        PermissionCodes.DataSourcesQuery,
        PermissionCodes.DataSourcesSchemaWrite,
        PermissionCodes.AiWorkflowView,
        PermissionCodes.AiWorkflowCreate,
        PermissionCodes.AiWorkflowUpdate,
        PermissionCodes.AiWorkflowDelete,
        PermissionCodes.AiWorkflowExecute,
        PermissionCodes.AiWorkflowDebug,
        PermissionCodes.WorkflowView,
        PermissionCodes.WorkflowDesign,
        PermissionCodes.AiDatabaseView,
        PermissionCodes.AiDatabaseCreate,
        PermissionCodes.AiDatabaseUpdate,
        PermissionCodes.AiDatabaseDelete,
        PermissionCodes.AiVariableView,
        PermissionCodes.AiVariableCreate,
        PermissionCodes.AiVariableUpdate,
        PermissionCodes.AiVariableDelete,
        PermissionCodes.AiPluginView,
        PermissionCodes.AiPluginCreate,
        PermissionCodes.AiPluginUpdate,
        PermissionCodes.AiPluginDelete,
        PermissionCodes.AiPluginPublish,
        PermissionCodes.AiPluginDebug,
        PermissionCodes.AiAppView,
        PermissionCodes.AiAppCreate,
        PermissionCodes.AiAppUpdate,
        PermissionCodes.AiAppDelete,
        PermissionCodes.AiAppPublish,
        PermissionCodes.AiPromptView,
        PermissionCodes.AiPromptCreate,
        PermissionCodes.AiPromptUpdate,
        PermissionCodes.AiPromptDelete,
        PermissionCodes.AiMarketplaceView,
        PermissionCodes.AiMarketplaceCreate,
        PermissionCodes.AiMarketplaceUpdate,
        PermissionCodes.AiMarketplaceDelete,
        PermissionCodes.AiMarketplacePublish,
        PermissionCodes.AiSearchView,
        PermissionCodes.AiSearchUpdate,
        PermissionCodes.AiAdminConfigView,
        PermissionCodes.AiAdminConfigUpdate,
        PermissionCodes.AiWorkspaceView,
        PermissionCodes.AiWorkspaceUpdate,
        PermissionCodes.AiDevopsView,
        PermissionCodes.AiShortcutView,
        PermissionCodes.AiShortcutManage,
        PermissionCodes.PersonalAccessTokenView,
        PermissionCodes.PersonalAccessTokenCreate,
        PermissionCodes.PersonalAccessTokenUpdate,
        PermissionCodes.PersonalAccessTokenDelete,
        PermissionCodes.DebugView,
        PermissionCodes.DebugRun,
        PermissionCodes.DebugManage
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

public sealed record AppPermissionSeed(string Code, string Name, string Type);
