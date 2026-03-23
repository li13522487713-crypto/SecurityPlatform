import {
  API_BASE,
  requestApi,
  requestApiBlob,
} from "@/services/api-core";
import type {
  ApiResponse,
  AttachmentBindingDto,
  AttachmentBindRequest,
  AttachmentUnbindRequest,
  FileChunkUploadCompleteRequest,
  FileChunkUploadInitRequest,
  FileChunkUploadInitResult,
  FileRecordDto,
  FileSignedUrlResult,
  FileInstantCheckResult,
  FileUploadResult,
  FileUploadSessionProgressDto,
  FileVersionHistoryItemDto,
  FileTusStatusResult
} from "@/types/api";
import { getAccessToken, getAntiforgeryToken, getTenantId, setAntiforgeryToken } from "@/utils/auth";

export async function uploadFileResource(file: File): Promise<FileUploadResult> {
  const formData = new FormData();
  formData.append("file", file);
  const response = await requestApi<ApiResponse<FileUploadResult>>("/files", {
    method: "POST",
    body: formData
  });
  if (!response.data) {
    throw new Error(response.message || "上传失败");
  }
  return response.data;
}

export async function deleteFileResource(id: string | number): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/files/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function getFileInfoResource(id: string | number): Promise<FileRecordDto> {
  const response = await requestApi<ApiResponse<FileRecordDto>>(`/files/${id}/info`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getFileSignedUrl(id: string | number, expiresInSeconds = 600): Promise<FileSignedUrlResult> {
  const response = await requestApi<ApiResponse<FileSignedUrlResult>>(`/files/${id}/signed-url?expiresInSeconds=${expiresInSeconds}`);
  if (!response.data) {
    throw new Error(response.message || "签名下载地址生成失败");
  }
  return response.data;
}

export async function checkInstantUpload(sha256: string, sizeBytes: number): Promise<FileInstantCheckResult> {
  const response = await requestApi<ApiResponse<FileInstantCheckResult>>(
    `/files/instant-check?sha256=${encodeURIComponent(sha256)}&sizeBytes=${sizeBytes}`
  );
  if (!response.data) {
    throw new Error(response.message || "秒传校验失败");
  }
  return response.data;
}

export async function downloadFileBlob(id: string | number): Promise<Blob> {
  return requestApiBlob(`/files/${id}`, { method: "GET" });
}

export async function initChunkUpload(request: FileChunkUploadInitRequest): Promise<FileChunkUploadInitResult> {
  const response = await requestApi<ApiResponse<FileChunkUploadInitResult>>("/files/upload/init", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "初始化分片上传失败");
  }
  return response.data;
}

export async function uploadChunkPart(sessionId: number, partNumber: number, chunk: Blob): Promise<void> {
  const formData = new FormData();
  formData.append("file", chunk, `part-${partNumber}.chunk`);
  const response = await requestApi<ApiResponse<object>>(`/files/upload/${sessionId}/part/${partNumber}`, {
    method: "POST",
    body: formData
  });
  if (!response.success) {
    throw new Error(response.message || "上传分片失败");
  }
}

export async function completeChunkUpload(
  sessionId: number,
  request: FileChunkUploadCompleteRequest
): Promise<FileUploadResult> {
  const response = await requestApi<ApiResponse<FileUploadResult>>(`/files/upload/${sessionId}/complete`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "完成分片上传失败");
  }
  return response.data;
}

export async function getChunkUploadProgress(sessionId: number): Promise<FileUploadSessionProgressDto> {
  const response = await requestApi<ApiResponse<FileUploadSessionProgressDto>>(`/files/upload/${sessionId}/progress`);
  if (!response.data) {
    throw new Error(response.message || "查询分片上传进度失败");
  }
  return response.data;
}

export async function createTusUploadSession(file: File): Promise<FileTusStatusResult> {
  const csrfToken = await ensureAntiforgeryToken();
  const headers = await buildTusHeaders(csrfToken);
  headers.set("Tus-Resumable", "1.0.0");
  headers.set("Upload-Length", `${file.size}`);
  headers.set("Upload-Metadata", buildTusMetadata(file));

  const response = await fetch(resolveApiPath("/files/tus"), {
    method: "POST",
    headers,
    credentials: "include"
  });

  if (response.status !== 201) {
    throw new Error(await readError(response, "创建 Tus 上传会话失败"));
  }

  const location = response.headers.get("Location");
  const uploadOffset = Number(response.headers.get("Upload-Offset") ?? 0);
  const uploadLength = Number(response.headers.get("Upload-Length") ?? file.size);
  const expiresAt = response.headers.get("Upload-Expires") ?? new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString();
  const sessionId = resolveTusSessionId(location);
  return {
    sessionId,
    uploadOffset,
    uploadLength,
    completed: uploadOffset >= uploadLength,
    expiresAt
  };
}

export async function getTusUploadStatus(sessionId: number): Promise<FileTusStatusResult> {
  const headers = await buildTusHeaders();
  headers.set("Tus-Resumable", "1.0.0");
  const response = await fetch(resolveApiPath(`/files/tus/${sessionId}`), {
    method: "HEAD",
    headers,
    credentials: "include"
  });

  if (response.status !== 204) {
    throw new Error(await readError(response, "查询 Tus 上传状态失败"));
  }

  const uploadOffset = Number(response.headers.get("Upload-Offset") ?? 0);
  const uploadLength = Number(response.headers.get("Upload-Length") ?? 0);
  const expiresAt = response.headers.get("Upload-Expires") ?? new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString();
  return {
    sessionId,
    uploadOffset,
    uploadLength,
    completed: uploadLength > 0 && uploadOffset >= uploadLength,
    expiresAt
  };
}

export async function uploadTusChunk(
  sessionId: number,
  chunk: Blob,
  uploadOffset: number
): Promise<{ uploadOffset: number; fileId?: number }> {
  const csrfToken = await ensureAntiforgeryToken();
  const headers = await buildTusHeaders(csrfToken);
  headers.set("Tus-Resumable", "1.0.0");
  headers.set("Upload-Offset", `${uploadOffset}`);
  headers.set("Content-Type", "application/offset+octet-stream");

  const response = await fetch(resolveApiPath(`/files/tus/${sessionId}`), {
    method: "PATCH",
    headers,
    body: chunk,
    credentials: "include"
  });

  if (response.status !== 204) {
    throw new Error(await readError(response, "Tus 分片上传失败"));
  }

  return {
    uploadOffset: Number(response.headers.get("Upload-Offset") ?? uploadOffset),
    fileId: response.headers.has("Upload-File-Id")
      ? Number(response.headers.get("Upload-File-Id"))
      : undefined
  };
}

function resolveApiPath(path: string): string {
  return `${API_BASE}${path}`;
}

function resolveTusSessionId(location: string | null): number {
  if (!location) {
    throw new Error("Tus 会话创建成功但响应缺少 Location");
  }

  const normalized = location.split("?")[0];
  const last = normalized.substring(normalized.lastIndexOf("/") + 1);
  const sessionId = Number(last);
  if (!Number.isFinite(sessionId) || sessionId <= 0) {
    throw new Error("Tus 会话标识无效");
  }
  return sessionId;
}

function buildTusMetadata(file: File): string {
  const filename = toBase64(file.name);
  const contentType = toBase64(file.type || "application/octet-stream");
  return `filename ${filename},contentType ${contentType}`;
}

async function buildTusHeaders(csrfToken?: string): Promise<Headers> {
  const headers = new Headers();
  const accessToken = getAccessToken();
  const tenantId = getTenantId();
  if (accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  }
  if (tenantId) {
    headers.set("X-Tenant-Id", tenantId);
  }
  if (csrfToken) {
    headers.set("X-CSRF-TOKEN", csrfToken);
  }
  return headers;
}

async function ensureAntiforgeryToken(): Promise<string> {
  const cached = getAntiforgeryToken();
  if (cached) {
    return cached;
  }

  const response = await requestApi<ApiResponse<{ token: string }>>("/secure/antiforgery", {
    method: "GET"
  });
  const token = response.data?.token;
  if (!token) {
    throw new Error(response.message || "获取 CSRF Token 失败");
  }
  setAntiforgeryToken(token);
  return token;
}

async function readError(response: Response, fallbackMessage: string): Promise<string> {
  const text = await response.text();
  if (!text) {
    return fallbackMessage;
  }

  try {
    const payload = JSON.parse(text) as { message?: string };
    return payload.message || fallbackMessage;
  } catch {
    return fallbackMessage;
  }
}

function toBase64(value: string): string {
  const bytes = new TextEncoder().encode(value);
  let binary = "";
  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte);
  });
  return window.btoa(binary);
}

// ---- 版本历史 ----

export async function getFileVersionHistory(id: number): Promise<FileVersionHistoryItemDto[]> {
  const response = await requestApi<ApiResponse<FileVersionHistoryItemDto[]>>(`/files/${id}/versions`);
  if (!response.data) {
    throw new Error(response.message || "查询版本历史失败");
  }
  return response.data;
}

// ---- 附件绑定 ----

export async function listAttachments(
  entityType: string,
  entityId: number,
  fieldKey?: string
): Promise<AttachmentBindingDto[]> {
  const params = new URLSearchParams();
  if (fieldKey) {
    params.set("fieldKey", fieldKey);
  }
  const qs = params.toString() ? `?${params.toString()}` : "";
  const response = await requestApi<ApiResponse<AttachmentBindingDto[]>>(
    `/files/attachments/${encodeURIComponent(entityType)}/${entityId}${qs}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询附件绑定失败");
  }
  return response.data;
}

export async function bindAttachment(request: AttachmentBindRequest): Promise<AttachmentBindingDto> {
  const response = await requestApi<ApiResponse<AttachmentBindingDto>>("/files/bind", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "绑定附件失败");
  }
  return response.data;
}

export async function unbindAttachment(request: AttachmentUnbindRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>("/files/unbind", {
    method: "DELETE",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "解绑附件失败");
  }
}
