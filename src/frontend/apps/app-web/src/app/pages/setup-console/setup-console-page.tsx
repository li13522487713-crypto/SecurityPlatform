import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useAppI18n } from "../../i18n";
import { useBootstrap } from "../../bootstrap-context";
import type { AppMessageKey } from "../../messages";
import { ConsoleAuthGate } from "./console-auth-gate";
import { DashboardTab } from "./dashboard-tab";
import { SystemInitTab } from "./system-init-tab";
import { WorkspaceInitTab } from "./workspace-init-tab";
import { MigrationTab } from "./migration-tab";
import { clearConsoleToken, readConsoleToken } from "./console-token-storage";

export type SetupConsoleTab = "dashboard" | "system-init" | "workspace-init" | "migration" | "repair";

const TAB_LIST: ReadonlyArray<{ id: SetupConsoleTab; labelKey: AppMessageKey }> = [
  { id: "dashboard", labelKey: "setupConsoleTabDashboard" },
  { id: "system-init", labelKey: "setupConsoleTabSystemInit" },
  { id: "workspace-init", labelKey: "setupConsoleTabWorkspaceInit" },
  { id: "migration", labelKey: "setupConsoleTabMigration" },
  { id: "repair", labelKey: "setupConsoleTabRepair" }
];

function parseTabFromParams(value: string | undefined): SetupConsoleTab {
  switch (value) {
    case "system-init":
    case "workspace-init":
    case "migration":
    case "repair":
      return value;
    default:
      return "dashboard";
  }
}

/**
 * 系统初始化与迁移控制台主壳。
 *
 * - URL 形态：`/setup-console/:tab?`，默认 dashboard。
 * - 二次认证：第一次进入 / token 过期 / 主动登出 → 弹 ConsoleAuthGate。
 * - 各 Tab 内容：
 *   - dashboard：M2 已完整实现（系统 / 工作空间 / 迁移 / 目录 4 卡）；
 *   - system-init / workspace-init / migration：M3-M4 替换为完整 Tab；
 *   - repair：M3 末或 M7 提供（重新打开 / 关闭引导 / 强制重置 etc.）。
 */
export function SetupConsolePage() {
  const navigate = useNavigate();
  const params = useParams<{ tab?: string }>();
  const { t } = useAppI18n();
  const { setupConsole, refreshSetupConsole } = useBootstrap();
  const [authenticated, setAuthenticated] = useState(false);
  const [refreshing, setRefreshing] = useState(false);

  const activeTab = useMemo(() => parseTabFromParams(params.tab), [params.tab]);

  // 加载时检查 sessionStorage，已 token 则直接进控制台。
  useEffect(() => {
    const snapshot = readConsoleToken();
    if (snapshot) {
      setAuthenticated(true);
    }
  }, []);

  // 控制台一旦认证通过，立刻拉取最新总览。
  useEffect(() => {
    if (authenticated) {
      void refreshSetupConsole();
    }
  }, [authenticated, refreshSetupConsole]);

  const handleAuthenticated = useCallback(() => {
    setAuthenticated(true);
  }, []);

  const handleLogout = useCallback(() => {
    clearConsoleToken();
    setAuthenticated(false);
  }, []);

  const handleRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await refreshSetupConsole();
    } finally {
      setRefreshing(false);
    }
  }, [refreshSetupConsole]);

  const handleJumpToTab = useCallback(
    (tab: SetupConsoleTab) => {
      navigate(tab === "dashboard" ? "/setup-console" : `/setup-console/${tab}`);
    },
    [navigate]
  );

  return (
    <ConsoleAuthGate authenticated={authenticated} onAuthenticated={handleAuthenticated}>
      <div className="atlas-setup-page" data-testid="setup-console-page">
        <div className="atlas-setup-card" style={{ maxWidth: "min(960px, 100%)" }}>
          <div className="atlas-org-section__header">
            <div>
              <h1 className="atlas-setup-card__title">{t("setupConsoleTitle")}</h1>
              <p className="atlas-setup-card__subtitle">{t("setupConsoleSubtitle")}</p>
            </div>
            <div className="atlas-setup-actions" style={{ gap: 8 }}>
              <button
                type="button"
                className="atlas-button atlas-button--secondary"
                data-testid="setup-console-logout"
                onClick={handleLogout}
              >
                {t("setupConsoleSystemDismiss")}
              </button>
            </div>
          </div>

          <nav className="atlas-tab-bar" data-testid="setup-console-tab-bar">
            {TAB_LIST.map((tab) => {
              const isActive = tab.id === activeTab;
              const target = tab.id === "dashboard" ? "/setup-console" : `/setup-console/${tab.id}`;
              return (
                <Link
                  key={tab.id}
                  to={target}
                  data-testid={`setup-console-tab-${tab.id}`}
                  className={`atlas-tab ${isActive ? "is-active" : ""}`.trim()}
                  aria-current={isActive ? "page" : undefined}
                >
                  {t(tab.labelKey)}
                </Link>
              );
            })}
          </nav>

          {activeTab === "dashboard" ? (
            <DashboardTab
              overview={setupConsole}
              loading={!setupConsole}
              refreshing={refreshing}
              onRefresh={handleRefresh}
              onJumpToTab={(next) => handleJumpToTab(next)}
            />
          ) : null}

          {activeTab === "system-init" ? (
            <SystemInitTab system={setupConsole?.system ?? null} onSnapshotChanged={handleRefresh} />
          ) : null}

          {activeTab === "workspace-init" ? (
            <WorkspaceInitTab
              workspaces={setupConsole?.workspaces ?? []}
              onSnapshotChanged={handleRefresh}
            />
          ) : null}

          {activeTab === "migration" ? (
            <MigrationTab
              activeMigration={setupConsole?.activeMigration ?? null}
              onSnapshotChanged={handleRefresh}
            />
          ) : null}

          {activeTab === "repair" ? (
            <PlaceholderPanel
              testId="setup-console-repair-placeholder"
              titleKey="setupConsoleTabRepair"
              hintKey="setupConsoleM1Notice"
            />
          ) : null}
        </div>
      </div>
    </ConsoleAuthGate>
  );
}

function PlaceholderPanel({
  testId,
  titleKey,
  hintKey
}: {
  testId: string;
  titleKey: AppMessageKey;
  hintKey: AppMessageKey;
}) {
  const { t } = useAppI18n();
  return (
    <section className="atlas-setup-panel" data-testid={testId}>
      <div className="atlas-section-title">{t(titleKey)}</div>
      <p className="atlas-field-hint">{t(hintKey)}</p>
    </section>
  );
}
