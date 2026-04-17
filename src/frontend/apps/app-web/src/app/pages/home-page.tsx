import { useEffect, useState } from "react";
import { Navigate } from "react-router-dom";
import { Spin } from "@douyinfe/semi-ui";
import { getTenantId } from "@atlas/shared-react-core/utils";
import { selectWorkspacePath, signPath, workspaceHomePath } from "@atlas/app-shell-shared";
import { useAppI18n } from "../i18n";
import { useBootstrap } from "../bootstrap-context";
import { useAuth } from "../auth-context";
import { getWorkspaces } from "../../services/api-org-workspaces";
import { rememberLastWorkspaceId, readLastWorkspaceId } from "../layouts/workspace-shell";

/**
 * `/` 入口：纯重定向网关。
 *
 * 流程：
 * 1. bootstrap loading / auth loading → 显示 Spin
 * 2. platform 未就绪 → /platform-not-ready
 * 3. app 未就绪 → /app-setup
 * 4. 未登录 → /sign
 * 5. 已登录 → 选目标工作空间：
 *    a) localStorage 的 last workspace
 *    b) 否则 getWorkspaces() 取第一个
 *    c) 否则 /select-workspace
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

    const lastWorkspaceId = readLastWorkspaceId();
    if (lastWorkspaceId) {
      setTarget(workspaceHomePath(lastWorkspaceId));
      setResolving(false);
      return;
    }

    const orgId = getTenantId();
    if (!orgId) {
      setTarget(selectWorkspacePath());
      setResolving(false);
      return;
    }

    getWorkspaces(orgId)
      .then(list => {
        if (list.length === 0) {
          setTarget(selectWorkspacePath());
          return;
        }
        const first = list[0];
        rememberLastWorkspaceId(first.id);
        setTarget(workspaceHomePath(first.id));
      })
      .catch(() => {
        setTarget(selectWorkspacePath());
      })
      .finally(() => {
        setResolving(false);
      });
  }, [auth.isAuthenticated, auth.loading, bootstrap.appReady, bootstrap.loading, bootstrap.platformReady]);

  if (resolving) {
    return (
      <div className="atlas-loading-page">
        <Spin size="large" />
        <span style={{ marginLeft: 8 }}>{t("loading")}</span>
      </div>
    );
  }

  if (target) {
    return <Navigate to={target} replace />;
  }

  return <Navigate to={selectWorkspacePath()} replace />;
}
