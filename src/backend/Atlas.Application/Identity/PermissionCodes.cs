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

    public const string AuditView = "audit:view";
    public const string AssetsCreate = "assets:create";

    public const string ApprovalFlowCreate = "approval:flow:create";
    public const string ApprovalFlowUpdate = "approval:flow:update";
    public const string ApprovalFlowPublish = "approval:flow:publish";
    public const string ApprovalFlowDelete = "approval:flow:delete";
    public const string ApprovalFlowDisable = "approval:flow:disable";

    public const string VisualizationProcessSave = "visualization:process:save";
    public const string VisualizationProcessUpdate = "visualization:process:update";
    public const string VisualizationProcessPublish = "visualization:process:publish";
}
