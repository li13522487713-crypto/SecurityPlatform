import { createContext, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { getAccessToken, getTenantId } from "@atlas/shared-react-core/utils";
import { getSetupState, type SetupStateResponse } from "../services/api-setup";
import { getConfiguredAppKey, rememberConfiguredAppKey } from "../services/api-core";
import { resolveAppInstanceId } from "../services/app-instance-context";
import { getSetupConsoleOverview } from "../services/mock";
import type {
  SetupConsoleOverviewDto,
  SystemSetupStateDto,
  WorkspaceSetupStateDto
} from "../services/api-setup-console";

export interface BootstrapState {
  loading: boolean;
  platformReady: boolean;
  appReady: boolean;
  appKey: string;
  appInstanceId: string | null;
  spaceId: string;
  workspaceLabel: string;
  appStatus: string;
  /** 控制台总览（M1 mock；M5 切真接口）。系统初始化与工作空间初始化的真理来源。 */
  setupConsole: SetupConsoleOverviewDto | null;
  /** 控制台总览中的系统级状态快照，便于路由守卫与 UI 直接消费。 */
  systemInit: SystemSetupStateDto | null;
  /** 当前所有工作空间的初始化状态快照。 */
  workspaceInits: WorkspaceSetupStateDto[];
  refresh: () => Promise<void>;
  refreshSetupConsole: () => Promise<void>;
}

const BootstrapContext = createContext<BootstrapState | null>(null);

export function BootstrapProvider({ children }: { children: ReactNode }) {
  const [loading, setLoading] = useState(true);
  const [setupState, setSetupState] = useState<SetupStateResponse | null>(null);
  const [appInstanceId, setAppInstanceId] = useState<string | null>(null);
  const [workspaceLabel, setWorkspaceLabel] = useState("Workspace");
  const [setupConsole, setSetupConsole] = useState<SetupConsoleOverviewDto | null>(null);
  const platformReady = setupState?.platformSetupCompleted === true;
  const appReady = setupState?.appSetupCompleted === true;
  const appStatus = String(setupState?.appStatus ?? "").trim();
  const appKey = String(
    setupState?.appKey ?? setupState?.configuredAppKey ?? getConfiguredAppKey()
  ).trim();

  const refreshSetupConsole = async () => {
    try {
      const response = await getSetupConsoleOverview();
      if (response.success && response.data) {
        setSetupConsole(response.data);
      }
    } catch {
      // mock 失败时不影响其它 bootstrap 流程；M5 真接口失败按 401 跳二次认证。
      setSetupConsole(null);
    }
  };

  const refresh = async () => {
    setLoading(true);
    try {
      const response = await getSetupState();
      setSetupState(response.data ?? null);
    } catch {
      setSetupState(null);
      setAppInstanceId(null);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void refresh();
    void refreshSetupConsole();
  }, []);

  useEffect(() => {
    if (appKey) {
      rememberConfiguredAppKey(appKey);
    }
  }, [appKey]);

  useEffect(() => {
    let cancelled = false;
    const hasAuthContext = Boolean(getAccessToken() && getTenantId());

    const syncWorkspaceContext = async () => {
      if (appReady && appKey && hasAuthContext) {
        setWorkspaceLabel(appKey);
        try {
          const resolvedAppInstanceId = await resolveAppInstanceId(appKey);
          if (!cancelled) {
            setAppInstanceId(resolvedAppInstanceId);
          }
        } catch {
          if (!cancelled) {
            setAppInstanceId(null);
          }
        }
        return;
      }

      if (!cancelled) {
        setWorkspaceLabel(appKey || "Workspace");
        setAppInstanceId(null);
      }
    };

    void syncWorkspaceContext();

    return () => {
      cancelled = true;
    };
  }, [appKey, appReady]);

  const value = useMemo<BootstrapState>(() => ({
    loading,
    platformReady,
    appReady,
    appKey,
    appInstanceId,
    spaceId: appInstanceId || "atlas-space",
    workspaceLabel,
    appStatus,
    setupConsole,
    systemInit: setupConsole?.system ?? null,
    workspaceInits: setupConsole?.workspaces ?? [],
    refresh,
    refreshSetupConsole
  }), [
    appInstanceId,
    appKey,
    appStatus,
    loading,
    platformReady,
    appReady,
    setupConsole,
    workspaceLabel
  ]);

  return (
    <BootstrapContext.Provider value={value}>
      {children}
    </BootstrapContext.Provider>
  );
}

export function useBootstrap() {
  const context = useContext(BootstrapContext);
  if (!context) {
    throw new Error("BootstrapProvider is missing.");
  }

  return context;
}
