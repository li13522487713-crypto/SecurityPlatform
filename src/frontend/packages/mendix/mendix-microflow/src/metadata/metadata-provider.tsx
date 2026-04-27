import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import type { MicroflowMetadataCatalog } from "./metadata-catalog";
import type { MicroflowMetadataAdapter } from "./metadata-adapter";

export interface MicroflowMetadataContextValue {
  catalog: MicroflowMetadataCatalog | null;
  loading: boolean;
  error: Error | null;
  version: number;
  reload: () => Promise<void>;
  refresh: () => Promise<void>;
  adapter: MicroflowMetadataAdapter | undefined;
  workspaceId?: string;
  moduleId?: string;
}

const fallbackReload = async () => undefined;

const defaultContextValue: MicroflowMetadataContextValue = {
  catalog: null,
  loading: true,
  error: null,
  version: 0,
  reload: fallbackReload,
  refresh: fallbackReload,
  adapter: undefined,
};

export const MicroflowMetadataContext = createContext<MicroflowMetadataContextValue>(defaultContextValue);

export interface MicroflowMetadataProviderProps {
  /** 生产路径必须注入真实 adapter；缺失时显示错误，不回落 mock metadata。 */
  adapter?: MicroflowMetadataAdapter;
  /** 若已持有 catalog，可同步注入并跳过首次 adapter 请求。 */
  initialCatalog?: MicroflowMetadataCatalog;
  workspaceId?: string;
  moduleId?: string;
  children: ReactNode;
}

export function MicroflowMetadataProvider({
  adapter: adapterProp,
  initialCatalog,
  workspaceId,
  moduleId,
  children,
}: MicroflowMetadataProviderProps) {
  const adapter = useMemo(() => adapterProp, [adapterProp]);

  const [catalog, setCatalog] = useState<MicroflowMetadataCatalog | null>(initialCatalog ?? null);
  const [loading, setLoading] = useState<boolean>(initialCatalog === undefined);
  const [error, setError] = useState<Error | null>(null);
  const [version, setVersion] = useState(0);

  const request = useMemo(
    () => ({ workspaceId, moduleId }),
    [workspaceId, moduleId],
  );

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    if (!adapter) {
      setCatalog(null);
      setError(new Error("Microflow metadata adapter is not configured."));
      setLoading(false);
      return;
    }
    try {
      const next = await adapter.getMetadataCatalog(request);
      setCatalog(next);
      setVersion(v => v + 1);
    } catch (err) {
      setCatalog(null);
      setError(err instanceof Error ? err : new Error(String(err)));
    } finally {
      setLoading(false);
    }
  }, [adapter, request]);

  useEffect(() => {
    if (initialCatalog === undefined) {
      return;
    }
    setCatalog(initialCatalog);
    setLoading(false);
    setError(null);
    setVersion(v => v + 1);
  }, [initialCatalog]);

  useEffect(() => {
    if (initialCatalog !== undefined) {
      return undefined;
    }
    void load();
    return undefined;
  }, [initialCatalog, load]);

  const reload = useCallback(async () => {
    await load();
  }, [load]);

  const refresh = useCallback(async () => {
    if (!adapter) {
      setCatalog(null);
      setError(new Error("Microflow metadata adapter is not configured."));
      setLoading(false);
      return;
    }
    if (adapter.refreshMetadataCatalog) {
      setLoading(true);
      setError(null);
      try {
        const next = await adapter.refreshMetadataCatalog(request);
        setCatalog(next);
        setVersion(v => v + 1);
      } catch (err) {
        setCatalog(null);
        setError(err instanceof Error ? err : new Error(String(err)));
      } finally {
        setLoading(false);
      }
      return;
    }
    await load();
  }, [adapter, load, request]);

  const value = useMemo<MicroflowMetadataContextValue>(
    () => ({
      catalog,
      loading,
      error,
      version,
      reload,
      refresh,
      adapter: adapterProp,
      workspaceId,
      moduleId,
    }),
    [adapterProp, catalog, error, loading, reload, refresh, version, workspaceId, moduleId],
  );

  return (
    <MicroflowMetadataContext.Provider value={value}>
      {children}
    </MicroflowMetadataContext.Provider>
  );
}

export function useMicroflowMetadataContext(): MicroflowMetadataContextValue {
  return useContext(MicroflowMetadataContext);
}
