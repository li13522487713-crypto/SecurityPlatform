-- AppConfig table for SQLite
-- Generated on 2026-01-31

CREATE TABLE IF NOT EXISTS AppConfig (
    Id INTEGER PRIMARY KEY,
    TenantIdValue TEXT NOT NULL,
    AppId TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    IsActive INTEGER NOT NULL,
    EnableProjectScope INTEGER NOT NULL,
    SortOrder INTEGER NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS UX_AppConfig_Tenant_AppId
    ON AppConfig (TenantIdValue, AppId);
