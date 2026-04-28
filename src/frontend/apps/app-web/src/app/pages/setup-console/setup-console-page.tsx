import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Button, Tabs } from "@douyinfe/semi-ui";
import { useAppI18n } from "../../i18n";
import { useBootstrap } from "../../bootstrap-context";
import { FormCard, PageShell } from "../../_shared";
import type { AppMessageKey } from "../../messages";
import { ConsoleAuthGate } from "./console-auth-gate";
import { DashboardTab } from "./dashboard-tab";
import { SystemInitTab } from "./system-init-tab";
import { WorkspaceInitTab } from "./workspace-init-tab";
import { MigrationTab } from "./migration-tab";
import { RepairTab } from "./repair-tab";
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

  useEffect(() => {
    const snapshot = readConsoleToken();
    if (snapshot) {
      setAuthenticated(true);
    }
  }, []);

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

  const handleTabChange = useCallback(
    (key: string) => {
      handleJumpToTab(parseTabFromParams(key));
    },
    [handleJumpToTab]
  );

  return (
    <ConsoleAuthGate authenticated={authenticated} onAuthenticated={handleAuthenticated}>
      <PageShell centered maxWidth={960} testId="setup-console-page">
        <FormCard
          title={t("setupConsoleTitle")}
          subtitle={t("setupConsoleSubtitle")}
          headerExtra={
            <Button
              type="tertiary"
              theme="light"
              data-testid="setup-console-logout"
              onClick={handleLogout}
            >
              {t("setupConsoleSystemDismiss")}
            </Button>
          }
        >
          <div data-testid="setup-console-tab-bar">
            <Tabs type="line" activeKey={activeTab} onChange={handleTabChange} keepDOM={false}>
              {TAB_LIST.map((tab) => (
                <Tabs.TabPane
                  key={tab.id}
                  itemKey={tab.id}
                  tab={
                    <span data-testid={`setup-console-tab-${tab.id}`}>{t(tab.labelKey)}</span>
                  }
                >
                  {tab.id === "dashboard" ? (
                    <DashboardTab
                      overview={setupConsole}
                      loading={!setupConsole}
                      refreshing={refreshing}
                      onRefresh={handleRefresh}
                      onJumpToTab={(next) => handleJumpToTab(next)}
                    />
                  ) : null}
                  {tab.id === "system-init" ? (
                    <SystemInitTab
                      system={setupConsole?.system ?? null}
                      onSnapshotChanged={handleRefresh}
                    />
                  ) : null}
                  {tab.id === "workspace-init" ? (
                    <WorkspaceInitTab
                      workspaces={setupConsole?.workspaces ?? []}
                      onSnapshotChanged={handleRefresh}
                    />
                  ) : null}
                  {tab.id === "migration" ? (
                    <MigrationTab
                      activeMigration={setupConsole?.activeMigration ?? null}
                      onSnapshotChanged={handleRefresh}
                    />
                  ) : null}
                  {tab.id === "repair" ? <RepairTab /> : null}
                </Tabs.TabPane>
              ))}
            </Tabs>
          </div>
        </FormCard>
      </PageShell>
    </ConsoleAuthGate>
  );
}
