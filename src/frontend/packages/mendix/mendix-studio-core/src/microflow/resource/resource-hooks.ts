import { useCallback, useEffect, useMemo, useState } from "react";

import { createLocalMicroflowResourceAdapter } from "../adapter/local-microflow-resource-adapter";
import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";
import type { MicroflowResource, MicroflowResourceQuery } from "./resource-types";

export interface UseMicroflowResourcesOptions {
  adapter?: MicroflowResourceAdapter;
  workspaceId?: string;
  currentUser?: { id: string; name: string };
  query: MicroflowResourceQuery;
}

export function useMicroflowResourceAdapter(adapter?: MicroflowResourceAdapter, workspaceId?: string, currentUser?: { id: string; name: string }) {
  return useMemo(
    () => adapter ?? createLocalMicroflowResourceAdapter({ workspaceId, currentUser }),
    [adapter, currentUser, workspaceId]
  );
}

export function useMicroflowResources(options: UseMicroflowResourcesOptions) {
  const adapter = useMicroflowResourceAdapter(options.adapter, options.workspaceId, options.currentUser);
  const [items, setItems] = useState<MicroflowResource[]>([]);
  const [allItems, setAllItems] = useState<MicroflowResource[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error>();

  const reload = useCallback(async () => {
    setLoading(true);
    setError(undefined);
    try {
      const [filtered, all] = await Promise.all([
        adapter.listMicroflows(options.query),
        adapter.listMicroflows({})
      ]);
      setItems(filtered.items);
      setAllItems(all.items);
    } catch (caught) {
      setError(caught instanceof Error ? caught : new Error(String(caught)));
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [adapter, options.query]);

  useEffect(() => {
    void reload();
  }, [reload]);

  return { adapter, items, allItems, loading, error, reload };
}
