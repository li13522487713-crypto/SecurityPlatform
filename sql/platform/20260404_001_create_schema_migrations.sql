CREATE TABLE IF NOT EXISTS "SchemaMigrations" (
    "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
    "Scope" TEXT NOT NULL,
    "TargetKey" TEXT NOT NULL,
    "ScriptName" TEXT NOT NULL,
    "ChecksumSha256" TEXT NOT NULL,
    "ExecutedAt" TEXT NOT NULL,
    "ExecutedBy" TEXT NOT NULL
);
