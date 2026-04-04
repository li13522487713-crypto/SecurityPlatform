CREATE INDEX IF NOT EXISTS "IX_RuntimeRoute_TenantIdValue_AppKey_PageKey"
ON "RuntimeRoute" ("TenantIdValue", "AppKey", "PageKey");

CREATE INDEX IF NOT EXISTS "IX_AppMember_TenantIdValue_AppId_UserId"
ON "AppMember" ("TenantIdValue", "AppId", "UserId");
