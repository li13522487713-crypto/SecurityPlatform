import { useEffect, useState } from "react";
import { Navigate } from "react-router-dom";
import { selectWorkspacePath, signPath } from "@atlas/app-shell-shared";
import { useAppI18n } from "../i18n";
import { useBootstrap } from "../bootstrap-context";
import { useAuth } from "../auth-context";
import { PageShell } from "../_shared";

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
  const [resolving, setResolving] = useState(true);
  const [target, setTarget] = useState<string | null>(null);

  useEffect(() => {
    if (bootstrap.loading || auth.loading) {
      return;
    }
    if (!bootstrap.platformReady) {
      setTarget("/platform-not-ready");
      setResolving(false);
      return;
    }
    if (!bootstrap.appReady) {
      setTarget("/app-setup");
      setResolving(false);
      return;
    }
    if (!auth.isAuthenticated) {
      setTarget(signPath());
      setResolving(false);
      return;
    }

    setTarget(selectWorkspacePath());
    setResolving(false);
  }, [auth.isAuthenticated, auth.loading, bootstrap.appReady, bootstrap.loading, bootstrap.platformReady]);

  if (resolving) {
    return <PageShell loading loadingTip={t("loading")} />;
  }

  if (target) {
    return <Navigate to={target} replace />;
  }

  return <Navigate to={selectWorkspacePath()} replace />;
}
