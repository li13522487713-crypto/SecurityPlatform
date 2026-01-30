namespace Atlas.WebApi.Authorization;

public static class PermissionPolicies
{
    public const string PolicyPrefix = "Permission:";

    public static string For(string permissionCode)
    {
        return $"{PolicyPrefix}{permissionCode}";
    }

    public const string SystemAdmin = "Permission:system:admin";
    public const string WorkflowDesign = "Permission:workflow:design";

    public const string UsersView = "Permission:users:view";
    public const string UsersCreate = "Permission:users:create";
    public const string UsersUpdate = "Permission:users:update";
    public const string UsersAssignRoles = "Permission:users:assign-roles";
    public const string UsersAssignDepartments = "Permission:users:assign-departments";

    public const string RolesView = "Permission:roles:view";
    public const string RolesCreate = "Permission:roles:create";
    public const string RolesUpdate = "Permission:roles:update";
    public const string RolesAssignPermissions = "Permission:roles:assign-permissions";
    public const string RolesAssignMenus = "Permission:roles:assign-menus";

    public const string PermissionsView = "Permission:permissions:view";
    public const string PermissionsCreate = "Permission:permissions:create";
    public const string PermissionsUpdate = "Permission:permissions:update";

    public const string DepartmentsView = "Permission:departments:view";
    public const string DepartmentsAll = "Permission:departments:all";
    public const string DepartmentsCreate = "Permission:departments:create";
    public const string DepartmentsUpdate = "Permission:departments:update";

    public const string MenusView = "Permission:menus:view";
    public const string MenusAll = "Permission:menus:all";
    public const string MenusCreate = "Permission:menus:create";
    public const string MenusUpdate = "Permission:menus:update";

    public const string AuditView = "Permission:audit:view";
    public const string AssetsCreate = "Permission:assets:create";

    public const string ApprovalFlowCreate = "Permission:approval:flow:create";
    public const string ApprovalFlowUpdate = "Permission:approval:flow:update";
    public const string ApprovalFlowPublish = "Permission:approval:flow:publish";
    public const string ApprovalFlowDelete = "Permission:approval:flow:delete";
    public const string ApprovalFlowDisable = "Permission:approval:flow:disable";

    public const string VisualizationProcessSave = "Permission:visualization:process:save";
    public const string VisualizationProcessUpdate = "Permission:visualization:process:update";
    public const string VisualizationProcessPublish = "Permission:visualization:process:publish";
}
