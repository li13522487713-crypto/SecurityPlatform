import { useEffect, useMemo, useState } from "react";
import { Avatar, Dropdown, Input, Spin, Tag } from "@douyinfe/semi-ui";
import { IconChevronDown, IconPlus, IconSearch, IconArrowUp } from "@douyinfe/semi-icons";
import { useNavigate } from "react-router-dom";
import { getTenantId } from "@atlas/shared-react-core/utils";
import { orgWorkspacesPath, workspaceHomePath } from "@atlas/app-shell-shared";
import { getWorkspaces, type WorkspaceSummaryDto } from "../../services/api-org-workspaces";
import { useAppI18n } from "../i18n";

interface WorkspaceSwitcherProps {
  workspaceId: string;
  workspaceLabel: string;
}

// Helper to generate a consistent color based on string
function getAvatarColor(name: string) {
  const colors = ["#8b5cf6", "#10b981", "#f59e0b", "#ef4444", "#3b82f6", "#ec4899", "#14b8a6"];
  let hash = 0;
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  }
  return colors[Math.abs(hash) % colors.length];
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

  const orgId = getTenantId();
  const currentWorkspace = useMemo(
    () => workspaces.find(item => item.id === workspaceId) ?? null,
    [workspaceId, workspaces]
  );

  const openCreateWorkspace = () => {
    if (!orgId) {
      return;
    }
    navigate(`${orgWorkspacesPath(orgId)}?create=1`);
  };

  const renderAvatar = (name: string, size: "small" | "large" = "small") => {
    const isPersonal = name.includes("个人空间") || name.toLowerCase() === "personal";
    if (isPersonal) {
      return (
        <div className={`bg-blue-100 rounded-full flex items-center justify-center shrink-0 ${size === "large" ? "size-[32px]" : "size-[24px]"}`}>
          <IconArrowUp className="text-blue-500" size={size === "large" ? "default" : "small"} />
        </div>
      );
    }
    return (
      <div 
        className={`rounded-full flex items-center justify-center shrink-0 text-white font-bold ${size === "large" ? "size-[32px] text-[14px]" : "size-[24px] text-[12px]"}`}
        style={{ backgroundColor: getAvatarColor(name) }}
      >
        {name.slice(0, 1).toUpperCase()}
      </div>
    );
  };

  return (
    <Dropdown
      trigger="click"
      position="bottomLeft"
      render={(
        <div className="w-[280px] bg-white rounded-[16px] shadow-[0_4px_24px_rgba(0,0,0,0.1)] p-[12px] flex flex-col gap-[12px]" data-testid="coze-workspace-switcher-panel">
          <div className="flex items-center justify-between">
            <span className="text-[#1f2329] text-[14px] font-medium">{t("cozeShellWorkspaceSwitcherManage")}</span>
            <button 
              type="button" 
              className="text-blue-500 hover:text-blue-600 text-[12px] bg-transparent border-none cursor-pointer p-0 font-medium" 
              onClick={() => orgId && navigate(orgWorkspacesPath(orgId))}
            >
              {t("cozeShellWorkspaceSwitcherManageLink")}
            </button>
          </div>

          {currentWorkspace ? (
            <div 
              className="bg-[#eff6ff] border border-blue-200 rounded-[12px] p-[12px] flex items-center justify-between cursor-pointer hover:bg-blue-50 transition-colors"
              onClick={() => navigate(workspaceHomePath(currentWorkspace.id))}
            >
              <div className="flex items-center gap-[10px]">
                {renderAvatar(currentWorkspace.name || currentWorkspace.appKey, "large")}
                <div className="flex flex-col gap-[2px]">
                  <span className="text-[#1f2329] text-[14px] font-medium leading-none">
                    {currentWorkspace.name || currentWorkspace.appKey}
                  </span>
                  <span className="bg-[#e5e6eb] text-[#4a5565] text-[10px] px-[6px] py-[2px] rounded-[4px] self-start leading-none mt-[2px]">
                    所有者
                  </span>
                </div>
              </div>
              <span className="text-blue-500 font-bold text-[14px]">✓</span>
            </div>
          ) : null}

          <Input
            value={keyword}
            onChange={value => setKeyword(value)}
            placeholder={t("cozeShellWorkspaceSwitcherSearch")}
            prefix={<IconSearch className="text-gray-400 ml-[4px]" />}
            className="!bg-gray-50 !border-transparent !rounded-[8px] hover:!bg-gray-100 focus-within:!bg-white focus-within:!border-blue-500 h-[32px]"
            showClear
          />

          <div className="flex flex-col gap-[2px] max-h-[220px] overflow-y-auto mt-[-4px]">
            {loading ? (
              <div className="p-[16px] text-center"><Spin size="small" /></div>
            ) : filtered.length === 0 ? (
              <div className="p-[16px] text-center text-gray-400 text-[12px]">{t("cozeShellWorkspaceSwitcherEmpty")}</div>
            ) : (
              filtered.map(item => {
                const isActive = item.id === workspaceId;
                return (
                  <button
                    key={item.id}
                    type="button"
                    className={`flex items-center justify-between w-full p-[8px] rounded-[8px] border-none cursor-pointer transition-colors ${isActive ? "bg-[#eff6ff]" : "bg-transparent hover:bg-gray-50"}`}
                    onClick={() => navigate(workspaceHomePath(item.id))}
                    data-testid={`coze-workspace-switcher-item-${item.id}`}
                  >
                    <div className="flex items-center gap-[10px]">
                      {renderAvatar(item.name || item.appKey, "small")}
                      <span className={`text-[13px] ${isActive ? "text-blue-600 font-medium" : "text-[#1f2329]"}`}>
                        {item.name || item.appKey}
                      </span>
                    </div>
                    {isActive ? <span className="text-blue-500 font-bold text-[14px]">✓</span> : null}
                  </button>
                );
              })
            )}
          </div>

          <div className="border-t border-[#f3f4f6] mx-[-12px] mt-[4px] mb-[-12px] pt-[4px] pb-[4px] px-[8px]">
            <button 
              type="button" 
              className="flex items-center gap-[8px] w-full p-[8px] rounded-[8px] border-none bg-transparent cursor-pointer hover:bg-gray-50 transition-colors text-[#4a5565]" 
              onClick={openCreateWorkspace}
            >
              <IconPlus className="text-gray-400" size="small" />
              <span className="text-[13px] font-medium">{t("cozeShellWorkspaceSwitcherCreateTeam")}</span>
              <Tag size="small" color="orange" className="!bg-orange-50 !text-orange-500 !border-orange-200 ml-auto">{t("cozeShellWorkspaceSwitcherCreateTeamBadge")}</Tag>
            </button>
          </div>
        </div>
      )}
    >
      <button 
        type="button" 
        className="flex items-center justify-between w-full bg-white border border-[#e5e6eb] rounded-[12px] px-[12px] py-[8px] hover:border-blue-500 transition-colors cursor-pointer outline-none" 
        data-testid="coze-workspace-switcher-trigger"
      >
        <div className="flex items-center gap-[8px]">
          {renderAvatar(workspaceLabel || t("cozeShellWorkspaceSwitcherTitle"), "small")}
          <span className="text-[#1f2329] text-[14px] font-medium truncate max-w-[120px]">
            {workspaceLabel || t("cozeShellWorkspaceSwitcherTitle")}
          </span>
        </div>
        <IconChevronDown className="text-gray-400" size="small" />
      </button>
    </Dropdown>
  );
}