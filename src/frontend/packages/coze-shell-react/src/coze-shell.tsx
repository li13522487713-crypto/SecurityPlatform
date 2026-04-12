import type { ReactNode } from "react";
import { Avatar, Button, Dropdown, Space, Typography } from "@douyinfe/semi-ui";
import { IconChevronLeft, IconGlobe, IconTreeTriangleDown, IconExit } from "@douyinfe/semi-icons";
import type {
  CozeHeaderAction,
  CozePrimaryNavItem,
  CozeSecondaryNavItem,
  CozeSecondaryNavSection,
} from "./types";

interface CozeShellProps {
  appKey: string;
  workspaceLabel: string;
  activePath: string;
  activePrimaryKey: string;
  primaryItems: CozePrimaryNavItem[];
  secondarySections: CozeSecondaryNavSection[];
  headerTitle: string;
  headerSubtitle?: string;
  localeLabel: string;
  userName: string;
  extraActions?: CozeHeaderAction[];
  onNavigate: (path: string) => void;
  onToggleLocale: () => void;
  onOpenProfile?: () => void;
  onLogout: () => void;
  children: ReactNode;
}

function isActiveSecondary(item: CozeSecondaryNavItem, activePath: string): boolean {
  return activePath === item.path || activePath.startsWith(`${item.path}/`);
}

function isActivePrimary(item: CozePrimaryNavItem, activePath: string): boolean {
  if (activePath === item.path || activePath.startsWith(`${item.path}/`)) {
    return true;
  }

  return (item.activePrefixes ?? []).some(prefix => activePath.startsWith(prefix));
}

export function CozeShell({
  appKey,
  workspaceLabel,
  activePath,
  activePrimaryKey,
  primaryItems,
  secondarySections,
  headerTitle,
  headerSubtitle,
  localeLabel,
  userName,
  extraActions,
  onNavigate,
  onToggleLocale,
  onOpenProfile,
  onLogout,
  children,
}: CozeShellProps) {
  return (
    <div className="coze-shell">
      <aside className="coze-shell__primary" data-testid="app-primary-nav">
        <button
          type="button"
          className="coze-shell__back"
          onClick={() => onNavigate(`/apps/${encodeURIComponent(appKey)}`)}
          data-testid="app-shell-back"
        >
          <IconChevronLeft size="large" />
        </button>

        <div className="coze-shell__brand">
          <Avatar size="small" color="light-blue">
            {(workspaceLabel || appKey).slice(0, 1).toUpperCase()}
          </Avatar>
          <div className="coze-shell__brand-copy">
            <span className="coze-shell__brand-title">{workspaceLabel}</span>
            <span className="coze-shell__brand-subtitle">{appKey}</span>
          </div>
        </div>

        <nav className="coze-shell__primary-nav">
          {primaryItems.map(item => {
            const active = item.key === activePrimaryKey || isActivePrimary(item, activePath);
            return (
              <button
                key={item.key}
                type="button"
                className={`coze-shell__primary-item${active ? " is-active" : ""}`}
                onClick={() => onNavigate(item.path)}
                data-testid={item.testId}
                title={item.label}
              >
                <span className="coze-shell__primary-item-icon">{item.icon}</span>
                <span className="coze-shell__primary-item-label">{item.label}</span>
              </button>
            );
          })}
        </nav>
      </aside>

      <aside className="coze-shell__secondary" data-testid="app-sidebar">
        <div className="coze-shell__secondary-header">
          <Typography.Title heading={6} style={{ margin: 0 }}>
            {headerTitle}
          </Typography.Title>
          {headerSubtitle ? <Typography.Text type="tertiary">{headerSubtitle}</Typography.Text> : null}
        </div>

        <div className="coze-shell__secondary-sections">
          {secondarySections.map(section => (
            <section key={section.key} className="coze-shell__section">
              <div className="coze-shell__section-title">{section.title}</div>
              <div className="coze-shell__section-items">
                {section.items.map(item => {
                  const active = isActiveSecondary(item, activePath);
                  return (
                    <button
                      key={item.key}
                      type="button"
                      className={`coze-shell__secondary-item${active ? " is-active" : ""}`}
                      onClick={() => onNavigate(item.path)}
                      data-testid={item.testId}
                    >
                      {item.label}
                    </button>
                  );
                })}
              </div>
            </section>
          ))}
        </div>
      </aside>

      <div className="coze-shell__content">
        <header className="coze-shell__header">
          <div className="coze-shell__header-copy">
            <Typography.Title heading={4} style={{ margin: 0 }}>
              {headerTitle}
            </Typography.Title>
            {headerSubtitle ? <Typography.Text type="tertiary">{headerSubtitle}</Typography.Text> : null}
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
                <Dropdown.Menu>
                  {onOpenProfile ? (
                    <Dropdown.Item onClick={onOpenProfile} data-testid="app-header-menu-profile">
                      个人中心
                    </Dropdown.Item>
                  ) : null}
                  <Dropdown.Item icon={<IconExit />} onClick={onLogout} data-testid="app-header-menu-logout">
                    退出登录
                  </Dropdown.Item>
                </Dropdown.Menu>
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
