CREATE INDEX IF NOT EXISTS "IX_AppRolePermission_TenantIdValue_AppId_RoleId"
ON "AppRolePermission" ("TenantIdValue", "AppId", "RoleId");

CREATE INDEX IF NOT EXISTS "IX_AppPermission_TenantIdValue_AppId_Code"
ON "AppPermission" ("TenantIdValue", "AppId", "Code");
