import { Button, Space, Typography } from "@douyinfe/semi-ui";
import { IconGlobe, IconExit } from "@douyinfe/semi-icons";
import type { ReactNode } from "react";
import type { AppNavGroup } from "./navigation";
import { useAppI18n } from "./i18n";
import type { AppMessageKey } from "./messages";

interface AppLayoutProps {
  appKey: string;
  runtimeLabel: string;
  userName: string;
  groups: AppNavGroup[];
  activePath: string;
  onNavigate: (path: string) => void;
  onLogout: () => void;
  children: ReactNode;
}

export function AppLayout({
  appKey,
  runtimeLabel,
  userName,
  groups,
  activePath,
  onNavigate,
  onLogout,
  children
}: AppLayoutProps) {
  const { locale, setLocale, t } = useAppI18n();

  return (
    <div className="atlas-app-shell">
      <aside className="atlas-sidebar">
        <div className="atlas-sidebar__brand">
          <div className="atlas-sidebar__logo">A</div>
          <div>
            <div className="atlas-sidebar__title">{t("appTitle")}</div>
            <div className="atlas-sidebar__subtitle">{appKey}</div>
          </div>
        </div>

        <div className="atlas-sidebar__groups">
          {groups.map(group => (
            <section key={group.titleKey} className="atlas-sidebar__group">
              <div className="atlas-sidebar__group-title">{t(group.titleKey as AppMessageKey)}</div>
              <div className="atlas-sidebar__items">
                {group.items.map(item => {
                  const active = activePath.startsWith(item.path);
                  return (
                    <button
                      key={item.path}
                      type="button"
                      className={`atlas-sidebar__item${active ? " is-active" : ""}`}
                      onClick={() => onNavigate(item.path)}
                    >
                      {t(item.labelKey)}
                    </button>
                  );
                })}
              </div>
            </section>
          ))}
        </div>
      </aside>

      <div className="atlas-shell-content">
        <header className="atlas-shell-header">
          <div>
            <Typography.Title heading={5} style={{ margin: 0 }}>
              {t("appSubtitle")}
            </Typography.Title>
            <Typography.Text type="tertiary">{runtimeLabel}</Typography.Text>
          </div>
          <Space spacing={12}>
            <Button
              theme="light"
              icon={<IconGlobe />}
              onClick={() => setLocale(locale === "zh-CN" ? "en-US" : "zh-CN")}
            >
              {locale === "zh-CN" ? t("switchToEnglish") : t("switchToChinese")}
            </Button>
            <div className="atlas-user-pill">{userName}</div>
            <Button theme="light" type="danger" icon={<IconExit />} onClick={onLogout}>
              {t("logout")}
            </Button>
          </Space>
        </header>

        <main className="atlas-shell-main">{children}</main>
      </div>
    </div>
  );
}
