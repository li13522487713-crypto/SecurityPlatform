import { computed, onBeforeUnmount, ref } from "vue";
import Uppy from "@uppy/core";
import Tus from "@uppy/tus";
import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@/types/api";
import { getAccessToken, getAntiforgeryToken, getTenantId, setAntiforgeryToken } from "@/utils/auth";

export type TusUploadStatus = "idle" | "uploading" | "paused" | "completed" | "error" | "cancelled";

export interface TusUploadProgress {
  sessionId: number;
  fileId?: number;
  uploadedBytes: number;
  totalBytes: number;
  progressPercent: number;
  status: TusUploadStatus;
  errorMessage?: string;
}

export function useTusUpload(defaultChunkSize = 2 * 1024 * 1024) {
  const sessionId = ref<number | null>(null);
  const uploadedBytes = ref(0);
  const fileId = ref<number>();
  const totalBytes = ref(0);
  const status = ref<TusUploadStatus>("idle");
  const errorMessage = ref<string>();
  const currentUppyFileId = ref<string>();

  const uppy = new Uppy({
    autoProceed: false,
    restrictions: {
      maxNumberOfFiles: 1
    }
  });

  uppy.use(Tus, {
    endpoint: "/api/v1/files/tus",
    chunkSize: defaultChunkSize,
    retryDelays: [0, 1000, 3000, 5000],
    uploadDataDuringCreation: true,
    removeFingerprintOnSuccess: false,
    async onBeforeRequest(req) {
      const headers = await buildSecurityHeaders();
      headers.forEach((value, key) => req.setHeader(key, value));
      req.setHeader("Tus-Resumable", "1.0.0");
    },
    onAfterResponse(_, res) {
      const uploadFileId = res.getHeader("Upload-File-Id");
      if (uploadFileId) {
        const parsed = Number(uploadFileId);
        if (Number.isFinite(parsed) && parsed > 0) {
          fileId.value = parsed;
        }
      }
    }
  });

  uppy.on("upload-progress", (_, progress) => {
    uploadedBytes.value = progress.bytesUploaded;
    totalBytes.value = progress.bytesTotal ?? totalBytes.value;
  });

  uppy.on("upload-success", (_, response) => {
    const uploadUrl = response.uploadURL ?? "";
    const resolvedSessionId = resolveSessionId(uploadUrl);
    if (resolvedSessionId) {
      sessionId.value = resolvedSessionId;
    }
    if (status.value !== "cancelled" && status.value !== "paused") {
      status.value = "completed";
    }
  });

  uppy.on("upload-error", (_, error) => {
    status.value = "error";
    errorMessage.value = error instanceof Error ? error.message : "Tus 上传失败";
  });

  const progressPercent = computed(() => {
    if (totalBytes.value <= 0) {
      return 0;
    }
    return Math.min(100, Math.round((uploadedBytes.value / totalBytes.value) * 100));
  });

  function reset() {
    uppy.cancelAll();
    uppy.clear();
    sessionId.value = null;
    uploadedBytes.value = 0;
    fileId.value = undefined;
    totalBytes.value = 0;
    status.value = "idle";
    errorMessage.value = undefined;
    currentUppyFileId.value = undefined;
  }

  async function start(file: File) {
    errorMessage.value = undefined;
    status.value = "uploading";
    uploadedBytes.value = 0;
    totalBytes.value = file.size;
    if (currentUppyFileId.value) {
      uppy.removeFile(currentUppyFileId.value);
      currentUppyFileId.value = undefined;
    }

    const addedFileId = uppy.addFile({
      name: file.name,
      type: file.type,
      data: file,
      meta: {
        filename: file.name,
        contentType: file.type || "application/octet-stream"
      }
    });
    currentUppyFileId.value = addedFileId;

    try {
      const result = await uppy.upload();
      const failedUploads = result?.failed ?? [];
      if (failedUploads.length > 0) {
        throw failedUploads[0]?.error ?? new Error("Tus 上传失败");
      }
    } catch (error) {
      status.value = "error";
      errorMessage.value = error instanceof Error ? error.message : "Tus 上传失败";
      throw error;
    }
  }

  function pause() {
    if (status.value === "uploading" && currentUppyFileId.value) {
      uppy.pauseResume(currentUppyFileId.value);
      status.value = "paused";
    }
  }

  function resume() {
    if (status.value === "paused" && currentUppyFileId.value) {
      uppy.pauseResume(currentUppyFileId.value);
      status.value = "uploading";
    }
  }

  function cancel() {
    uppy.cancelAll();
    uppy.clear();
    status.value = "cancelled";
    currentUppyFileId.value = undefined;
  }

  function snapshot(): TusUploadProgress {
    return {
      sessionId: sessionId.value ?? 0,
      fileId: fileId.value,
      uploadedBytes: uploadedBytes.value,
      totalBytes: totalBytes.value,
      progressPercent: progressPercent.value,
      status: status.value,
      errorMessage: errorMessage.value
    };
  }

  async function buildSecurityHeaders() {
    const headers = new Map<string, string>();
    const accessToken = getAccessToken();
    const tenantId = getTenantId();
    const csrfToken = await ensureAntiforgeryToken();

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

  async function ensureAntiforgeryToken(): Promise<string | null> {
    const cached = getAntiforgeryToken();
    if (cached) {
      return cached;
    }

    const response = await requestApi<ApiResponse<{ token: string }>>("/secure/antiforgery", {
      method: "GET"
    });
    const token = response.data?.token ?? null;
    if (token) {
      setAntiforgeryToken(token);
    }
    return token;
  }

  function resolveSessionId(uploadUrl: string): number | null {
    if (!uploadUrl) {
      return null;
    }
    try {
      const normalized = uploadUrl.split("?")[0];
      const value = normalized.substring(normalized.lastIndexOf("/") + 1);
      const parsed = Number(value);
      return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
    } catch {
      return null;
    }
  }

  onBeforeUnmount(() => {
    uppy.cancelAll();
    uppy.clear();
    uppy.destroy();
  });

  return {
    sessionId,
    uploadedBytes,
    fileId,
    totalBytes,
    progressPercent,
    status,
    errorMessage,
    reset,
    start,
    pause,
    resume,
    cancel,
    snapshot
  };
}
