/**
 * 运行时启动流程编排。
 *
 * 进入页面时：
 *   1. 解析路由参数 (appKey, pageKey)
 *   2. 加载 RuntimeManifest（已发布 release）
 *   3. 初始化 RuntimeContextStore
 *   4. 生成 runtimeExecutionId
 */

import type { RouteLocationNormalizedLoaded } from "vue-router";
import { getAuthProfile, getTenantId } from "@atlas/shared-core";
import { useRuntimeContextStore } from "../context/runtime-context-store";
import { loadRuntimeManifest } from "./runtime-manifest-loader";
import { registerBuiltinHandlers } from "../actions/builtin-handlers";
import type { RuntimeManifest } from "../release/runtime-release-types";

let handlersRegistered = false;

export interface RuntimeBootstrapResult {
  manifest: RuntimeManifest;
  executionId: string;
}

function generateExecutionId(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return `exec-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

export async function bootstrapRuntime(
  route: RouteLocationNormalizedLoaded,
): Promise<RuntimeBootstrapResult> {
  const appKey = String(route.params.appKey ?? "");
  const pageKey = String(route.params.pageKey ?? "");

  if (!appKey || !pageKey) {
    throw new Error("Missing appKey or pageKey in route params");
  }

  if (!handlersRegistered) {
    registerBuiltinHandlers();
    handlersRegistered = true;
  }

  const manifest = await loadRuntimeManifest(appKey, pageKey);
  const executionId = generateExecutionId();
  const profile = getAuthProfile();
  const tenantId = getTenantId() ?? "";

  const store = useRuntimeContextStore();
  store.initContext({
    app: {
      appKey,
      name: manifest.pageTitle,
      releaseId: manifest.releaseId,
      releaseVersion: manifest.releaseVersion,
    },
    page: {
      pageKey,
      title: manifest.pageTitle,
      pageType: manifest.pageType,
      mode: "view",
    },
    user: {
      id: profile?.id,
      name: profile?.displayName || profile?.username || "",
      roles: profile?.roles ?? [],
      permissions: profile?.permissions ?? [],
    },
    route: {
      path: route.fullPath,
      params: Object.fromEntries(
        Object.entries(route.params).map(([k, v]) => [k, String(v ?? "")]),
      ),
      query: Object.fromEntries(
        Object.entries(route.query).map(([k, v]) => [k, String(v ?? "")]),
      ),
    },
    env: {
      runtimeExecutionId: executionId,
      releaseId: manifest.releaseId,
      releaseVersion: manifest.releaseVersion,
    },
    global: {
      tenantId,
    },
  });

  return { manifest, executionId };
}
