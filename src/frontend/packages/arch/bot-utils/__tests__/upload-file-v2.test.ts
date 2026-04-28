/*
 * Copyright 2025 coze-dev Authors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

import { afterEach, describe, expect, test, vi } from 'vitest';

import { uploadFileV2 } from '../src/upload-file-v2';

globalThis.APP_ID = 1;
globalThis.IMAGE_FALLBACK_HOST = 'fallback.test';
globalThis.BYTE_UPLOADER_REGION = 'cn-north-1';
globalThis.IS_OVERSEA = false;

vi.mock('@coze-arch/bot-api', () => ({
  DeveloperApi: {
    GetUploadAuthToken: vi.fn(() =>
      Promise.resolve({ data: { service_id: '', upload_host: '' } }),
    ),
  },
}));
vi.mock('@coze-studio/uploader-adapter', () => {
  class MockUploader {
    start = () => 0;
    addFile = () => '12312341';
    test: 'test';
    on(event: string, cb: (data: any) => void) {
      if (event === 'complete') {
        cb({ uploadResult: { Uri: 'test_url' } });
      } else if (event === 'error') {
        cb({ extra: 'error' });
      } else if (event === 'progress') {
        cb(50);
      }
    }
  }
  return {
    getUploader: vi.fn((props: any, isOverSea?: boolean) => new MockUploader()),
    shouldUseAtlasLocalUpload: vi.fn(() => false),
    createLocalUploader: vi.fn(() => new MockUploader()),
  };
});

describe('upload-file', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  test('upLoadFile should resolve Url of result if upload success', () =>
    new Promise((resolve, reject) => {
      global.IS_OVERSEA = true;
      uploadFileV2({
        fileItemList: [{ file: new File([], 'test_file'), fileType: 'image' }],
        userId: '123',
        timeout: undefined,
        signal: new AbortController().signal,
        onSuccess: event => {
          try {
            expect(event.uploadResult.Uri).equal('test_url');
            resolve('ok');
          } catch (e) {
            reject(e);
          }
        },
        onUploadAllSuccess(event) {
          try {
            expect(event[0].uploadResult.Uri).equal('test_url');
            resolve('ok');
          } catch (e) {
            reject(e);
          }
        },
      });
      global.IS_OVERSEA = false;
      uploadFileV2({
        fileItemList: [{ file: new File([], 'test_file'), fileType: 'image' }],
        userId: '123',
        timeout: undefined,
        signal: new AbortController().signal,
        onSuccess: event => {
          try {
            expect(event.uploadResult.Uri).equal('test_url');
            resolve('ok');
          } catch (e) {
            reject(e);
          }
        },
        onUploadAllSuccess(event) {
          try {
            expect(event[0].uploadResult.Uri).equal('test_url');
            resolve('ok');
          } catch (e) {
            reject(e);
          }
        },
      });
    }));

  test('should use atlas local uploader when local upload is enabled', async () => {
    const uploaderAdapter = await import('@coze-studio/uploader-adapter');
    vi.mocked(uploaderAdapter.shouldUseAtlasLocalUpload).mockReturnValue(true);

    await new Promise((resolve, reject) => {
      uploadFileV2({
        fileItemList: [{ file: new File([], 'test_file'), fileType: 'image' }],
        userId: '123',
        timeout: undefined,
        signal: new AbortController().signal,
        onSuccess: event => {
          try {
            expect(event.uploadResult.Uri).toBe('test_url');
            expect(uploaderAdapter.createLocalUploader).toHaveBeenCalled();
            resolve('ok');
          } catch (error) {
            reject(error);
          }
        },
      });
    });
  });
});
