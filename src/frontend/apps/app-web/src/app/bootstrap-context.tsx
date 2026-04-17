import { createContext, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { getAccessToken, getTenantId } from "@atlas/shared-react-core/utils";
import { getSetupState } from "../services/api-setup";
import { getConfiguredAppKey, rememberConfiguredAppKey } from "../services/api-core";
import { resolveAppInstanceId } from "../services/app-instance-context";
import { getSetupConsoleOverview } from "../services/mock";
import type {
  SetupConsoleOverviewDto,
  SystemSetupStateDto,
  WorkspaceSetupStateDto
} from "../services/api-setup-console";

interface BootstrapState {
  loading: boolean;
  platformReady: boolean;
  appReady: boolean;
  appKey: string;
  appInstanceId: string | null;
  spaceId: string;
  workspaceLabel: string;
  platformStatus: string;
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
  const [platformReady, setPlatformReady] = useState(true);
  const [appReady, setAppReady] = useState(true);
  const [appKey, setAppKey] = useState(getConfiguredAppKey());
  const [appInstanceId, setAppInstanceId] = useState<string | null>(null);
  const [platformStatus, setPlatformStatus] = useState("");
  const [appStatus, setAppStatus] = useState("");
  const [workspaceLabel, setWorkspaceLabel] = useState("Workspace");
  const [setupConsole, setSetupConsole] = useState<SetupConsoleOverviewDto | null>(null);

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
      const nextPlatformStatus = String(response.data?.platformStatus ?? "").trim();
      const nextAppStatus = String(response.data?.appStatus ?? "").trim();
      const nextPlatformReady = response.success && (
        response.data?.platformSetupCompleted === true || nextPlatformStatus === "Ready"
      );
      const nextAppReady = nextPlatformReady && response.data?.appSetupCompleted === true;
      const nextAppKey = String(response.data?.appKey ?? response.data?.configuredAppKey ?? getConfiguredAppKey()).trim();

      setPlatformStatus(nextPlatformStatus);
      setAppStatus(nextAppStatus);
      setPlatformReady(nextPlatformReady);
      setAppReady(nextAppReady);
      setAppKey(nextAppKey);
      rememberConfiguredAppKey(nextAppKey);

      const hasAuthContext = Boolean(getAccessToken() && getTenantId());

      if (nextPlatformReady && nextAppReady && nextAppKey && hasAuthContext) {
        setWorkspaceLabel(nextAppKey);
        try {
          const resolvedAppInstanceId = await resolveAppInstanceId(nextAppKey);
          setAppInstanceId(resolvedAppInstanceId);
        } catch {
          setAppInstanceId(null);
        }
      } else {
        setWorkspaceLabel(nextAppKey || "Workspace");
        setAppInstanceId(null);
      }
    } catch {
      setPlatformReady(false);
      setAppReady(false);
      setAppInstanceId(null);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void refresh();
    void refreshSetupConsole();
  }, []);

  const value = useMemo<BootstrapState>(() => ({
    loading,
    platformReady,
    appReady,
    appKey,
    appInstanceId,
    spaceId: appInstanceId || "atlas-space",
    workspaceLabel,
    platformStatus,
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
    platformStatus,
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
