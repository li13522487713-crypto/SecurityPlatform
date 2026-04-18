import { useEffect, useState } from "react";
import { Avatar, Button, Card, Empty, List, Typography } from "@douyinfe/semi-ui";
import { useNavigate } from "react-router-dom";
import { getTenantId } from "@atlas/shared-react-core/utils";
import { workspaceHomePath } from "@atlas/app-shell-shared";
import { useAppI18n } from "../i18n";
import { useAuth } from "../auth-context";
import { getWorkspaces, type WorkspaceSummaryDto } from "../../services/api-org-workspaces";
import { rememberLastWorkspaceId } from "../layouts/workspace-shell";
import { PageShell } from "../_shared";

const { Title, Text } = Typography;

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
    return <PageShell loading loadingTip={t("loading")} />;
  }

  return (
    <PageShell centered maxWidth={640} testId="coze-select-workspace-page">
      <Card bodyStyle={{ padding: 24 }}>
        <Title heading={3}>{t("cozeShellWorkspaceSwitcherTitle")}</Title>
        <Text type="tertiary">{t("cozeShellWorkspaceSwitcherCurrent")}</Text>

        {workspaces.length === 0 ? (
          <div style={{ marginTop: 32 }}>
            <Empty description={t("cozeShellWorkspaceSwitcherEmpty")} />
          </div>
        ) : (
          <List
            style={{ marginTop: 16 }}
            dataSource={workspaces}
            renderItem={item => (
              <List.Item
                main={
                  <button
                    type="button"
                    onClick={() => {
                      rememberLastWorkspaceId(item.id);
                      navigate(workspaceHomePath(item.id));
                    }}
                    data-testid={`coze-select-workspace-${item.id}`}
                    style={{
                      width: "100%",
                      display: "flex",
                      alignItems: "center",
                      gap: 12,
                      padding: "8px 4px",
                      border: "none",
                      background: "transparent",
                      cursor: "pointer",
                      textAlign: "left"
                    }}
                  >
                    <Avatar size="default" color="light-blue">
                      {(item.name || item.appKey).slice(0, 1).toUpperCase()}
                    </Avatar>
                    <div style={{ flex: 1, display: "flex", flexDirection: "column" }}>
                      <strong>{item.name || item.appKey}</strong>
                      <Text type="tertiary" style={{ fontSize: 12 }}>
                        {item.appKey}
                      </Text>
                    </div>
                  </button>
                }
                extra={
                  <Button
                    theme="borderless"
                    type="primary"
                    onClick={() => {
                      rememberLastWorkspaceId(item.id);
                      navigate(workspaceHomePath(item.id));
                    }}
                  >
                    {t("homeEnter")}
                  </Button>
                }
              />
            )}
          />
        )}
      </Card>
    </PageShell>
  );
}
