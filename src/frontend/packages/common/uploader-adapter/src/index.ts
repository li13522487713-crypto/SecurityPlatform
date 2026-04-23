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

import Uploader, { type ImageXFileOption } from 'tt-uploader';
import {
  type Config,
  type STSToken,
  type ObjectSync,
  type EventPayloadMaps,
} from '@coze-arch/uploader-interface';

export interface FileOption {
  file: Blob;
  stsToken: STSToken;
  type?: any;
  callbackArgs?: string;
  testHost?: string;
  objectSync?: ObjectSync;
}

export const getUploader = (config: Config, isOversea?: boolean) => {
  const imageHost = (
    config.imageHost ||
    config.imageFallbackHost ||
    ''
  ).replace(/^https:\/\//, config.schema ? `${config.schema}://` : '');
  const uploader = new Uploader({
    /**
     * The schema needs to be dynamically obtained according to the deployment environment of the current user
     * Schema compatibility with special HTTP scenario fields
     */
    schema: config.schema,
    region: isOversea ? 'ap-singapore-1' : 'cn-north-1',
    imageHost,
    appId: config.appId,
    userId: config.userId,
    useFileExtension: config.useFileExtension,
    uploadTimeout: config.uploadTimeout,
    imageConfig: config.imageConfig,
  } as any);

  const originalAddImageFile: (option: ImageXFileOption) => string =
    uploader.addImageFile.bind(uploader);

  uploader.addFile = function (options: FileOption) {
    const imageOptions: ImageXFileOption = {
      file: options.file,
      stsToken: options.stsToken,
    };
    return originalAddImageFile(imageOptions);
  };
  return uploader as CozeUploader;
};

type UploadEventName = 'complete' | 'error' | 'progress' | 'stream-progress';

const AUTH_STORAGE_PREFIX = 'atlas';
const ACCESS_TOKEN_KEY = `${AUTH_STORAGE_PREFIX}_access_token`;
const TENANT_ID_KEY = `${AUTH_STORAGE_PREFIX}_tenant_id`;

const safeStorageGet = (key: string) => {
  if (typeof window === 'undefined') {
    return null;
  }

  try {
    return window.sessionStorage.getItem(key) ?? window.localStorage.getItem(key);
  } catch {
    return null;
  }
};

const buildAtlasHeaders = () => {
  const headers = new Headers();
  const accessToken = safeStorageGet(ACCESS_TOKEN_KEY);
  const tenantId = safeStorageGet(TENANT_ID_KEY);
  if (accessToken) {
    headers.set('Authorization', `Bearer ${accessToken}`);
  }
  if (tenantId) {
    headers.set('X-Tenant-Id', tenantId);
  }
  headers.set('X-Requested-With', 'XMLHttpRequest');
  return headers;
};

export const shouldUseAtlasLocalUpload = () =>
  Boolean(safeStorageGet(ACCESS_TOKEN_KEY) && safeStorageGet(TENANT_ID_KEY));

class LocalUploaderEmitter {
  private readonly listeners: Record<UploadEventName, Set<(payload: unknown) => void>> = {
    complete: new Set(),
    error: new Set(),
    progress: new Set(),
    'stream-progress': new Set(),
  };

  on<T extends UploadEventName>(eventName: T, callback: (info: EventPayloadMaps[T]) => void) {
    this.listeners[eventName].add(callback as (payload: unknown) => void);
  }

  removeAllListeners(eventName: UploadEventName) {
    this.listeners[eventName].clear();
  }

  emit<T extends UploadEventName>(eventName: T, payload: EventPayloadMaps[T]) {
    this.listeners[eventName].forEach(listener => listener(payload));
  }
}

export const createLocalUploader = () => {
  const emitter = new LocalUploaderEmitter();
  const files = new Map<string, { file: Blob; controller?: AbortController }>();
  const createBasePayload = (key: string, percent: number, message: string) => ({
    startTime: Date.now(),
    endTime: Date.now(),
    stageStartTime: Date.now(),
    stageEndTime: Date.now(),
    duration: 0,
    fileSize: 0,
    key,
    oid: key,
    percent,
    signature: '',
    sliceLength: 0,
    stage: 'atlas-local-upload',
    status: 1 as const,
    task: null,
    type: 'success' as const,
    uploadID: key,
    extra: {
      message,
    },
  });

  const uploader = {
    addFile(options: FileOption) {
      const key =
        typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
          ? crypto.randomUUID()
          : `atlas-upload-${Date.now()}-${Math.random().toString(16).slice(2)}`;
      files.set(key, { file: options.file });
      return key;
    },
    async start(key?: string) {
      if (!key) {
        return;
      }

      const target = files.get(key);
      if (!target) {
        return;
      }

      const controller = new AbortController();
      target.controller = controller;
      const file = target.file;
      const formData = new FormData();
      const fileName = file instanceof File ? file.name : `upload-${Date.now()}.bin`;
      formData.append('file', file, fileName);

      emitter.emit('progress', {
        ...createBasePayload(key, 10, 'uploading'),
      } as EventPayloadMaps['progress']);

      try {
        const response = await fetch('/api/v1/files', {
          method: 'POST',
          headers: buildAtlasHeaders(),
          body: formData,
          credentials: 'include',
          signal: controller.signal,
        });

        if (!response.ok) {
          const errorText = await response.text();
          throw new Error(errorText || `Upload failed with status ${response.status}`);
        }

        const payload = (await response.json()) as {
          data?: { id?: number; originalName?: string };
          message?: string;
        };
        const fileId = payload.data?.id;
        if (!fileId) {
          throw new Error(payload.message || 'Upload response missing file id');
        }

        emitter.emit('progress', {
          ...createBasePayload(key, 100, 'uploaded'),
        } as EventPayloadMaps['progress']);

        emitter.emit('complete', {
          ...createBasePayload(key, 100, 'uploaded'),
          uploadResult: {
            Uri: `atlas-file:${fileId}`,
            FileName: payload.data?.originalName ?? fileName,
            ImageUri: `atlas-file:${fileId}`,
            ImageWidth: 0,
            ImageHeight: 0,
          },
        } as EventPayloadMaps['complete']);
      } catch (error) {
        emitter.emit('error', {
          ...createBasePayload(key, 0, error instanceof Error ? error.message : String(error)),
          type: 'error',
          extra: {
            error,
            message: error instanceof Error ? error.message : String(error),
          },
        } as EventPayloadMaps['error']);
      }
    },
    pause() {
      return;
    },
    cancel(key?: string) {
      if (key) {
        files.get(key)?.controller?.abort();
        return;
      }

      files.forEach(item => item.controller?.abort());
    },
    on<T extends UploadEventName>(eventName: T, callback: (info: EventPayloadMaps[T]) => void) {
      emitter.on(eventName, callback);
    },
    removeAllListeners(eventName: UploadEventName) {
      emitter.removeAllListeners(eventName);
    },
  };

  return uploader as unknown as CozeUploader;
};

export type CozeUploader = Uploader & {
  addFile: (options: FileOption) => string;
  removeAllListeners: (eventName: UploadEventName) => void;
};

export {
  type Config,
} from '@coze-arch/uploader-interface';
