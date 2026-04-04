import type { RouteLocationNormalized, NavigationGuardNext } from "vue-router";

/**
 * 应用运行态路由守卫（存根）。
 * Phase 0 仅作占位；Phase 3 正式实现应用运行态鉴权逻辑。
 *
 * 预期职责（Phase 3 填入）：
 * - 读取应用运行时 session token
 * - 无 token 时重定向到 AppEntryGatewayPage（含 redirect 参数）
 * - 验证 token 有效性并写入 appRuntimeContext
 * - 支持 /r/:appKey/:pageKey 路由的 appKey 上下文注入
 */
export async function checkAppRuntimeAuth(
  _to: RouteLocationNormalized,
  _from: RouteLocationNormalized,
  next: NavigationGuardNext,
): Promise<void> {
  // 存根：放行所有请求。Phase 3 在此处接入应用运行时 auth/entry-gateway 逻辑。
  next();
}
