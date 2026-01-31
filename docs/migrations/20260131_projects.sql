-- Project tables for SQLite
-- Generated on 2026-01-31

CREATE TABLE IF NOT EXISTS Project (
    Id INTEGER PRIMARY KEY,
    TenantIdValue TEXT NOT NULL,
    Code TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    IsActive INTEGER NOT NULL,
    SortOrder INTEGER NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS UX_Project_Tenant_Code
    ON Project (TenantIdValue, Code);

CREATE TABLE IF NOT EXISTS ProjectUser (
    Id INTEGER PRIMARY KEY,
    TenantIdValue TEXT NOT NULL,
    ProjectId INTEGER NOT NULL,
    UserId INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_ProjectUser_Project_User
    ON ProjectUser (TenantIdValue, ProjectId, UserId);

CREATE TABLE IF NOT EXISTS ProjectDepartment (
    Id INTEGER PRIMARY KEY,
    TenantIdValue TEXT NOT NULL,
    ProjectId INTEGER NOT NULL,
    DepartmentId INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_ProjectDepartment_Project_Department
    ON ProjectDepartment (TenantIdValue, ProjectId, DepartmentId);

CREATE TABLE IF NOT EXISTS ProjectPosition (
    Id INTEGER PRIMARY KEY,
    TenantIdValue TEXT NOT NULL,
    ProjectId INTEGER NOT NULL,
    PositionId INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_ProjectPosition_Project_Position
    ON ProjectPosition (TenantIdValue, ProjectId, PositionId);
