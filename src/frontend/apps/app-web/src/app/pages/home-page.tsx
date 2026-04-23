import { Navigate } from "react-router-dom";
import { useAppI18n } from "../i18n";
import { useBootstrap } from "../bootstrap-context";
import { useAuth } from "../auth-context";
import { PageShell } from "../_shared";
import { resolveStartupRedirectTarget, STARTUP_ROUTE_PATHS } from "../startup-routing";

/**
 * `/` 入口：纯重定向网关。
 *
 * 流程：
 * 1. bootstrap loading / auth loading → 显示 Spin
 * 2. platform 未就绪 → /platform-not-ready
 * 3. app 未就绪 → /app-setup
 * 4. 未登录 → /sign
 * 5. 已登录 → 统一进入组织工作空间页
 */
export function HomePage() {
  const { t } = useAppI18n();
  const auth = useAuth();
  const bootstrap = useBootstrap();

  if (bootstrap.loading || auth.loading) {
    return <PageShell loading loadingTip={t("loading")} />;
  }

  const target = resolveStartupRedirectTarget({
    pathname: "/",
    bootstrap,
    auth
  });

  return <Navigate to={target ?? STARTUP_ROUTE_PATHS.selectWorkspace} replace />;
}
