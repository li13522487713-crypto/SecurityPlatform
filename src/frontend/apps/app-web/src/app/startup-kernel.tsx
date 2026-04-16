import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useBootstrap } from "./bootstrap-context";
import { useAuth } from "./auth-context";
import { loadAppFeatureFlags } from "./runtime-init";

type FeatureFlagsPhase = "idle" | "loading" | "ready" | "error";

export interface AppStartupState {
  bootstrapReady: boolean;
  platformReady: boolean;
  appReady: boolean;
  featureFlagsReady: boolean;
  featureFlagsLoading: boolean;
  spaceReady: boolean;
  workflowAllowed: boolean;
  featureFlagsError: Error | null;
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
  const autoLoadedRef = useRef(false);

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

  const value = useMemo<AppStartupState>(() => {
    const bootstrapReady = !bootstrap.loading;
    const featureFlagsReady = featureFlagsPhase === "ready";
    const featureFlagsLoading = featureFlagsPhase === "loading" || featureFlagsPhase === "idle";
    const spaceReady = !auth.isAuthenticated || Boolean(bootstrap.spaceId);
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
      featureFlagsError,
      refreshFeatureFlags
    };
  }, [
    auth.isAuthenticated,
    bootstrap.appReady,
    bootstrap.loading,
    bootstrap.platformReady,
    bootstrap.spaceId,
    featureFlagsError,
    featureFlagsPhase,
    refreshFeatureFlags
  ]);

  return (
    <AppStartupContext.Provider value={value}>
      {bootstrap.loading ? loadingFallback ?? null : children}
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
