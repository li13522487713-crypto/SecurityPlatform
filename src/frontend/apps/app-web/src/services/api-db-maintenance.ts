import { requestApi } from "./api-core";
import type { ApiResponse } from "@atlas/shared-react-core/types";

export interface DatabaseConnectionStatus {
  connected: boolean;
  message: string;
  latencyMs: number | null;
}

export interface DatabaseInfo {
  dbType: string;
  connectionString: string;
  fileSizeBytes: number | null;
  journalMode: string | null;
  pageCount: number | null;
  pageSize: number | null;
}

export interface BackupFileInfo {
  fileName: string;
  sizeBytes: number;
  createdAt: string;
  sha256: string | null;
}

export interface BackupResult {
  success: boolean;
  fileName: string | null;
  message: string | null;
  sizeBytes: number | null;
}

const BASE = "/database-maintenance";

export function testConnection(): Promise<ApiResponse<DatabaseConnectionStatus>> {
  return requestApi<ApiResponse<DatabaseConnectionStatus>>(`${BASE}/test-connection`);
}

export function getDatabaseInfo(): Promise<ApiResponse<DatabaseInfo>> {
  return requestApi<ApiResponse<DatabaseInfo>>(`${BASE}/info`);
}

export function listBackups(): Promise<ApiResponse<BackupFileInfo[]>> {
  return requestApi<ApiResponse<BackupFileInfo[]>>(`${BASE}/backups`);
}

export function backupNow(): Promise<ApiResponse<BackupResult>> {
  return requestApi<ApiResponse<BackupResult>>(`${BASE}/backups`, { method: "POST" });
}
