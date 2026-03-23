import { computed, ref } from "vue";
import { API_BASE } from "@/services/api-core";
import { getFileInfoResource } from "@/services/api-files";
import { getAccessToken, getTenantId } from "@/utils/auth";

export type RangeDownloadStatus = "idle" | "downloading" | "paused" | "completed" | "cancelled" | "error";

export function useRangeDownload(defaultChunkSize = 2 * 1024 * 1024) {
  const fileName = ref("");
  const totalBytes = ref(0);
  const downloadedBytes = ref(0);
  const status = ref<RangeDownloadStatus>("idle");
  const errorMessage = ref<string>();
  const currentFileId = ref<number>();

  const paused = ref(false);
  const cancelled = ref(false);
  const chunks: Blob[] = [];

  const progressPercent = computed(() => {
    if (totalBytes.value <= 0) {
      return 0;
    }
    return Math.min(100, Math.round((downloadedBytes.value / totalBytes.value) * 100));
  });

  async function start(fileId: number, chunkSize = defaultChunkSize): Promise<void> {
    currentFileId.value = fileId;
    paused.value = false;
    cancelled.value = false;
    errorMessage.value = undefined;
    status.value = "downloading";

    if (totalBytes.value <= 0 || !fileName.value) {
      const info = await getFileInfoResource(fileId);
      fileName.value = info.originalName;
      totalBytes.value = info.sizeBytes;
      const savedOffset = Number(localStorage.getItem(progressStorageKey(fileId)) ?? "0");
      if (savedOffset > 0 && savedOffset < info.sizeBytes) {
        downloadedBytes.value = savedOffset;
      }
    }

    try {
      while (downloadedBytes.value < totalBytes.value) {
        if (cancelled.value) {
          status.value = "cancelled";
          return;
        }
        if (paused.value) {
          status.value = "paused";
          return;
        }

        const nextEnd = Math.min(totalBytes.value - 1, downloadedBytes.value + chunkSize - 1);
        const blob = await fetchRangeChunk(fileId, downloadedBytes.value, nextEnd);
        chunks.push(blob);
        downloadedBytes.value = nextEnd + 1;
        localStorage.setItem(progressStorageKey(fileId), `${downloadedBytes.value}`);
      }

      const merged = new Blob(chunks, { type: "application/octet-stream" });
      triggerBrowserDownload(merged, fileName.value || `file-${fileId}.bin`);
      localStorage.removeItem(progressStorageKey(fileId));
      resetTransientState();
      status.value = "completed";
    } catch (error) {
      status.value = "error";
      errorMessage.value = error instanceof Error ? error.message : "下载失败";
      throw error;
    }
  }

  function pause() {
    if (status.value === "downloading") {
      paused.value = true;
    }
  }

  function resume(chunkSize = defaultChunkSize) {
    if (status.value === "paused" && currentFileId.value) {
      return start(currentFileId.value, chunkSize);
    }
    return Promise.resolve();
  }

  function cancel() {
    cancelled.value = true;
    paused.value = false;
    status.value = "cancelled";
    if (currentFileId.value) {
      localStorage.removeItem(progressStorageKey(currentFileId.value));
    }
    resetAll();
  }

  function resetAll() {
    fileName.value = "";
    totalBytes.value = 0;
    downloadedBytes.value = 0;
    currentFileId.value = undefined;
    resetTransientState();
  }

  function resetTransientState() {
    paused.value = false;
    cancelled.value = false;
    chunks.splice(0, chunks.length);
  }

  return {
    fileName,
    totalBytes,
    downloadedBytes,
    progressPercent,
    status,
    errorMessage,
    start,
    pause,
    resume,
    cancel,
    resetAll
  };
}

async function fetchRangeChunk(fileId: number, start: number, end: number): Promise<Blob> {
  const headers = new Headers();
  const token = getAccessToken();
  const tenantId = getTenantId();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }
  if (tenantId) {
    headers.set("X-Tenant-Id", tenantId);
  }
  headers.set("Range", `bytes=${start}-${end}`);

  const response = await fetch(`${API_BASE}/files/${fileId}`, {
    method: "GET",
    headers,
    credentials: "include"
  });
  if (response.status !== 206 && response.status !== 200) {
    throw new Error("下载失败");
  }
  return response.blob();
}

function progressStorageKey(fileId: number): string {
  return `atlas_file_download_${fileId}`;
}

function triggerBrowserDownload(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = name;
  anchor.click();
  URL.revokeObjectURL(url);
}
