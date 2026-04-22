import { type ReactNode } from "react";
import { Tabs, TabPane, Typography, Tag } from "@douyinfe/semi-ui";
import { IconSetting, IconGlobe } from "@douyinfe/semi-icons";
import { useNavigate } from "react-router-dom";
import { workspaceSettingsModelsPath, workspaceSettingsPublishPath } from "@atlas/app-shell-shared";
import { useAppI18n } from "../i18n";
import { useWorkspaceContext } from "../workspace-context";

/** 根据工作空间名称生成一个固定的渐变色索引 */
function getWorkspaceGradient(name: string): string {
  const gradients = [
    "linear-gradient(135deg, #667eea 0%, #764ba2 100%)",
    "linear-gradient(135deg, #f093fb 0%, #f5576c 100%)",
    "linear-gradient(135deg, #4facfe 0%, #00f2fe 100%)",
    "linear-gradient(135deg, #43e97b 0%, #38f9d7 100%)",
    "linear-gradient(135deg, #fa709a 0%, #fee140 100%)",
    "linear-gradient(135deg, #a18cd1 0%, #fbc2eb 100%)",
    "linear-gradient(135deg, #ffecd2 0%, #fcb69f 100%)",
    "linear-gradient(135deg, #a1c4fd 0%, #c2e9fb 100%)",
  ];
  let hash = 0;
  for (let i = 0; i < name.length; i++) {
    hash = (hash * 31 + name.charCodeAt(i)) & 0xffff;
  }
  return gradients[hash % gradients.length];
}

/** 取工作空间名称首字（中英文兼容） */
function getInitials(name: string): string {
  if (!name) return "W";
  const trimmed = name.trim();
  if (/[\u4e00-\u9fa5]/.test(trimmed[0])) {
    return trimmed.slice(0, 1);
  }
  const parts = trimmed.split(/\s+/);
  return parts.length > 1
    ? `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase()
    : trimmed[0].toUpperCase();
}

export function WorkspaceSettingsLayout({
  children,
  activeTab,
}: {
  children: ReactNode;
  activeTab: "publish" | "models";
}) {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();

  const displayName = workspace.name || workspace.appKey || t("cozeShellWorkspaceSwitcherScopeLabel");
  const gradient = getWorkspaceGradient(displayName);
  const initials = getInitials(displayName);

  return (
    <div
      className="coze-page coze-settings-page"
      data-testid="coze-settings-page"
      style={{ background: "var(--semi-color-bg-0)", minHeight: "100vh" }}
    >
      {/* ── Hero Banner ── */}
      <div
        style={{
          position: "relative",
          overflow: "hidden",
          background: "linear-gradient(135deg, #0f0c29 0%, #302b63 50%, #24243e 100%)",
          padding: "40px 40px 0",
        }}
      >
        {/* 背景光晕装饰 */}
        <div
          aria-hidden="true"
          style={{
            position: "absolute",
            inset: 0,
            backgroundImage:
              "radial-gradient(ellipse at 20% 50%, rgba(102,126,234,0.25) 0%, transparent 55%), radial-gradient(ellipse at 80% 20%, rgba(240,147,251,0.15) 0%, transparent 50%)",
            pointerEvents: "none",
          }}
        />

        {/* Workspace 信息卡片区 */}
        <div style={{ position: "relative", display: "flex", alignItems: "center", gap: 20, marginBottom: 32 }}>
          {/* 头像 */}
          <div
            style={{
              width: 56,
              height: 56,
              borderRadius: 16,
              background: gradient,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: initials.length > 1 ? 18 : 22,
              fontWeight: 700,
              color: "#fff",
              letterSpacing: initials.length > 1 ? -1 : 0,
              boxShadow: "0 8px 24px rgba(0,0,0,0.3)",
              flexShrink: 0,
            }}
          >
            {initials}
          </div>

          {/* 名称 + role */}
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 6 }}>
              <Typography.Title
                heading={4}
                style={{
                  margin: 0,
                  color: "#fff",
                  fontWeight: 700,
                  fontSize: 20,
                  lineHeight: 1.3,
                }}
              >
                {displayName}
              </Typography.Title>
              <Tag
                size="small"
                style={{
                  background: "rgba(255,255,255,0.15)",
                  border: "1px solid rgba(255,255,255,0.25)",
                  color: "#fff",
                  borderRadius: 20,
                  fontWeight: 500,
                  fontSize: 11,
                  backdropFilter: "blur(4px)",
                }}
              >
                {t("workspaceSettingsRoleOwner")}
              </Tag>
            </div>
            <Typography.Text style={{ color: "rgba(255,255,255,0.55)", fontSize: 13 }}>
              {t("cozeSettingsTitle")}
            </Typography.Text>
          </div>
        </div>

        {/* 主 Tab 导航 — 置于 banner 底部，紧贴内容区 */}
        <div style={{ position: "relative" }}>
          <Tabs
            type="line"
            activeKey={activeTab}
            onChange={(key) => {
              if (key === "models") {
                navigate(workspaceSettingsModelsPath(workspace.id));
              } else if (key === "publish") {
                navigate(workspaceSettingsPublishPath(workspace.id));
              }
            }}
            style={{ marginBottom: 0 }}
            contentStyle={{ display: "none" }}
            className="workspace-settings-tabs"
          >
            <TabPane
              tab={
                <span style={{ display: "inline-flex", alignItems: "center", gap: 6, padding: "0 4px" }}>
                  <IconGlobe size="small" />
                  {t("cozeSettingsTabPublish")}
                </span>
              }
              itemKey="publish"
            />
            <TabPane
              tab={
                <span style={{ display: "inline-flex", alignItems: "center", gap: 6, padding: "0 4px" }}>
                  <IconSetting size="small" />
                  {t("cozeSettingsTabModels")}
                </span>
              }
              itemKey="models"
            />
          </Tabs>
        </div>

        {/* Tab 底部高亮线覆盖层（让 Semi line tab 在深色背景下显示正确） */}
        <style>{`
          .workspace-settings-tabs .semi-tabs-bar {
            border-bottom: 1px solid rgba(255,255,255,0.12);
            margin-bottom: 0;
          }
          .workspace-settings-tabs .semi-tabs-tab {
            color: rgba(255,255,255,0.55) !important;
            font-size: 14px;
            font-weight: 500;
            padding-bottom: 12px;
          }
          .workspace-settings-tabs .semi-tabs-tab:hover {
            color: rgba(255,255,255,0.9) !important;
          }
          .workspace-settings-tabs .semi-tabs-tab-active {
            color: #fff !important;
            font-weight: 600;
          }
          .workspace-settings-tabs .semi-tabs-indicator {
            background-color: #fff !important;
            height: 2px !important;
          }
          .workspace-settings-tabs .semi-tabs-content {
            display: none !important;
          }
        `}</style>
      </div>

      {/* ── 内容区 ── */}
      <div style={{ padding: "28px 40px 40px" }}>
        {children}
      </div>
    </div>
  );
}
