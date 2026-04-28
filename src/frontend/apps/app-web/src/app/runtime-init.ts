import { setAuthStorageNamespace, getAccessToken, getTenantId } from "@atlas/shared-react-core/utils";
import { pullFeatureFlags, type FEATURE_FLAGS } from "../../../../packages/arch/bot-flags/src";
import { addGlobalRequestInterceptor } from "../../../../packages/arch/bot-http/src";
import { suppressBenignBrowserErrors } from "../bootstrap/suppress-benign-browser-errors";

let runtimeInitialized = false;

function applyAuthorizationHeaders<T extends { headers?: unknown }>(config: T): T {
  const accessToken = getAccessToken();
  const tenantId = getTenantId();
  const headers = (config.headers ?? {}) as Record<string, string>;

  if (accessToken) {
    headers.Authorization = `Bearer ${accessToken}`;
  }

  if (tenantId) {
    headers["X-Tenant-Id"] = tenantId;
  }

  config.headers = headers;
  return config;
}

export function initializeAppRuntime() {
  if (runtimeInitialized) {
    return;
  }

  runtimeInitialized = true;
  setAuthStorageNamespace("atlas_app");
  suppressBenignBrowserErrors();
  addGlobalRequestInterceptor((config) => applyAuthorizationHeaders(config));
}

export async function loadAppFeatureFlags() {
  try {
    await pullFeatureFlags({
      strict: false,
      timeout: 300,
      fetchFeatureGating: async () => ({}) as FEATURE_FLAGS
    });
  } catch {
    // 本地低代码工作流编辑器不能被远端 feature flags 初始化失败阻断。
  }
}
