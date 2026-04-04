import type { RouteLocationNormalized, NavigationGuardNext } from "vue-router";

/**
 * 平台控制台路由守卫（存根）。
 * Phase 0 仅作占位；Phase 3 正式实现平台控制台鉴权逻辑。
 *
 * 预期职责（Phase 3 填入）：
 * - 验证平台 token 有效性
 * - 校验用户是否具有平台控制台访问权限
 * - 权限不足时重定向到 fallback 路径
 */
export async function checkPlatformAuth(
  _to: RouteLocationNormalized,
  _from: RouteLocationNormalized,
  next: NavigationGuardNext,
): Promise<void> {
  // 存根：放行所有请求。Phase 3 在此处接入平台 auth/permission 校验逻辑。
  next();
}
