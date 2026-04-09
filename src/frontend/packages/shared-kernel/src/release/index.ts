export type DraftStatus = "draft" | "published" | "archived";

export interface VersionInfo {
  versionId: string;
  versionNo: string;
  createdAt: string;
  createdBy?: string;
}

export interface ReleaseState {
  status: DraftStatus;
  currentVersion?: VersionInfo;
  latestPublishedVersion?: VersionInfo;
}

export type PublishMode = "full" | "incremental" | "dry-run";
