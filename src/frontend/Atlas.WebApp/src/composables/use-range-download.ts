import { computed, ref } from "vue";
import { API_BASE } from "@/services/api-core";
import { getFileInfoResource } from "@/services/api-files";
import { getAccessToken, getTenantId } from "@/utils/auth";

export type RangeDownloadStatus = "idle" | "downloading" | "paused" | "completed" | "cancelled" | "error";

export interface RangeDownloadOptions {
  /** 每个分片字节数，默认 2MB */
  chunkSize?: number;
  /** 并发分片请求数（1-4），默认 1（顺序模式） */
  concurrency?: number;
  /** 下载速度上限（bytes/sec），0 表示不限速 */
  maxBytesPerSecond?: number;
}

export function useRangeDownload(options: RangeDownloadOptions = {}) {
  const {
    chunkSize: defaultChunkSize = 2 * 1024 * 1024,
    concurrency: defaultConcurrency = 1,
    maxBytesPerSecond: defaultMaxBps = 0
  } = options;

  const fileName = ref("");
  const totalBytes = ref(0);
  const downloadedBytes = ref(0);
  const status = ref<RangeDownloadStatus>("idle");
  const errorMessage = ref<string>();
  const currentFileId = ref<number>();
  // bytes/sec，基于最近 1 秒内已下载字节的滑动窗口
  const currentSpeed = ref(0);

  const paused = ref(false);
  const cancelled = ref(false);

  // 用于计算实时速度的滑动窗口（[timestamp, bytes]）
  const speedWindow: Array<{ ts: number; bytes: number }> = [];

  const progressPercent = computed(() => {
    if (totalBytes.value <= 0) return 0;
    return Math.min(100, Math.round((downloadedBytes.value / totalBytes.value) * 100));
  });

  function recordSpeed(bytes: number) {
    const now = Date.now();
    speedWindow.push({ ts: now, bytes });
    // 保留最近 3 秒的记录
    const cutoff = now - 3000;
    while (speedWindow.length > 0 && speedWindow[0].ts < cutoff) {
      speedWindow.shift();
    }
    const windowMs = speedWindow.length > 1
      ? speedWindow[speedWindow.length - 1].ts - speedWindow[0].ts
      : 1000;
    const totalInWindow = speedWindow.reduce((s, e) => s + e.bytes, 0);
    currentSpeed.value = windowMs > 0 ? Math.round((totalInWindow / windowMs) * 1000) : 0;
  }

  async function start(
    fileId: number,
    chunkSize = defaultChunkSize,
    concurrency = defaultConcurrency,
    maxBps = defaultMaxBps
  ): Promise<void> {
    currentFileId.value = fileId;
    paused.value = false;
    cancelled.value = false;
    errorMessage.value = undefined;
    status.value = "downloading";
    speedWindow.splice(0, speedWindow.length);
    currentSpeed.value = 0;

    if (totalBytes.value <= 0 || !fileName.value) {
      const info = await getFileInfoResource(fileId);
      fileName.value = info.originalName;
      totalBytes.value = info.sizeBytes;
      const savedOffset = Number(localStorage.getItem(progressStorageKey(fileId)) ?? "0");
      if (savedOffset > 0 && savedOffset < info.sizeBytes) {
        downloadedBytes.value = savedOffset;
      }
    }

    const clampedConcurrency = Math.max(1, Math.min(4, Math.round(concurrency)));

    try {
      // 生成从当前偏移开始的所有分片描述符
      const ranges = buildRangeDescriptors(downloadedBytes.value, totalBytes.value, chunkSize);

      if (clampedConcurrency <= 1) {
        // 顺序模式（原有行为）
        await downloadSequential(fileId, ranges, maxBps);
      } else {
        // 并发滑动窗口模式
        await downloadConcurrent(fileId, ranges, clampedConcurrency, maxBps);
      }

      if (cancelled.value) {
        status.value = "cancelled";
        return;
      }
      if (paused.value) {
        status.value = "paused";
        return;
      }

      currentSpeed.value = 0;
      localStorage.removeItem(progressStorageKey(fileId));
      resetTransientState();
      status.value = "completed";
    } catch (error) {
      currentSpeed.value = 0;
      status.value = "error";
      errorMessage.value = error instanceof Error ? error.message : "下载失败";
      throw error;
    }
  }

  /** 顺序模式：逐片拉取，支持暂停/取消 */
  async function downloadSequential(
    fileId: number,
    ranges: RangeDescriptor[],
    maxBps: number
  ): Promise<void> {
    const resultBlobs: Blob[] = new Array(ranges.length);

    for (const desc of ranges) {
      if (cancelled.value || paused.value) return;

      const chunkStart = Date.now();
      const blob = await fetchRangeChunk(fileId, desc.start, desc.end);
      resultBlobs[desc.index] = blob;

      await applyRateLimit(blob.size, Date.now() - chunkStart, maxBps);

      downloadedBytes.value = desc.end + 1;
      recordSpeed(blob.size);
      localStorage.setItem(progressStorageKey(fileId), `${downloadedBytes.value}`);
    }

    if (!cancelled.value && !paused.value) {
      triggerBrowserDownload(
        new Blob(resultBlobs, { type: "application/octet-stream" }),
        fileName.value || `file-${fileId}.bin`
      );
    }
  }

  /** 并发滑动窗口模式：维护最多 concurrency 个并发请求 */
  async function downloadConcurrent(
    fileId: number,
    ranges: RangeDescriptor[],
    concurrency: number,
    maxBps: number
  ): Promise<void> {
    const resultBlobs: Blob[] = new Array(ranges.length);
    let completedBytes = downloadedBytes.value;

    // 追踪飞行中的 Promise（每个 Promise resolve 后从 Set 移除自身）
    const inFlight = new Set<Promise<void>>();

    for (const desc of ranges) {
      if (cancelled.value || paused.value) break;

      // 等待并发槽位
      if (inFlight.size >= concurrency) {
        await Promise.race(inFlight);
      }
      if (cancelled.value || paused.value) break;

      const chunkStart = Date.now();
      const p: Promise<void> = fetchRangeChunk(fileId, desc.start, desc.end).then(async (blob) => {
        inFlight.delete(p);
        resultBlobs[desc.index] = blob;
        completedBytes += blob.size;
        downloadedBytes.value = completedBytes;
        recordSpeed(blob.size);
        localStorage.setItem(progressStorageKey(fileId), `${completedBytes}`);
        await applyRateLimit(blob.size, Date.now() - chunkStart, maxBps);
      });
      inFlight.add(p);
    }

    // 等待剩余的飞行请求完成
    await Promise.all(inFlight);

    if (!cancelled.value && !paused.value) {
      triggerBrowserDownload(
        new Blob(resultBlobs, { type: "application/octet-stream" }),
        fileName.value || `file-${fileId}.bin`
      );
    }
  }

  function pause() {
    if (status.value === "downloading") {
      paused.value = true;
    }
  }

  function resume(
    chunkSize = defaultChunkSize,
    concurrency = defaultConcurrency,
    maxBps = defaultMaxBps
  ) {
    if (status.value === "paused" && currentFileId.value) {
      return start(currentFileId.value, chunkSize, concurrency, maxBps);
    }
    return Promise.resolve();
  }

  function cancel() {
    cancelled.value = true;
    paused.value = false;
    status.value = "cancelled";
    currentSpeed.value = 0;
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
    currentSpeed.value = 0;
    resetTransientState();
  }

  function resetTransientState() {
    paused.value = false;
    cancelled.value = false;
    speedWindow.splice(0, speedWindow.length);
  }

  return {
    fileName,
    totalBytes,
    downloadedBytes,
    progressPercent,
    status,
    errorMessage,
    currentSpeed,
    start,
    pause,
    resume,
    cancel,
    resetAll
  };
}

// ---- 内部工具 ----

interface RangeDescriptor {
  index: number;
  start: number;
  end: number;
}

/** 从 startOffset 开始生成所有分片描述符 */
function buildRangeDescriptors(
  startOffset: number,
  total: number,
  chunkSize: number
): RangeDescriptor[] {
  const ranges: RangeDescriptor[] = [];
  let index = 0;
  let pos = startOffset;
  while (pos < total) {
    const end = Math.min(total - 1, pos + chunkSize - 1);
    ranges.push({ index, start: pos, end });
    pos = end + 1;
    index++;
  }
  return ranges;
}

async function fetchRangeChunk(fileId: number, start: number, end: number): Promise<Blob> {
  const headers = new Headers();
  const token = getAccessToken();
  const tenantId = getTenantId();
  if (token) headers.set("Authorization", `Bearer ${token}`);
  if (tenantId) headers.set("X-Tenant-Id", tenantId);
  headers.set("Range", `bytes=${start}-${end}`);

  const response = await fetch(`${API_BASE}/files/${fileId}`, {
    method: "GET",
    headers,
    credentials: "include"
  });

  if (response.status !== 206 && response.status !== 200) {
    throw new Error(`下载失败（HTTP ${response.status}）`);
  }
  return response.blob();
}

/**
 * 软限速：根据实际耗时与期望耗时的差值决定是否等待。
 * @param bytes  本次下载字节数
 * @param elapsedMs 实际耗时（ms）
 * @param maxBps 速度上限（bytes/sec），0 表示不限速
 */
async function applyRateLimit(bytes: number, elapsedMs: number, maxBps: number): Promise<void> {
  if (maxBps <= 0) return;
  const expectedMs = (bytes / maxBps) * 1000;
  const sleepMs = expectedMs - elapsedMs;
  if (sleepMs > 0) {
    await new Promise<void>((resolve) => setTimeout(resolve, sleepMs));
  }
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
