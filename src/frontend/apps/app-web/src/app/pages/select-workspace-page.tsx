import { useEffect, useState } from "react";
import { Avatar, Button, Empty, Spin, Typography } from "@douyinfe/semi-ui";
import { useNavigate } from "react-router-dom";
import { getTenantId } from "@atlas/shared-react-core/utils";
import { workspaceHomePath } from "@atlas/app-shell-shared";
import { useAppI18n } from "../i18n";
import { useAuth } from "../auth-context";
import { getWorkspaces, type WorkspaceSummaryDto } from "../../services/api-org-workspaces";
import { rememberLastWorkspaceId } from "../layouts/workspace-shell";

/**
 * 工作空间选择页：用于多空间用户登录后或没有 last visited 的状态。
 * 自动加载当前租户下所有可访问空间，单击进入并记住 last workspace。
 */
export function SelectWorkspacePage() {
  const { t } = useAppI18n();
  const auth = useAuth();
  const navigate = useNavigate();
  const [workspaces, setWorkspaces] = useState<WorkspaceSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    const orgId = getTenantId();
    if (!orgId) {
      setLoading(false);
      return () => {
        cancelled = true;
      };
    }
    setLoading(true);
    getWorkspaces(orgId)
      .then(list => {
        if (cancelled) {
          return;
        }
        setWorkspaces(list);
        if (list.length === 1) {
          rememberLastWorkspaceId(list[0].id);
          navigate(workspaceHomePath(list[0].id), { replace: true });
        }
      })
      .catch(() => {
        if (!cancelled) {
          setWorkspaces([]);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [navigate]);

  if (auth.loading || loading) {
    return (
      <div className="atlas-loading-page"><Spin size="large" /></div>
    );
  }

  return (
    <div className="coze-select-workspace" data-testid="coze-select-workspace-page">
      <Typography.Title heading={3}>{t("cozeShellWorkspaceSwitcherTitle")}</Typography.Title>
      <Typography.Text type="tertiary">{t("cozeShellWorkspaceSwitcherCurrent")}</Typography.Text>

      {workspaces.length === 0 ? (
        <Empty description={t("cozeShellWorkspaceSwitcherEmpty")} style={{ marginTop: 32 }} />
      ) : (
        <div className="coze-select-workspace__list">
          {workspaces.map(item => (
            <button
              key={item.id}
              type="button"
              className="coze-select-workspace__item"
              onClick={() => {
                rememberLastWorkspaceId(item.id);
                navigate(workspaceHomePath(item.id));
              }}
              data-testid={`coze-select-workspace-${item.id}`}
            >
              <Avatar size="default" color="light-blue">{(item.name || item.appKey).slice(0, 1).toUpperCase()}</Avatar>
              <div className="coze-select-workspace__item-meta">
                <strong>{item.name || item.appKey}</strong>
                <span>{item.appKey}</span>
              </div>
              <Button theme="borderless" type="primary">{t("homeEnter")}</Button>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
