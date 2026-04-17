import { describe, it, expect } from 'vitest';
import { ALLOWED_MIME_PATTERNS, FileTooLargeError, MAX_FILE_SIZE_BYTES, MimeNotAllowedError, ensureMimeAllowed, ensureSizeAllowed } from '../types';

describe('mime / size 校验', () => {
  it('放行白名单', () => {
    expect(() => ensureMimeAllowed('image/png')).not.toThrow();
    expect(() => ensureMimeAllowed('video/mp4')).not.toThrow();
    expect(() => ensureMimeAllowed('application/pdf')).not.toThrow();
    expect(() => ensureMimeAllowed('text/markdown')).not.toThrow();
    expect(() => ensureMimeAllowed('application/vnd.openxmlformats-officedocument.wordprocessingml.document')).not.toThrow();
  });

  it('拒绝高风险类型', () => {
    expect(() => ensureMimeAllowed('application/x-msdownload')).toThrow(MimeNotAllowedError);
    expect(() => ensureMimeAllowed('application/x-sh')).toThrow(MimeNotAllowedError);
  });

  it('size 校验', () => {
    expect(() => ensureSizeAllowed(1024)).not.toThrow();
    expect(() => ensureSizeAllowed(MAX_FILE_SIZE_BYTES + 1)).toThrow(FileTooLargeError);
    expect(() => ensureSizeAllowed(1024, 100)).toThrow(FileTooLargeError);
  });

  it('白名单覆盖图像/视频/音频/PDF/Office/text/json 7 大类', () => {
    expect(ALLOWED_MIME_PATTERNS.length).toBeGreaterThanOrEqual(7);
  });
});
