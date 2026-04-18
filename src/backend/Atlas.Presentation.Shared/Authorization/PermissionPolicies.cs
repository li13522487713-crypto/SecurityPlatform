namespace Atlas.Presentation.Shared.Authorization;

public static class PermissionPolicies
{
    public const string PolicyPrefix = "Permission:";

    public static string For(string permissionCode)
    {
        return $"{PolicyPrefix}{permissionCode}";
    }

    public const string SystemAdmin = "Permission:system:admin";
    public const string WorkflowDesign = "Permission:workflow:design";
    public const string WorkflowView = "Permission:workflow:view";

    public const string WebhooksView = "Permission:webhooks:view";
    public const string WebhooksCreate = "Permission:webhooks:create";
    public const string WebhooksUpdate = "Permission:webhooks:update";
    public const string WebhooksDelete = "Permission:webhooks:delete";
    public const string WebhooksTest = "Permission:webhooks:test";

    public const string TemplatesView = "Permission:templates:view";
    public const string TemplatesCreate = "Permission:templates:create";
    public const string TemplatesUpdate = "Permission:templates:update";
    public const string TemplatesDelete = "Permission:templates:delete";
    public const string TemplatesInstantiate = "Permission:templates:instantiate";

    public const string ConnectorsView = "Permission:connectors:view";
    public const string ConnectorsCreate = "Permission:connectors:create";
    public const string ConnectorsUpdate = "Permission:connectors:update";
    public const string ConnectorsDelete = "Permission:connectors:delete";
    public const string ConnectorsSync = "Permission:connectors:sync";
    public const string ConnectorsExecute = "Permission:connectors:execute";

    public const string PlatformEventsView = "Permission:platform-events:view";

    public const string PackagesExport = "Permission:packages:export";
    public const string PackagesImport = "Permission:packages:import";
    public const string PackagesAnalyze = "Permission:packages:analyze";

    public const string AlertRulesView = "Permission:alert-rules:view";
    public const string AlertRulesCreate = "Permission:alert-rules:create";
    public const string AlertRulesUpdate = "Permission:alert-rules:update";
    public const string AlertRulesDelete = "Permission:alert-rules:delete";

    public const string MeteringView = "Permission:metering:view";
    public const string MeteringUpdate = "Permission:metering:update";

    public const string LicenseView = "Permission:license:view";
    public const string LicenseManage = "Permission:license:manage";

    public const string ToolPoliciesView = "Permission:tool-policies:view";

    public const string TenantsView = "Permission:system:tenant:query";
    public const string TenantsCreate = "Permission:system:tenant:create";
    public const string TenantsUpdate = "Permission:system:tenant:update";
    public const string TenantsDelete = "Permission:system:tenant:delete";

    public const string DataSourcesView = "Permission:system:datasource:view";
    public const string DataSourcesCreate = "Permission:system:datasource:create";
    public const string DataSourcesUpdate = "Permission:system:datasource:update";
    public const string DataSourcesDelete = "Permission:system:datasource:delete";
    public const string DataSourcesQuery = "Permission:system:datasource:query";

    public const string UsersView = "Permission:users:view";
    public const string UsersCreate = "Permission:users:create";
    public const string UsersUpdate = "Permission:users:update";
    public const string UsersDelete = "Permission:users:delete";
    public const string UsersAssignRoles = "Permission:users:assign-roles";
    public const string UsersAssignDepartments = "Permission:users:assign-departments";
    public const string UsersAssignPositions = "Permission:users:assign-positions";

    public const string RolesView = "Permission:roles:view";
    public const string RolesCreate = "Permission:roles:create";
    public const string RolesUpdate = "Permission:roles:update";
    public const string RolesDelete = "Permission:roles:delete";
    public const string RolesAssignPermissions = "Permission:roles:assign-permissions";
    public const string RolesAssignMenus = "Permission:roles:assign-menus";

    public const string PermissionsView = "Permission:permissions:view";
    public const string PermissionsCreate = "Permission:permissions:create";
    public const string PermissionsUpdate = "Permission:permissions:update";

    public const string DepartmentsView = "Permission:departments:view";
    public const string DepartmentsAll = "Permission:departments:all";
    public const string DepartmentsCreate = "Permission:departments:create";
    public const string DepartmentsUpdate = "Permission:departments:update";
    public const string DepartmentsDelete = "Permission:departments:delete";

    public const string PositionsView = "Permission:positions:view";
    public const string PositionsCreate = "Permission:positions:create";
    public const string PositionsUpdate = "Permission:positions:update";
    public const string PositionsDelete = "Permission:positions:delete";

    public const string MenusView = "Permission:menus:view";
    public const string MenusAll = "Permission:menus:all";
    public const string MenusCreate = "Permission:menus:create";
    public const string MenusUpdate = "Permission:menus:update";
    public const string MenusDelete = "Permission:menus:delete";

    public const string AppsView = "Permission:apps:view";
    public const string AppsUpdate = "Permission:apps:update";
    public const string AppMembersView = "Permission:apps:members:view";
    public const string AppMembersUpdate = "Permission:apps:members:update";
    public const string AppRolesView = "Permission:apps:roles:view";
    public const string AppRolesUpdate = "Permission:apps:roles:update";
    public const string AppAdmin = "Permission:app:admin";
    public const string AppUser = "Permission:app:user";
    public const string DebugView = "Permission:debug:view";
    public const string DebugRun = "Permission:debug:run";
    public const string DebugManage = "Permission:debug:manage";

    public const string ProjectsView = "Permission:projects:view";
    public const string ProjectsCreate = "Permission:projects:create";
    public const string ProjectsUpdate = "Permission:projects:update";
    public const string ProjectsDelete = "Permission:projects:delete";
    public const string ProjectsAssignUsers = "Permission:projects:assign-users";
    public const string ProjectsAssignDepartments = "Permission:projects:assign-departments";
    public const string ProjectsAssignPositions = "Permission:projects:assign-positions";

    public const string AuditView = "Permission:audit:view";
    public const string AssetsView = "Permission:assets:view";
    public const string AssetsCreate = "Permission:assets:create";
    public const string AlertView = "Permission:alert:view";

    // 低代码 UI Builder（M01-M20）— 设计态权限
    public const string LowcodeAppView = "Permission:lowcode-app:view";
    public const string LowcodeAppCreate = "Permission:lowcode-app:create";
    public const string LowcodeAppUpdate = "Permission:lowcode-app:update";
    public const string LowcodeAppDelete = "Permission:lowcode-app:delete";
    public const string LowcodeAppPublish = "Permission:lowcode-app:publish";

    public const string ModelConfigView = "Permission:model-config:view";
    public const string ModelConfigCreate = "Permission:model-config:create";
    public const string ModelConfigUpdate = "Permission:model-config:update";
    public const string ModelConfigDelete = "Permission:model-config:delete";

    public const string AgentView = "Permission:agent:view";
    public const string AgentCreate = "Permission:agent:create";
    public const string AgentUpdate = "Permission:agent:update";
    public const string AgentDelete = "Permission:agent:delete";
    public const string ConversationView = "Permission:conversation:view";
    public const string ConversationCreate = "Permission:conversation:create";
    public const string ConversationDelete = "Permission:conversation:delete";
    public const string KnowledgeBaseView = "Permission:knowledge-base:view";
    public const string KnowledgeBaseCreate = "Permission:knowledge-base:create";
    public const string KnowledgeBaseUpdate = "Permission:knowledge-base:update";
    public const string KnowledgeBaseDelete = "Permission:knowledge-base:delete";
    public const string AiWorkflowView = "Permission:ai-workflow:view";
    public const string AiWorkflowCreate = "Permission:ai-workflow:create";
    public const string AiWorkflowUpdate = "Permission:ai-workflow:update";
    public const string AiWorkflowDelete = "Permission:ai-workflow:delete";
    public const string AiWorkflowExecute = "Permission:ai-workflow:execute";
    public const string AiWorkflowDebug = "Permission:ai-workflow:debug";
    public const string AiDatabaseView = "Permission:ai-database:view";
    public const string AiDatabaseCreate = "Permission:ai-database:create";
    public const string AiDatabaseUpdate = "Permission:ai-database:update";
    public const string AiDatabaseDelete = "Permission:ai-database:delete";
    public const string AiVariableView = "Permission:ai-variable:view";
    public const string AiVariableCreate = "Permission:ai-variable:create";
    public const string AiVariableUpdate = "Permission:ai-variable:update";
    public const string AiVariableDelete = "Permission:ai-variable:delete";
    public const string AiPluginView = "Permission:ai-plugin:view";
    public const string AiPluginCreate = "Permission:ai-plugin:create";
    public const string AiPluginUpdate = "Permission:ai-plugin:update";
    public const string AiPluginDelete = "Permission:ai-plugin:delete";
    public const string AiPluginPublish = "Permission:ai-plugin:publish";
    public const string AiPluginDebug = "Permission:ai-plugin:debug";
    public const string AiAppView = "Permission:ai-app:view";
    public const string AiAppCreate = "Permission:ai-app:create";
    public const string AiAppUpdate = "Permission:ai-app:update";
    public const string AiAppDelete = "Permission:ai-app:delete";
    public const string AiAppPublish = "Permission:ai-app:publish";
    public const string AiPromptView = "Permission:ai-prompt:view";
    public const string AiPromptCreate = "Permission:ai-prompt:create";
    public const string AiPromptUpdate = "Permission:ai-prompt:update";
    public const string AiPromptDelete = "Permission:ai-prompt:delete";
    public const string AiMarketplaceView = "Permission:ai-marketplace:view";
    public const string AiMarketplaceCreate = "Permission:ai-marketplace:create";
    public const string AiMarketplaceUpdate = "Permission:ai-marketplace:update";
    public const string AiMarketplaceDelete = "Permission:ai-marketplace:delete";
    public const string AiMarketplacePublish = "Permission:ai-marketplace:publish";
    public const string AiSearchView = "Permission:ai-search:view";
    public const string AiSearchUpdate = "Permission:ai-search:update";
    public const string AiAdminConfigView = "Permission:ai-admin-config:view";
    public const string AiAdminConfigUpdate = "Permission:ai-admin-config:update";
    public const string AiWorkspaceView = "Permission:ai-workspace:view";
    public const string AiWorkspaceUpdate = "Permission:ai-workspace:update";
    public const string AiDevopsView = "Permission:ai-devops:view";
    public const string AiShortcutView = "Permission:ai-shortcut:view";
    public const string AiShortcutManage = "Permission:ai-shortcut:manage";
    public const string PersonalAccessTokenView = "Permission:pat:view";
    public const string PersonalAccessTokenCreate = "Permission:pat:create";
    public const string PersonalAccessTokenUpdate = "Permission:pat:update";
    public const string PersonalAccessTokenDelete = "Permission:pat:delete";

    public const string ApprovalFlowView = "Permission:approval:flow:view";
    public const string ApprovalFlowManage = "Permission:approval:flow:manage";
    public const string ApprovalFlowCreate = "Permission:approval:flow:create";
    public const string ApprovalFlowUpdate = "Permission:approval:flow:update";
    public const string ApprovalFlowPublish = "Permission:approval:flow:publish";
    public const string ApprovalFlowDelete = "Permission:approval:flow:delete";
    public const string ApprovalFlowDisable = "Permission:approval:flow:disable";

    public const string VisualizationView = "Permission:visualization:view";
    public const string VisualizationProcessSave = "Permission:visualization:process:save";
    public const string VisualizationProcessUpdate = "Permission:visualization:process:update";
    public const string VisualizationProcessPublish = "Permission:visualization:process:publish";

    // Dict management
    public const string DictTypeView = "Permission:dict:type:view";
    public const string DictTypeCreate = "Permission:dict:type:create";
    public const string DictTypeUpdate = "Permission:dict:type:update";
    public const string DictTypeDelete = "Permission:dict:type:delete";
    public const string DictDataView = "Permission:dict:data:view";
    public const string DictDataCreate = "Permission:dict:data:create";
    public const string DictDataUpdate = "Permission:dict:data:update";
    public const string DictDataDelete = "Permission:dict:data:delete";

    // System config management
    public const string ConfigView = "Permission:config:view";
    public const string ConfigCreate = "Permission:config:create";
    public const string ConfigUpdate = "Permission:config:update";
    public const string ConfigDelete = "Permission:config:delete";

    // Login log
    public const string LoginLogView = "Permission:loginlog:view";
    public const string LoginLogDelete = "Permission:loginlog:delete";

    // Online users / sessions
    public const string OnlineUsersView = "Permission:online:view";
    public const string OnlineUsersForceLogout = "Permission:online:force-logout";

    // Monitor
    public const string MonitorView = "Permission:monitor:view";

    // Notification
    public const string NotificationView = "Permission:notification:view";
    public const string NotificationCreate = "Permission:notification:create";
    public const string NotificationUpdate = "Permission:notification:update";
    public const string NotificationDelete = "Permission:notification:delete";

    // Scheduled jobs
    public const string JobView = "Permission:job:view";
    public const string JobCreate = "Permission:job:create";
    public const string JobUpdate = "Permission:job:update";
    public const string JobDelete = "Permission:job:delete";
    public const string JobTrigger = "Permission:job:trigger";

    // Data scope
    public const string DataScopeManage = "Permission:datascope:manage";

    // File upload/download
    public const string FileUpload = "Permission:file:upload";
    public const string FileDownload = "Permission:file:download";
    public const string FileDelete = "Permission:file:delete";
}
