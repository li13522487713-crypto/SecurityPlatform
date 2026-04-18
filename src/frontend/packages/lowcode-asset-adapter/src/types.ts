/**
 * Asset 适配器协议（M10 C10-1）。
 *
 * 强约束（PLAN.md §M10）：
 * - 禁止把 File 对象直接塞给 workflow inputs；必须先 prepareUpload/completeUpload 换 fileHandle。
 * - mime/大小白名单在前端 + 服务端双校验（前端走 ALLOWED_MIME；服务端走 IFileStorageService）。
 * - 7 天 GC 由后端 Hangfire 任务执行。
 */

export interface PrepareUploadRequest {
  /** 原文件名。*/
  fileName: string;
  /** mime（前端可由 file.type 给出，服务端会二次校验）。*/
  contentType: string;
  /** 文件总字节数。*/
  size: number;
  /** 可选：sha256 摘要（秒传用）。*/
  sha256?: string;
}

export interface PrepareUploadResponse {
  /** 短期 token（关联本次上传会话）。*/
  token: string;
  /** 直传 URL（签名）。*/
  uploadUrl: string;
  /** 是否秒传命中（命中时直接返回 fileHandle，无需 PUT）。*/
  instantHit?: boolean;
  /** 命中时返回的文件句柄。*/
  fileHandle?: string;
}

export interface CompleteUploadResponse {
  fileHandle: string;
  url: string;
  contentType: string;
  size: number;
  /** 仅图片场景填充。*/
  imageId?: string;
}

export interface AssetAdapter {
  prepareUpload(req: PrepareUploadRequest, signal?: AbortSignal): Promise<PrepareUploadResponse>;
  completeUpload(token: string, blob: Blob | File, signal?: AbortSignal, onProgress?: (p: { loaded: number; total: number }) => void): Promise<CompleteUploadResponse>;
  /** 取消上传：终止 fetch + 通知服务端释放占位。*/
  cancel(token: string): Promise<void>;
  /** 删除已上传文件。*/
  remove(fileHandle: string): Promise<void>;
}

/** mime 白名单（前端预校验，服务端再次校验）。*/
export const ALLOWED_MIME_PATTERNS: ReadonlyArray<RegExp> = [
  /^image\/(png|jpe?g|gif|webp|bmp|svg\+xml)$/i,
  /^video\/(mp4|webm|ogg|quicktime)$/i,
  /^audio\/(mp3|mpeg|wav|ogg|webm|aac)$/i,
  /^application\/pdf$/i,
  /^application\/(vnd\.openxmlformats-officedocument\.|msword|vnd\.ms-excel|vnd\.ms-powerpoint).*/i,
  /^text\/(plain|csv|markdown|html)$/i,
  /^application\/json$/i
];

export const MAX_FILE_SIZE_BYTES = 200 * 1024 * 1024; // 200 MiB

export class MimeNotAllowedError extends Error {
  constructor(public readonly contentType: string) {
    super(`mime 类型未允许：${contentType}`);
    this.name = 'MimeNotAllowedError';
  }
}

export class FileTooLargeError extends Error {
  constructor(public readonly size: number, public readonly limit: number) {
    super(`文件过大：${size} 字节，限制 ${limit} 字节`);
    this.name = 'FileTooLargeError';
  }
}

export function ensureMimeAllowed(contentType: string): void {
  if (!ALLOWED_MIME_PATTERNS.some((re) => re.test(contentType))) {
    throw new MimeNotAllowedError(contentType);
  }
}

export function ensureSizeAllowed(size: number, limit: number = MAX_FILE_SIZE_BYTES): void {
  if (size > limit) throw new FileTooLargeError(size, limit);
}
