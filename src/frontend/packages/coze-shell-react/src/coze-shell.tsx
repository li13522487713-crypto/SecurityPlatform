import { useState, type ReactNode } from "react";
import { Avatar, Button, Dropdown, Space, Typography, Tag } from "@douyinfe/semi-ui";
import { IconChevronLeft, IconGlobe, IconTreeTriangleDown, IconExit, IconPlus, IconMinus } from "@douyinfe/semi-icons";
import type {
  CozeHeaderAction,
  CozeNavItem,
  CozeNavSection,
} from "./types";

interface CozeShellProps {
  appKey: string;
  backPath?: string;
  workspaceLabel: string;
  activePath: string;
  navSections: CozeNavSection[];
  headerTitle: string;
  headerSubtitle?: string;
  localeLabel: string;
  userName: string;
  extraActions?: CozeHeaderAction[];
  profileLabel?: string;
  logoutLabel?: string;
  onNavigate: (path: string) => void;
  onToggleLocale: () => void;
  onOpenProfile?: () => void;
  onLogout: () => void;
  children: ReactNode;
}

function isActiveNavItem(item: CozeNavItem, activePath: string): boolean {
  if (activePath === item.path || activePath.startsWith(`${item.path}/`)) {
    return true;
  }

  const [activePathname] = activePath.split("?");
  const [itemPathname] = item.path.split("?");
  return activePathname === itemPathname && activePath.includes(item.path);
}

export function CozeShell({
  appKey,
  backPath,
  workspaceLabel,
  activePath,
  navSections,
  headerTitle,
  headerSubtitle,
  localeLabel,
  userName,
  extraActions,
  profileLabel = "个人中心",
  logoutLabel = "退出登录",
  onNavigate,
  onToggleLocale,
  onOpenProfile,
  onLogout,
  children,
}: CozeShellProps) {
  const [expandedSections, setExpandedSections] = useState<Record<string, boolean>>({});
  const [collapsed, setCollapsed] = useState(false);

  return (
    <div className={`coze-shell${collapsed ? " coze-shell--sidebar-collapsed" : ""}`}>
      <aside className="coze-shell__sidebar" data-testid="app-sidebar">
        <div className="coze-shell__sidebar-toolbar">
          <button
            type="button"
            className="coze-shell__back"
            onClick={() => onNavigate(backPath ?? `/apps/${encodeURIComponent(appKey)}`)}
            data-testid="app-shell-back"
            title="返回应用"
          >
            <IconChevronLeft size="large" />
          </button>

          <button
            type="button"
            className="coze-shell__collapse"
            onClick={() => setCollapsed(current => !current)}
            data-testid="app-sidebar-toggle"
            title={collapsed ? "展开侧边栏" : "收起侧边栏"}
          >
            {collapsed ? <IconPlus size="small" /> : <IconMinus size="small" />}
          </button>
        </div>

        <div className="coze-shell__brand">
          <Avatar size="small" color="light-blue">
            {(workspaceLabel || appKey).slice(0, 1).toUpperCase()}
          </Avatar>
          <div className="coze-shell__brand-copy">
            <span className="coze-shell__brand-title">{workspaceLabel}</span>
            <span className="coze-shell__brand-subtitle">{appKey}</span>
          </div>
        </div>

        <div className="coze-shell__sidebar-nav">
          {navSections.map(section => {
            const overflowItems = section.overflowItems ?? [];
            const activeOverflow = overflowItems.some(item => isActiveNavItem(item, activePath));
            const expanded = expandedSections[section.key] || activeOverflow;

            return (
              <section key={section.key} className="coze-shell__section">
                <div className="coze-shell__section-title">{section.title}</div>
                <div className="coze-shell__section-items">
                  {section.items.map(item => {
                    const active = isActiveNavItem(item, activePath);
                    return (
                      <button
                        key={item.key}
                        type="button"
                        className={`coze-shell__secondary-item${active ? " is-active" : ""}`}
                        onClick={() => onNavigate(item.path)}
                        data-testid={item.testId}
                        title={item.label}
                      >
                        {item.icon ? <span className="coze-shell__secondary-item-icon">{item.icon}</span> : null}
                        <span className="coze-shell__secondary-item-label">{item.label}</span>
                        {item.badge ? <Tag size="small" color="light-blue">{item.badge}</Tag> : null}
                      </button>
                    );
                  })}

                  {overflowItems.length > 0 ? (
                    <>
                      <button
                        type="button"
                        className={`coze-shell__secondary-item coze-shell__secondary-item--more${expanded ? " is-expanded" : ""}`}
                        data-testid={section.overflowTestId}
                        title={section.overflowLabel ?? "更多"}
                        onClick={() => {
                          setExpandedSections(current => ({
                            ...current,
                            [section.key]: !expanded
                          }));
                        }}
                      >
                        <span className="coze-shell__secondary-item-icon">
                          {expanded ? <IconMinus /> : <IconPlus />}
                        </span>
                        <span className="coze-shell__secondary-item-label">{section.overflowLabel ?? "更多"}</span>
                      </button>

                      {expanded ? (
                        <div className="coze-shell__section-overflow">
                          {overflowItems.map(item => {
                            const active = isActiveNavItem(item, activePath);
                            return (
                              <button
                                key={item.key}
                                type="button"
                                className={`coze-shell__secondary-item coze-shell__secondary-item--overflow${active ? " is-active" : ""}`}
                                onClick={() => onNavigate(item.path)}
                                data-testid={item.testId}
                                title={item.label}
                              >
                                {item.icon ? <span className="coze-shell__secondary-item-icon">{item.icon}</span> : null}
                                <span className="coze-shell__secondary-item-label">{item.label}</span>
                                {item.badge ? <Tag size="small" color="light-blue">{item.badge}</Tag> : null}
                              </button>
                            );
                          })}
                        </div>
                      ) : null}
                    </>
                  ) : null}
                </div>
              </section>
            );
          })}
        </div>
      </aside>

      <div className="coze-shell__content">
        <header className="coze-shell__header">
          <div className="coze-shell__header-copy">
            <Typography.Title heading={4} style={{ margin: 0 }}>
              <span data-testid="app-shell-header-title">{headerTitle}</span>
            </Typography.Title>
            {headerSubtitle ? (
              <Typography.Text type="tertiary">
                <span data-testid="app-shell-header-subtitle">{headerSubtitle}</span>
              </Typography.Text>
            ) : null}
          </div>

          <Space spacing={12}>
            {(extraActions ?? []).map(action => (
              <Button
                key={action.key}
                theme="light"
                icon={action.icon}
                onClick={action.onClick}
                data-testid={action.testId}
              >
                {action.label}
              </Button>
            ))}

            <Button
              theme="borderless"
              icon={<IconGlobe />}
              onClick={onToggleLocale}
              data-testid="app-shell-toggle-locale"
            >
              {localeLabel}
            </Button>

            <Dropdown
              position="bottomRight"
              render={
                <div className="coze-shell__user-menu" role="menu">
                  {onOpenProfile ? (
                    <button
                      type="button"
                      className="coze-shell__user-menu-item"
                      onClick={onOpenProfile}
                      data-testid="app-header-menu-profile"
                    >
                      {profileLabel}
                    </button>
                  ) : null}
                  <button
                    type="button"
                    className="coze-shell__user-menu-item"
                    onClick={onLogout}
                    data-testid="app-header-menu-logout"
                  >
                    <IconExit />
                    <span>{logoutLabel}</span>
                  </button>
                </div>
              }
            >
              <Button theme="light" icon={<IconTreeTriangleDown />} data-testid="app-header-user-menu">
                {userName}
              </Button>
            </Dropdown>
          </Space>
        </header>

        <main className="coze-shell__main">{children}</main>
      </div>
    </div>
  );
}
