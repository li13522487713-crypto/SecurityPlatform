namespace Atlas.Application.Identity;

public static class PermissionCodes
{
    public const string SystemAdmin = "system:admin";
    public const string WorkflowDesign = "workflow:design";

    public const string UsersView = "users:view";
    public const string UsersCreate = "users:create";
    public const string UsersUpdate = "users:update";
    public const string UsersDelete = "users:delete";
    public const string UsersAssignRoles = "users:assign-roles";
    public const string UsersAssignDepartments = "users:assign-departments";
    public const string UsersAssignPositions = "users:assign-positions";

    public const string RolesView = "roles:view";
    public const string RolesCreate = "roles:create";
    public const string RolesUpdate = "roles:update";
    public const string RolesDelete = "roles:delete";
    public const string RolesAssignPermissions = "roles:assign-permissions";
    public const string RolesAssignMenus = "roles:assign-menus";

    public const string PermissionsView = "permissions:view";
    public const string PermissionsCreate = "permissions:create";
    public const string PermissionsUpdate = "permissions:update";

    public const string DepartmentsView = "departments:view";
    public const string DepartmentsAll = "departments:all";
    public const string DepartmentsCreate = "departments:create";
    public const string DepartmentsUpdate = "departments:update";
    public const string DepartmentsDelete = "departments:delete";

    public const string PositionsView = "positions:view";
    public const string PositionsCreate = "positions:create";
    public const string PositionsUpdate = "positions:update";
    public const string PositionsDelete = "positions:delete";

    public const string MenusView = "menus:view";
    public const string MenusAll = "menus:all";
    public const string MenusCreate = "menus:create";
    public const string MenusUpdate = "menus:update";

    public const string AppsView = "apps:view";
    public const string AppsUpdate = "apps:update";
    public const string AppAdmin = "app:admin";
    public const string AppUser = "app:user";

    public const string ProjectsView = "projects:view";
    public const string ProjectsCreate = "projects:create";
    public const string ProjectsUpdate = "projects:update";
    public const string ProjectsDelete = "projects:delete";
    public const string ProjectsAssignUsers = "projects:assign-users";
    public const string ProjectsAssignDepartments = "projects:assign-departments";
    public const string ProjectsAssignPositions = "projects:assign-positions";

    public const string AuditView = "audit:view";
    public const string AssetsView = "assets:view";
    public const string AssetsCreate = "assets:create";
    public const string AlertView = "alert:view";

    public const string ModelConfigView = "model-config:view";
    public const string ModelConfigCreate = "model-config:create";
    public const string ModelConfigUpdate = "model-config:update";
    public const string ModelConfigDelete = "model-config:delete";

    public const string AgentView = "agent:view";
    public const string AgentCreate = "agent:create";
    public const string AgentUpdate = "agent:update";
    public const string AgentDelete = "agent:delete";
    public const string ConversationView = "conversation:view";
    public const string ConversationCreate = "conversation:create";
    public const string ConversationDelete = "conversation:delete";
    public const string KnowledgeBaseView = "knowledge-base:view";
    public const string KnowledgeBaseCreate = "knowledge-base:create";
    public const string KnowledgeBaseUpdate = "knowledge-base:update";
    public const string KnowledgeBaseDelete = "knowledge-base:delete";
    public const string AiWorkflowView = "ai-workflow:view";
    public const string AiWorkflowCreate = "ai-workflow:create";
    public const string AiWorkflowUpdate = "ai-workflow:update";
    public const string AiWorkflowDelete = "ai-workflow:delete";
    public const string AiWorkflowExecute = "ai-workflow:execute";
    public const string AiDatabaseView = "ai-database:view";
    public const string AiDatabaseCreate = "ai-database:create";
    public const string AiDatabaseUpdate = "ai-database:update";
    public const string AiDatabaseDelete = "ai-database:delete";
    public const string AiVariableView = "ai-variable:view";
    public const string AiVariableCreate = "ai-variable:create";
    public const string AiVariableUpdate = "ai-variable:update";
    public const string AiVariableDelete = "ai-variable:delete";
    public const string AiPluginView = "ai-plugin:view";
    public const string AiPluginCreate = "ai-plugin:create";
    public const string AiPluginUpdate = "ai-plugin:update";
    public const string AiPluginDelete = "ai-plugin:delete";
    public const string AiPluginPublish = "ai-plugin:publish";
    public const string AiPluginDebug = "ai-plugin:debug";
    public const string AiAppView = "ai-app:view";
    public const string AiAppCreate = "ai-app:create";
    public const string AiAppUpdate = "ai-app:update";
    public const string AiAppDelete = "ai-app:delete";
    public const string AiAppPublish = "ai-app:publish";
    public const string AiPromptView = "ai-prompt:view";
    public const string AiPromptCreate = "ai-prompt:create";
    public const string AiPromptUpdate = "ai-prompt:update";
    public const string AiPromptDelete = "ai-prompt:delete";
    public const string AiMarketplaceView = "ai-marketplace:view";
    public const string AiMarketplaceCreate = "ai-marketplace:create";
    public const string AiMarketplaceUpdate = "ai-marketplace:update";
    public const string AiMarketplaceDelete = "ai-marketplace:delete";
    public const string AiMarketplacePublish = "ai-marketplace:publish";
    public const string AiSearchView = "ai-search:view";
    public const string AiSearchUpdate = "ai-search:update";
    public const string PersonalAccessTokenView = "pat:view";
    public const string PersonalAccessTokenCreate = "pat:create";
    public const string PersonalAccessTokenUpdate = "pat:update";
    public const string PersonalAccessTokenDelete = "pat:delete";

    public const string ApprovalFlowView = "approval:flow:view";
    public const string ApprovalFlowManage = "approval:flow:manage";
    public const string ApprovalFlowCreate = "approval:flow:create";
    public const string ApprovalFlowUpdate = "approval:flow:update";
    public const string ApprovalFlowPublish = "approval:flow:publish";
    public const string ApprovalFlowDelete = "approval:flow:delete";
    public const string ApprovalFlowDisable = "approval:flow:disable";

    public const string VisualizationView = "visualization:view";
    public const string VisualizationProcessSave = "visualization:process:save";
    public const string VisualizationProcessUpdate = "visualization:process:update";
    public const string VisualizationProcessPublish = "visualization:process:publish";

    public const string DictTypeView = "dict:type:view";
    public const string DictTypeCreate = "dict:type:create";
    public const string DictTypeUpdate = "dict:type:update";
    public const string DictTypeDelete = "dict:type:delete";
    public const string DictDataView = "dict:data:view";
    public const string DictDataCreate = "dict:data:create";
    public const string DictDataUpdate = "dict:data:update";
    public const string DictDataDelete = "dict:data:delete";

    public const string ConfigView = "config:view";
    public const string ConfigCreate = "config:create";
    public const string ConfigUpdate = "config:update";
    public const string ConfigDelete = "config:delete";

    public const string LoginLogView = "loginlog:view";
    public const string LoginLogDelete = "loginlog:delete";
    public const string OnlineUsersView = "online:view";
    public const string OnlineUsersForceLogout = "online:force-logout";
    public const string MonitorView = "monitor:view";

    public const string NotificationView = "notification:view";
    public const string NotificationCreate = "notification:create";
    public const string NotificationUpdate = "notification:update";
    public const string NotificationDelete = "notification:delete";

    public const string JobView = "job:view";
    public const string JobCreate = "job:create";
    public const string JobUpdate = "job:update";
    public const string JobDelete = "job:delete";
    public const string JobTrigger = "job:trigger";

    public const string DataScopeManage = "datascope:manage";
    public const string FileUpload = "file:upload";
    public const string FileDownload = "file:download";
    public const string FileDelete = "file:delete";
}
