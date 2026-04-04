-- AppRelease 新增构件与安装相关字段
-- 要求：SQLite ALTER TABLE 逐列添加

ALTER TABLE AppRelease ADD COLUMN ArtifactId TEXT NULL;
ALTER TABLE AppRelease ADD COLUMN Checksum TEXT NULL;
ALTER TABLE AppRelease ADD COLUMN InstallSpec TEXT NULL;
ALTER TABLE AppRelease ADD COLUMN RollbackMetadata TEXT NULL;
