import { useEffect, useMemo, useState } from "react";
import { Avatar, Dropdown, Input, Spin, Tag } from "@douyinfe/semi-ui";
import { IconChevronDown, IconPlus } from "@douyinfe/semi-icons";
import { useNavigate } from "react-router-dom";
import { getTenantId } from "@atlas/shared-react-core/utils";
import { workspaceHomePath, selectWorkspacePath } from "@atlas/app-shell-shared";
import { getWorkspaces, type WorkspaceSummaryDto } from "../../services/api-org-workspaces";
import { useAppI18n } from "../i18n";

interface WorkspaceSwitcherProps {
  workspaceId: string;
  workspaceLabel: string;
}

export function WorkspaceSwitcher({ workspaceId, workspaceLabel }: WorkspaceSwitcherProps) {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const [workspaces, setWorkspaces] = useState<WorkspaceSummaryDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [keyword, setKeyword] = useState("");

  useEffect(() => {
    let cancelled = false;
    const orgId = getTenantId();
    if (!orgId) {
      return () => {
        cancelled = true;
      };
    }
    setLoading(true);
    getWorkspaces(orgId)
      .then(list => {
        if (!cancelled) {
          setWorkspaces(list);
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
  }, [workspaceId]);

  const filtered = useMemo(() => {
    if (!keyword.trim()) {
      return workspaces;
    }
    const lower = keyword.trim().toLowerCase();
    return workspaces.filter(item => item.name?.toLowerCase().includes(lower) || item.appKey.toLowerCase().includes(lower));
  }, [keyword, workspaces]);

  return (
    <Dropdown
      trigger="click"
      position="bottomLeft"
      render={(
        <div className="coze-workspace-switcher" data-testid="coze-workspace-switcher-panel">
          <div className="coze-workspace-switcher__head">
            <span>{t("cozeShellWorkspaceSwitcherTitle")}</span>
            <button
              type="button"
              className="coze-workspace-switcher__create"
              onClick={() => navigate(selectWorkspacePath())}
              data-testid="coze-workspace-switcher-create"
            >
              <IconPlus size="small" /> {t("cozeShellWorkspaceSwitcherCreate")}
            </button>
          </div>
          <Input
            value={keyword}
            onChange={value => setKeyword(value)}
            placeholder={t("cozeShellWorkspaceSwitcherSearch")}
            size="small"
            showClear
          />
          <div className="coze-workspace-switcher__list">
            {loading ? (
              <div className="coze-workspace-switcher__loading"><Spin size="small" /></div>
            ) : filtered.length === 0 ? (
              <div className="coze-workspace-switcher__empty">{t("cozeShellWorkspaceSwitcherEmpty")}</div>
            ) : (
              filtered.map(item => (
                <button
                  key={item.id}
                  type="button"
                  className={`coze-workspace-switcher__item${item.id === workspaceId ? " is-active" : ""}`}
                  onClick={() => navigate(workspaceHomePath(item.id))}
                  data-testid={`coze-workspace-switcher-item-${item.id}`}
                >
                  <Avatar size="extra-small" color="light-blue">
                    {(item.name || item.appKey).slice(0, 1).toUpperCase()}
                  </Avatar>
                  <div className="coze-workspace-switcher__item-meta">
                    <span className="coze-workspace-switcher__item-name">{item.name || item.appKey}</span>
                    <span className="coze-workspace-switcher__item-sub">{item.appKey}</span>
                  </div>
                  {item.id === workspaceId ? (
                    <Tag size="small" color="blue">{t("cozeShellWorkspaceSwitcherCurrent")}</Tag>
                  ) : null}
                </button>
              ))
            )}
          </div>
        </div>
      )}
    >
      <button type="button" className="coze-workspace-switcher__trigger" data-testid="coze-workspace-switcher-trigger">
        <Avatar size="extra-small" color="light-blue">{(workspaceLabel || "W").slice(0, 1).toUpperCase()}</Avatar>
        <span>{workspaceLabel || t("cozeShellWorkspaceSwitcherTitle")}</span>
        <IconChevronDown size="small" />
      </button>
    </Dropdown>
  );
}
