import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useBootstrap } from "./bootstrap-context";
import { useAuth } from "./auth-context";
import { loadAppFeatureFlags } from "./runtime-init";
import { I18n, initI18nInstance } from "../../../../packages/arch/i18n/src/raw";

/**
 * 启动前路由白名单：这些路由必须在 BootstrapProvider 完成之前就可以访问，
 * 否则当后端尚未启动（典型场景：私有化首启 / setup 控制台 / 离线灾难恢复）时，
 * 前端会被永久卡在 `LoadingPage` 上。
 *
 * 之所以放在这里而不是 BootstrapProvider 内部：BootstrapProvider 需要保留对启动失败的
 * 全局降级语义（platformReady=false 时，业务路由会被 PlatformShellLayout 引导到错误页）；
 * 而 startup gate 只需要在“肯定不依赖 bootstrap 的路由”上保持非阻塞即可。
 */
const PRE_BOOTSTRAP_ROUTE_PREFIXES = [
  "/setup-console",
  "/sign",
  "/platform-not-ready",
  "/app-setup"
] as const;

function isPreBootstrapPath(pathname: string): boolean {
  return PRE_BOOTSTRAP_ROUTE_PREFIXES.some(
    (prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`)
  );
}

type FeatureFlagsPhase = "idle" | "loading" | "ready" | "error";
type CozeI18nPhase = "idle" | "loading" | "ready" | "error";

/**
 * 从 localStorage 读取 Atlas 语言设置，转为 cozelib i18next 语言代码。
 * 仅在启动阶段同步读取一次，后续语言变更通过 I18n.setLang() 完成。
 */
function getInitialCozeLocale(): "en" | "zh-CN" {
  try {
    const saved = typeof window !== "undefined"
      ? window.localStorage.getItem("atlas_locale")
      : null;
    if (saved && saved.trim().toLowerCase().startsWith("zh-")) {
      return "zh-CN";
    }
  } catch {
    // localStorage 不可用时使用默认值
  }
  return "zh-CN";
}

export interface AppStartupState {
  bootstrapReady: boolean;
  platformReady: boolean;
  appReady: boolean;
  featureFlagsReady: boolean;
  featureFlagsLoading: boolean;
  spaceReady: boolean;
  workflowAllowed: boolean;
  featureFlagsError: Error | null;
  /** cozelib i18next 实例是否已完成全局初始化 */
  cozeI18nReady: boolean;
  refreshFeatureFlags: () => Promise<void>;
}

const AppStartupContext = createContext<AppStartupState | null>(null);

export function AppStartupKernel({
  children,
  loadingFallback
}: {
  children: ReactNode;
  loadingFallback?: ReactNode;
}) {
  const bootstrap = useBootstrap();
  const auth = useAuth();
  const [featureFlagsPhase, setFeatureFlagsPhase] = useState<FeatureFlagsPhase>("idle");
  const [featureFlagsError, setFeatureFlagsError] = useState<Error | null>(null);
  const [cozeI18nPhase, setCozeI18nPhase] = useState<CozeI18nPhase>("idle");
  const autoLoadedRef = useRef(false);
  const cozeI18nLoadedRef = useRef(false);

  const refreshFeatureFlags = useCallback(async () => {
    setFeatureFlagsPhase("loading");
    setFeatureFlagsError(null);

    try {
      await loadAppFeatureFlags();
      setFeatureFlagsPhase("ready");
    } catch (error) {
      setFeatureFlagsError(error instanceof Error ? error : new Error("Failed to load feature flags."));
      setFeatureFlagsPhase("error");
    }
  }, []);

  useEffect(() => {
    if (bootstrap.loading || autoLoadedRef.current) {
      return;
    }

    autoLoadedRef.current = true;
    void refreshFeatureFlags();
  }, [bootstrap.loading, refreshFeatureFlags]);

  // cozelib i18next 全局初始化：在 AppStartupKernel 挂载时立即执行一次，
  // 与 bootstrap 并行，不阻塞 featureFlags 加载，缩短 WorkflowRuntimeBoundary 等待时长。
  useEffect(() => {
    if (cozeI18nLoadedRef.current) {
      return;
    }
    cozeI18nLoadedRef.current = true;
    setCozeI18nPhase("loading");

    const lng = getInitialCozeLocale();
    initI18nInstance({ lng })
      .then(() => {
        I18n.setLang(lng);
        setCozeI18nPhase("ready");
      })
      .catch(() => {
        // 初始化失败时仍设为 ready，避免永久阻塞画布加载
        setCozeI18nPhase("error");
      });
  }, []);

  const value = useMemo<AppStartupState>(() => {
    const bootstrapReady = !bootstrap.loading;
    const featureFlagsReady = featureFlagsPhase === "ready";
    const featureFlagsLoading = featureFlagsPhase === "loading" || featureFlagsPhase === "idle";
    const spaceReady = !auth.isAuthenticated || Boolean(bootstrap.spaceId);
    const cozeI18nReady = cozeI18nPhase === "ready" || cozeI18nPhase === "error";
    const workflowAllowed =
      bootstrapReady &&
      bootstrap.platformReady &&
      bootstrap.appReady &&
      featureFlagsReady &&
      spaceReady;

    return {
      bootstrapReady,
      platformReady: bootstrap.platformReady,
      appReady: bootstrap.appReady,
      featureFlagsReady,
      featureFlagsLoading,
      spaceReady,
      workflowAllowed,
      cozeI18nReady,
      featureFlagsError,
      refreshFeatureFlags
    };
  }, [
    auth.isAuthenticated,
    bootstrap.appReady,
    bootstrap.loading,
    bootstrap.platformReady,
    bootstrap.spaceId,
    cozeI18nPhase,
    featureFlagsError,
    featureFlagsPhase,
    refreshFeatureFlags
  ]);

  const pathname = typeof window !== "undefined" ? window.location.pathname : "";
  const bypassLoadingGate = isPreBootstrapPath(pathname);

  return (
    <AppStartupContext.Provider value={value}>
      {bootstrap.loading && !bypassLoadingGate ? loadingFallback ?? null : children}
    </AppStartupContext.Provider>
  );
}

export function useAppStartup() {
  const context = useContext(AppStartupContext);
  if (!context) {
    throw new Error("AppStartupKernel is missing.");
  }

  return context;
}
