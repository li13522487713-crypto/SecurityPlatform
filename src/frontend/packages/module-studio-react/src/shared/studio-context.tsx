import React, { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from "react";
import type { 
  StudioModuleApi, 
  WorkspaceIdeSummary, 
  ModelConfigItem, 
  WorkspaceIdeResource 
} from "../types";

export interface StudioContextValue {
  workspaceSummary: WorkspaceIdeSummary | null;
  modelConfigs: ModelConfigItem[];
  hasEnabledModel: boolean;
  recentResources: WorkspaceIdeResource[];
  pendingPublishCount: number;
  refreshSummary: () => Promise<void>;
}

const StudioContext = createContext<StudioContextValue | null>(null);

export function useStudioContext(): StudioContextValue {
  const context = useContext(StudioContext);
  if (!context) {
    throw new Error("useStudioContext must be used within a StudioContextProvider");
  }
  return context;
}

export interface StudioContextProviderProps {
  api: StudioModuleApi;
  children: ReactNode;
}

export function StudioContextProvider({ api, children }: StudioContextProviderProps) {
  const [workspaceSummary, setWorkspaceSummary] = useState<WorkspaceIdeSummary | null>(null);
  const [modelConfigs, setModelConfigs] = useState<ModelConfigItem[]>([]);
  const [recentResources, setRecentResources] = useState<WorkspaceIdeResource[]>([]);
  const [pendingPublishCount, setPendingPublishCount] = useState(0);

  const refreshSummary = useCallback(async () => {
    try {
      const [summary, models, resources, dashboardStats] = await Promise.all([
        api.getWorkspaceSummary(),
        api.listModelConfigs(),
        api.listWorkspaceResources({ pageSize: 10 }),
        api.getDashboardStats()
      ]);
      setWorkspaceSummary(summary);
      setModelConfigs(models.items);
      setRecentResources(resources.items);
      setPendingPublishCount(dashboardStats.pendingPublishItems.length);
    } catch (e) {
      console.error("Failed to load studio summary", e);
    }
  }, [api]);

  useEffect(() => {
    void refreshSummary();
  }, [refreshSummary]);

  const hasEnabledModel = modelConfigs.some(m => m.isEnabled);

  const value: StudioContextValue = {
    workspaceSummary,
    modelConfigs,
    hasEnabledModel,
    recentResources,
    pendingPublishCount,
    refreshSummary
  };

  return (
    <StudioContext.Provider value={value}>
      {children}
    </StudioContext.Provider>
  );
}
