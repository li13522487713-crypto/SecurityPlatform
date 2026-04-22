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

import axios, { type AxiosResponse, isAxiosError } from 'axios';
import { redirect } from '@coze-arch/web-context';
import { logger } from '@coze-arch/logger';

import { emitAPIErrorEvent, APIErrorEvent } from './eventbus';
import { ApiError, reportHttpError, ReportEventNames } from './api-error';

interface UnauthorizedResponse {
  data: {
    redirect_uri: string;
  };
  code: number;
  msg: string;
}

interface AuthProfileLike {
  tenantId?: string;
}

export enum ErrorCodes {
  NOT_LOGIN = 700012006,
  COUNTRY_RESTRICTED = 700012015,
  COZE_TOKEN_INSUFFICIENT = 702082020,
  COZE_TOKEN_INSUFFICIENT_WORKFLOW = 702095072,
}

export const axiosInstance = axios.create();
let unauthorizedHandler: (() => void | Promise<void>) | null = null;

const HTTP_STATUS_COE_UNAUTHORIZED = 401;

type ResponseInterceptorOnFulfilled = (res: AxiosResponse) => AxiosResponse;
const customInterceptors = {
  response: new Set<ResponseInterceptorOnFulfilled>(),
};

const AUTH_STORAGE_PREFIX = 'atlas';
const ACCESS_TOKEN_KEY = `${AUTH_STORAGE_PREFIX}_access_token`;
const REFRESH_TOKEN_KEY = `${AUTH_STORAGE_PREFIX}_refresh_token`;
const TENANT_ID_KEY = `${AUTH_STORAGE_PREFIX}_tenant_id`;
const PROFILE_KEY = `${AUTH_STORAGE_PREFIX}_auth_profile`;

function getSafeStorage(kind: 'localStorage' | 'sessionStorage'): Storage | null {
  if (typeof window === 'undefined') {
    return null;
  }

  try {
    return window[kind];
  } catch {
    return null;
  }
}

function safeGetItem(kind: 'localStorage' | 'sessionStorage', key: string) {
  const storage = getSafeStorage(kind);
  if (!storage) {
    return null;
  }

  try {
    return storage.getItem(key);
  } catch {
    return null;
  }
}

function safeSetItem(kind: 'localStorage' | 'sessionStorage', key: string, value: string) {
  const storage = getSafeStorage(kind);
  if (!storage) {
    return;
  }

  try {
    storage.setItem(key, value);
  } catch {
    // Ignore storage write failures to avoid blocking requests.
  }
}

function safeRemoveItem(kind: 'localStorage' | 'sessionStorage', key: string) {
  const storage = getSafeStorage(kind);
  if (!storage) {
    return;
  }

  try {
    storage.removeItem(key);
  } catch {
    // Ignore storage cleanup failures to avoid masking the original auth error.
  }
}

function getAccessToken() {
  return safeGetItem('sessionStorage', ACCESS_TOKEN_KEY) ?? safeGetItem('localStorage', ACCESS_TOKEN_KEY);
}

function getTenantId() {
  return safeGetItem('localStorage', TENANT_ID_KEY);
}

function setTenantId(tenantId: string) {
  safeSetItem('localStorage', TENANT_ID_KEY, tenantId);
}

function getAuthProfile(): AuthProfileLike | null {
  const raw = safeGetItem('sessionStorage', PROFILE_KEY) ?? safeGetItem('localStorage', PROFILE_KEY);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as AuthProfileLike;
  } catch {
    safeRemoveItem('sessionStorage', PROFILE_KEY);
    safeRemoveItem('localStorage', PROFILE_KEY);
    return null;
  }
}

function clearAuthStorage() {
  safeRemoveItem('sessionStorage', ACCESS_TOKEN_KEY);
  safeRemoveItem('sessionStorage', PROFILE_KEY);
  safeRemoveItem('localStorage', ACCESS_TOKEN_KEY);
  safeRemoveItem('localStorage', REFRESH_TOKEN_KEY);
  safeRemoveItem('localStorage', TENANT_ID_KEY);
  safeRemoveItem('localStorage', PROFILE_KEY);
}

function getClientContextHeaders() {
  const userAgent = typeof navigator === 'undefined' ? '' : navigator.userAgent.toLowerCase();
  const platform = userAgent.includes('android')
    ? 'Android'
    : userAgent.includes('iphone') || userAgent.includes('ipad') || userAgent.includes('ipod')
      ? 'iOS'
      : 'Web';
  const agent = userAgent.includes('edg/')
    ? 'Edge'
    : userAgent.includes('chrome/') && !userAgent.includes('edg/')
      ? 'Chrome'
      : userAgent.includes('firefox/')
        ? 'Firefox'
        : userAgent.includes('safari/') && !userAgent.includes('chrome/')
          ? 'Safari'
          : 'Other';

  return {
    'X-Client-Type': 'WebH5',
    'X-Client-Platform': platform,
    'X-Client-Channel': 'Browser',
    'X-Client-Agent': agent,
  };
}

function setHeaderValue(headers: NonNullable<typeof axiosInstance.defaults.headers>, key: string, value: string) {
  if (typeof headers.set === 'function') {
    headers.set(key, value);
    return;
  }

  headers[key] = value;
}

function getHeaderValue(headers: NonNullable<typeof axiosInstance.defaults.headers>, key: string) {
  if (typeof headers.get === 'function') {
    return headers.get(key);
  }

  return headers[key];
}

function resolveTenantId() {
  const token = getAccessToken();
  let tenantId = getTenantId();
  if (!tenantId && token) {
    const profileTenantId = getAuthProfile()?.tenantId?.trim();
    if (profileTenantId) {
      tenantId = profileTenantId;
      setTenantId(profileTenantId);
    }
  }

  return tenantId;
}

async function handleUnauthorized(error: unknown) {
  clearAuthStorage();

  if (unauthorizedHandler) {
    await unauthorizedHandler();
    return;
  }

  if (isAxiosError(error) && typeof error.response?.data === 'object') {
    const unauthorizedData = error.response.data as UnauthorizedResponse;
    const redirectUri = unauthorizedData?.data?.redirect_uri;
    if (redirectUri) {
      redirect(redirectUri);
    }
  }
}

axiosInstance.interceptors.response.use(
  response => {
    logger.info({
      namespace: 'api',
      scope: 'response',
      message: '----',
      meta: { response },
    });
    const { data = {} } = response;

    // Added interface return message field
    const { code, msg, message } = data;

    if (code !== 0) {
      const apiError = new ApiError(String(code), message ?? msg, response);

      switch (code) {
        case ErrorCodes.NOT_LOGIN: {
          // @ts-expect-error type safe
          apiError.config.__disableErrorToast = true;
          emitAPIErrorEvent(APIErrorEvent.UNAUTHORIZED, apiError);
          break;
        }
        case ErrorCodes.COUNTRY_RESTRICTED: {
          // @ts-expect-error type safe
          apiError.config.__disableErrorToast = true;
          emitAPIErrorEvent(APIErrorEvent.COUNTRY_RESTRICTED, apiError);
          break;
        }
        case ErrorCodes.COZE_TOKEN_INSUFFICIENT: {
          // eslint-disable-next-line @typescript-eslint/ban-ts-comment
          // @ts-expect-error
          apiError.config.__disableErrorToast = true;
          emitAPIErrorEvent(APIErrorEvent.COZE_TOKEN_INSUFFICIENT, apiError);
          break;
        }
        case ErrorCodes.COZE_TOKEN_INSUFFICIENT_WORKFLOW: {
          // eslint-disable-next-line @typescript-eslint/ban-ts-comment
          // @ts-expect-error
          apiError.config.__disableErrorToast = true;
          emitAPIErrorEvent(APIErrorEvent.COZE_TOKEN_INSUFFICIENT, apiError);
          break;
        }
        default: {
          break;
        }
      }

      reportHttpError(ReportEventNames.ApiError, apiError);
      return Promise.reject(apiError);
    }
    let res = response;
    for (const interceptor of customInterceptors.response) {
      res = interceptor(res);
    }

    return res;
  },
  error => {
    if (isAxiosError(error)) {
      reportHttpError(ReportEventNames.NetworkError, error);
      if (error.response?.status === HTTP_STATUS_COE_UNAUTHORIZED) {
        void handleUnauthorized(error);
      }
    }

    return Promise.reject(error);
  },
);

axiosInstance.interceptors.request.use(config => {
  config.withCredentials ??= true;
  const authToken = getAccessToken();
  const tenantId = resolveTenantId();

  if (authToken && !getHeaderValue(config.headers, 'authorization')) {
    setHeaderValue(config.headers, 'Authorization', `Bearer ${authToken}`);
  }

  if (tenantId && !getHeaderValue(config.headers, 'x-tenant-id')) {
    setHeaderValue(config.headers, 'X-Tenant-Id', tenantId);
  }

  const clientHeaders = getClientContextHeaders();
  (Object.entries(clientHeaders) as [string, string][]).forEach(([key, value]) => {
    if (value && !getHeaderValue(config.headers, key)) {
      setHeaderValue(config.headers, key, value);
    }
  });

  setHeaderValue(config.headers, 'x-requested-with', 'XMLHttpRequest');
  if (
    ['post', 'get'].includes(config.method?.toLowerCase() ?? '') &&
    !getHeaderValue(config.headers, 'content-type')
  ) {
    // The new CSRF protection requires all post/get requests to have this header.
    setHeaderValue(config.headers, 'content-type', 'application/json');
    if (!config.data) {
      // Axios will automatically clear the content-type when the data is empty, so you need to set an empty object
      config.data = {};
    }
  }
  return config;
});

type AddRequestInterceptorShape = typeof axiosInstance.interceptors.request.use;
/**
 * Add an interceptor handler for global axios to easily extend axios behavior on top.
 * Please note that this interface will affect all requests under bot-http. Please ensure the stability of the behavior
 */
export const addGlobalRequestInterceptor: AddRequestInterceptorShape = (
  onFulfilled,
  onRejected?,
) => {
  // PS: It is not expected to directly expose the axios instance to the upper layer, because it is not known how it will be modified and used
  // Therefore, several methods need to be exposed to keep behavior and side effects under control
  const id = axiosInstance.interceptors.request.use(onFulfilled, onRejected);
  return id;
};

type RemoveRequestInterceptorShape =
  typeof axiosInstance.interceptors.request.eject;
/**
 * Removes the interceptor handler of the global axios where the id parameter is the value returned by the calling addGlobalRequestInterceptor
 */
export const removeGlobalRequestInterceptor: RemoveRequestInterceptorShape = (
  id: number,
) => {
  axiosInstance.interceptors.request.eject(id);
};

export const addGlobalResponseInterceptor = (
  onFulfilled: ResponseInterceptorOnFulfilled,
) => {
  customInterceptors.response.add(onFulfilled);
  return () => {
    customInterceptors.response.delete(onFulfilled);
  };
};

export const setBotApiUnauthorizedHandler = (
  handler: (() => void | Promise<void>) | null,
) => {
  unauthorizedHandler = handler;
};
