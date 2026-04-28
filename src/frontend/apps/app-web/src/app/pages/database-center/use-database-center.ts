import { useCallback, useEffect, useMemo, useState } from "react";
import { Toast } from "@douyinfe/semi-ui";
import {
  getDatabaseCenterSchemaStructure,
  getDatabaseCenterSource,
  listDatabaseCenterSchemas,
  listDatabaseCenterSources,
  type DatabaseCenterEnvironment,
  type DatabaseCenterSchemaStructure,
  type DatabaseCenterSchemaSummary,
  type DatabaseCenterSourceDetail,
  type DatabaseCenterSourceSummary
} from "../../../services/api-database-center";
import type { DatabaseCenterLabels } from "./database-center-labels";

export interface UseDatabaseCenterOptions {
  workspaceId?: string;
  initialSourceId?: string;
  labels: DatabaseCenterLabels;
}

export function useDatabaseCenter({ workspaceId, initialSourceId, labels }: UseDatabaseCenterOptions) {
  const [keyword, setKeyword] = useState("");
  const [sources, setSources] = useState<DatabaseCenterSourceSummary[]>([]);
  const [selectedSourceId, setSelectedSourceId] = useState(initialSourceId ?? "");
  const [sourceDetail, setSourceDetail] = useState<DatabaseCenterSourceDetail | null>(null);
  const [environment, setEnvironment] = useState<DatabaseCenterEnvironment>("Draft");
  const [schemas, setSchemas] = useState<DatabaseCenterSchemaSummary[]>([]);
  const [selectedSchema, setSelectedSchema] = useState("");
  const [structure, setStructure] = useState<DatabaseCenterSchemaStructure | null>(null);
  const [loadingSources, setLoadingSources] = useState(false);
  const [loadingStructure, setLoadingStructure] = useState(false);

  const selectedSource = useMemo(
    () => sources.find(item => item.id === selectedSourceId) ?? sourceDetail,
    [sourceDetail, selectedSourceId, sources]
  );

  const loadSources = useCallback(async () => {
    setLoadingSources(true);
    try {
      const result = await listDatabaseCenterSources({
        pageIndex: 1,
        pageSize: 50,
        keyword: keyword.trim() || undefined,
        workspaceId
      });
      const nextSources = result.items ?? [];
      setSources(nextSources);
      setSelectedSourceId(current => {
        if (current) {
          return current;
        }

        if (initialSourceId) {
          const direct = nextSources.find(item => item.id === initialSourceId);
          if (direct) {
            return direct.id;
          }

          const byAiDatabase = nextSources.find(item => item.aiDatabaseId === initialSourceId && item.environment === "Draft")
            ?? nextSources.find(item => item.aiDatabaseId === initialSourceId);
          if (byAiDatabase) {
            return byAiDatabase.id;
          }
        }

        return nextSources[0]?.id || "";
      });
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoadingSources(false);
    }
  }, [initialSourceId, keyword, labels.loadFailed, workspaceId]);

  const loadSelectedSource = useCallback(async () => {
    if (!selectedSourceId) {
      setSourceDetail(null);
      setSchemas([]);
      setSelectedSchema("");
      setStructure(null);
      return;
    }

    setLoadingStructure(true);
    try {
      const [detail, nextSchemas] = await Promise.all([
        getDatabaseCenterSource(selectedSourceId),
        listDatabaseCenterSchemas(selectedSourceId, environment)
      ]);
      setSourceDetail(detail);
      if (detail.environment === "Draft" || detail.environment === "Online") {
        setEnvironment(detail.environment);
      }
      setSchemas(nextSchemas);
      const nextSchema = nextSchemas.find(item => item.defaultSchema)?.name || nextSchemas[0]?.name || "";
      setSelectedSchema(nextSchema);
      if (!nextSchema) {
        setStructure(null);
      }
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoadingStructure(false);
    }
  }, [environment, labels.loadFailed, selectedSourceId]);

  const loadStructure = useCallback(async () => {
    if (!selectedSourceId || !selectedSchema) {
      setStructure(null);
      return;
    }

    setLoadingStructure(true);
    try {
      setStructure(await getDatabaseCenterSchemaStructure(selectedSourceId, selectedSchema, environment));
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoadingStructure(false);
    }
  }, [environment, labels.loadFailed, selectedSchema, selectedSourceId]);

  useEffect(() => {
    void loadSources();
  }, [loadSources]);

  useEffect(() => {
    void loadSelectedSource();
  }, [loadSelectedSource]);

  useEffect(() => {
    void loadStructure();
  }, [loadStructure]);

  const refresh = useCallback(async () => {
    await loadSources();
    await loadSelectedSource();
  }, [loadSelectedSource, loadSources]);

  return {
    keyword,
    setKeyword,
    sources,
    selectedSourceId,
    setSelectedSourceId,
    selectedSource,
    sourceDetail,
    environment,
    setEnvironment,
    schemas,
    selectedSchema,
    setSelectedSchema,
    structure,
    loadingSources,
    loadingStructure,
    refresh,
    loadSources,
    loadStructure
  };
}
