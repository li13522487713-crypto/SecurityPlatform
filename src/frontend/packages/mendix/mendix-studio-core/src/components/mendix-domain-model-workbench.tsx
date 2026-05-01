import { useCallback, useEffect, useMemo, useState } from "react";
import { Button, Card, Divider, Empty, Form, Input, Modal, Select, SideSheet, Space, Spin, Table, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";

import type { StudioWorkbenchTab } from "../store";
import { useMendixStudioStore } from "../store";
import type { MicroflowModuleAsset } from "../microflow/resource";
import type { MicroflowApiClient } from "../microflow/adapter/http/microflow-api-client";
import { getMendixStudioCopy } from "../i18n/copy";

const { Text, Title } = Typography;

interface DomainModelBinding {
  bindingId: string;
  sourceId: string;
  aiDatabaseId?: string;
  alias: string;
  driverCode: string;
  defaultSchemaName?: string;
  displayName?: string;
  enabled: boolean;
}

interface DomainModelAttribute {
  attributeId: string;
  name: string;
  columnName: string;
  type: string;
  required: boolean;
  primaryKey: boolean;
  indexed: boolean;
  defaultValue?: string;
}

interface DomainModelEntity {
  entityId: string;
  bindingId: string;
  name: string;
  qualifiedName: string;
  schemaName: string;
  tableName: string;
  origin: string;
  syncStatus: string;
  persistable: boolean;
  attributes: DomainModelAttribute[];
}

interface DomainModelAssociation {
  associationId: string;
  name: string;
  fromEntityId: string;
  toEntityId: string;
  sourceAttributeId?: string;
  targetAttributeId?: string;
  owner: string;
  cardinality: string;
  bindingMode: string;
  joinSpec?: {
    sourceBindingId: string;
    targetBindingId: string;
    sourceField: string;
    targetField: string;
    joinType: string;
  };
}

interface DomainModelDocument {
  appId: string;
  workspaceId: string;
  moduleId: string;
  bindings: DomainModelBinding[];
  entities: DomainModelEntity[];
  associations: DomainModelAssociation[];
  layout: {
    entityFrames: Record<string, { x: number; y: number; width: number; height: number }>;
  };
  syncState: {
    status: string;
    lastSyncedAt?: string;
    lastError?: string;
  };
}

interface DomainModelSyncPlan {
  createTables: Array<{ bindingId: string; schemaName: string; tableName: string; entityId: string }>;
  addColumns: Array<{ bindingId: string; schemaName: string; tableName: string; columnName: string }>;
  alterColumns: Array<{ bindingId: string; schemaName: string; tableName: string; columnName: string }>;
  renameColumns: Array<{ bindingId: string; schemaName: string; tableName: string; columnName: string; newColumnName: string }>;
  dropColumns: Array<{ bindingId: string; schemaName: string; tableName: string; columnName: string }>;
  createForeignKeys: Array<{ bindingId: string; schemaName: string; tableName: string; foreignKeyName: string; referencedTableName: string }>;
  dropForeignKeys: Array<{ bindingId: string; schemaName: string; tableName: string; foreignKeyName: string }>;
  warnings: string[];
  errors: string[];
}

interface DatabaseSourceSummary {
  id: string;
  aiDatabaseId?: string | null;
  name: string;
  driverCode: string;
  environment?: string | null;
}

interface DatabaseSchemaSummary {
  name: string;
}

interface DatabaseStructureResponse {
  objects: Array<{ id: string; name: string; objectType: string }>;
}

interface MendixDomainModelWorkbenchProps {
  tab: StudioWorkbenchTab;
  modules: MicroflowModuleAsset[];
  appId?: string;
  workspaceId?: string;
  apiClient?: MicroflowApiClient;
}

function createEmptyDocument(appId: string, workspaceId: string, moduleId: string): DomainModelDocument {
  return {
    appId,
    workspaceId,
    moduleId,
    bindings: [],
    entities: [],
    associations: [],
    layout: { entityFrames: {} },
    syncState: { status: "idle" }
  };
}

function toModuleEntities(document: DomainModelDocument): MicroflowModuleAsset["entities"] {
  return document.entities.map(entity => ({
    id: entity.entityId,
    name: entity.name,
    qualifiedName: entity.qualifiedName,
    moduleName: document.moduleId,
    attributeCount: entity.attributes.length,
    associationCount: document.associations.filter(association => association.fromEntityId === entity.entityId || association.toEntityId === entity.entityId).length,
    isPersistable: entity.persistable
  }));
}

export function MendixDomainModelWorkbench({
  tab,
  modules,
  appId,
  workspaceId,
  apiClient
}: MendixDomainModelWorkbenchProps) {
  const copy = getMendixStudioCopy();
  const c = copy.domainModelWorkbench;
  const [loading, setLoading] = useState(true);
  const [document, setDocument] = useState<DomainModelDocument | null>(null);
  const [previewPlan, setPreviewPlan] = useState<DomainModelSyncPlan | null>(null);
  const [previewVisible, setPreviewVisible] = useState(false);
  const [bindVisible, setBindVisible] = useState(false);
  const [importVisible, setImportVisible] = useState(false);
  const [entityVisible, setEntityVisible] = useState(false);
  const [relationVisible, setRelationVisible] = useState(false);
  const [saving, setSaving] = useState(false);
  const [syncing, setSyncing] = useState(false);
  const [sources, setSources] = useState<DatabaseSourceSummary[]>([]);
  const [schemas, setSchemas] = useState<DatabaseSchemaSummary[]>([]);
  const [tables, setTables] = useState<string[]>([]);
  const [selectedEntityId, setSelectedEntityId] = useState<string>();
  const [selectedRelationId, setSelectedRelationId] = useState<string>();
  const [bindSourceId, setBindSourceId] = useState<string>();
  const [bindAlias, setBindAlias] = useState("");
  const [importBindingId, setImportBindingId] = useState<string>();
  const [importSchemaName, setImportSchemaName] = useState<string>();
  const [importTableNames, setImportTableNames] = useState<string[]>([]);
  const [newEntityName, setNewEntityName] = useState("");
  const [newEntityBindingId, setNewEntityBindingId] = useState<string>();
  const [newRelationName, setNewRelationName] = useState("");
  const [newRelationSourceEntityId, setNewRelationSourceEntityId] = useState<string>();
  const [newRelationTargetEntityId, setNewRelationTargetEntityId] = useState<string>();
  const [newRelationSourceAttributeId, setNewRelationSourceAttributeId] = useState<string>();
  const [newRelationTargetAttributeId, setNewRelationTargetAttributeId] = useState<string>();
  const [newRelationCrossDatabase, setNewRelationCrossDatabase] = useState(false);
  const setAppAssetModules = useMendixStudioStore(state => state.setAppAssetModules);

  const moduleId = tab.moduleId ?? tab.resourceId ?? "";

  const applyModuleEntities = useCallback((nextDocument: DomainModelDocument) => {
    const nextModules = modules.map(module => module.moduleId === moduleId
      ? { ...module, entities: toModuleEntities(nextDocument) }
      : module);
    setAppAssetModules(nextModules);
  }, [moduleId, modules, setAppAssetModules]);

  const applyLocalDocument = useCallback((nextDocument: DomainModelDocument) => {
    setDocument(nextDocument);
    applyModuleEntities(nextDocument);
  }, [applyModuleEntities]);

  const request = useCallback(async <T,>(method: "GET" | "POST" | "PUT", path: string, body?: unknown): Promise<T> => {
    if (!apiClient) {
      throw new Error(c.noApi);
    }
    if (method === "GET") {
      return apiClient.get<T>(path);
    }
    if (method === "POST") {
      return apiClient.post<T>(path, body);
    }
    return apiClient.put<T>(path, body);
  }, [apiClient, c.noApi]);

  const loadDocument = useCallback(async () => {
    if (!appId || !workspaceId || !moduleId) {
      setDocument(null);
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const result = await request<DomainModelDocument>(
        "GET",
        `/microflow-apps/${encodeURIComponent(appId)}/domain-model/modules/${encodeURIComponent(moduleId)}?workspaceId=${encodeURIComponent(workspaceId)}`
      );
      applyLocalDocument(result);
      setSelectedEntityId(current => current ?? result.entities[0]?.entityId);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : c.noApi);
      const fallback = createEmptyDocument(appId, workspaceId, moduleId);
      applyLocalDocument(fallback);
    } finally {
      setLoading(false);
    }
  }, [appId, workspaceId, moduleId, request, applyModuleEntities, c.noApi]);

  useEffect(() => {
    void loadDocument();
  }, [loadDocument]);

  const selectedEntity = useMemo(
    () => document?.entities.find(entity => entity.entityId === selectedEntityId) ?? document?.entities[0],
    [document, selectedEntityId]
  );
  const selectedRelation = useMemo(
    () => document?.associations.find(association => association.associationId === selectedRelationId),
    [document, selectedRelationId]
  );

  const sourceOptions = useMemo(
    () => sources.map(source => ({ label: `${source.name} (${source.driverCode})`, value: source.id })),
    [sources]
  );
  const bindingOptions = useMemo(
    () => (document?.bindings ?? []).map(binding => ({ label: `${binding.alias} · ${binding.displayName ?? binding.sourceId}`, value: binding.bindingId })),
    [document]
  );
  const schemaOptions = useMemo(
    () => schemas.map(schema => ({ label: schema.name, value: schema.name })),
    [schemas]
  );
  const tableOptions = useMemo(
    () => tables.map(table => ({ label: table, value: table })),
    [tables]
  );
  const entityOptions = useMemo(
    () => (document?.entities ?? []).map(entity => ({ label: entity.qualifiedName, value: entity.entityId })),
    [document]
  );
  const sourceFieldOptions = useMemo(() => {
    const entity = document?.entities.find(item => item.entityId === newRelationSourceEntityId);
    return (entity?.attributes ?? []).map(attribute => ({ label: attribute.name, value: attribute.attributeId }));
  }, [document, newRelationSourceEntityId]);
  const targetFieldOptions = useMemo(() => {
    const entity = document?.entities.find(item => item.entityId === newRelationTargetEntityId);
    return (entity?.attributes ?? []).map(attribute => ({ label: attribute.name, value: attribute.attributeId }));
  }, [document, newRelationTargetEntityId]);
  const relationFieldOptions = useMemo(() => {
    const source = document?.entities.find(item => item.entityId === selectedRelation?.fromEntityId);
    const target = document?.entities.find(item => item.entityId === selectedRelation?.toEntityId);
    return {
      source: (source?.attributes ?? []).map(attribute => ({ label: attribute.name, value: attribute.attributeId })),
      target: (target?.attributes ?? []).map(attribute => ({ label: attribute.name, value: attribute.attributeId }))
    };
  }, [document, selectedRelation]);
  const groupedTree = useMemo(() => {
    if (!document) {
      return [];
    }

    return document.bindings.map(binding => {
      const entities = document.entities.filter(entity => entity.bindingId === binding.bindingId);
      const schemaNames = Array.from(new Set(entities.map(entity => entity.schemaName))).sort((left, right) => left.localeCompare(right));
      return {
        binding,
        schemas: schemaNames.map(schemaName => ({
          schemaName,
          entities: entities
            .filter(entity => entity.schemaName === schemaName)
            .sort((left, right) => left.name.localeCompare(right.name))
            .map(entity => ({
              entity,
              associations: document.associations.filter(association => association.fromEntityId === entity.entityId || association.toEntityId === entity.entityId)
            }))
        }))
      };
    });
  }, [document]);
  const relationLines = useMemo(() => {
    if (!document) {
      return [];
    }

    return document.associations
      .map(association => {
        const sourceFrame = document.layout.entityFrames[association.fromEntityId];
        const targetFrame = document.layout.entityFrames[association.toEntityId];
        if (!sourceFrame || !targetFrame) {
          return null;
        }
        const sourceEntity = document.entities.find(entity => entity.entityId === association.fromEntityId);
        const targetEntity = document.entities.find(entity => entity.entityId === association.toEntityId);
        return {
          association,
          sourceEntity,
          targetEntity,
          x1: sourceFrame.x + sourceFrame.width,
          y1: sourceFrame.y + Math.max(48, sourceFrame.height / 2),
          x2: targetFrame.x,
          y2: targetFrame.y + Math.max(48, targetFrame.height / 2)
        };
      })
      .filter((item): item is NonNullable<typeof item> => item !== null);
  }, [document]);

  const loadSources = useCallback(async () => {
    if (!workspaceId) {
      return;
    }
    const response = await request<{ items: DatabaseSourceSummary[] }>(
      "GET",
      `/database-center/sources?pageIndex=1&pageSize=100&workspaceId=${encodeURIComponent(workspaceId)}`
    );
    setSources(response.items ?? []);
  }, [request, workspaceId]);

  const loadSchemas = useCallback(async (sourceId: string) => {
    const response = await request<DatabaseSchemaSummary[]>(
      "GET",
      `/database-center/sources/${encodeURIComponent(sourceId)}/schemas?environment=Draft`
    );
    setSchemas(response);
  }, [request]);

  const loadTables = useCallback(async (sourceId: string, schemaName: string) => {
    const response = await request<DatabaseStructureResponse>(
      "GET",
      `/database-center/sources/${encodeURIComponent(sourceId)}/schemas/${encodeURIComponent(schemaName)}/structure?environment=Draft`
    );
    setTables((response.objects ?? []).filter(item => item.objectType === "table").map(item => item.name));
  }, [request]);

  const saveDocument = useCallback(async (nextDocument: DomainModelDocument) => {
    if (!appId || !workspaceId || !moduleId) {
      return;
    }
    setSaving(true);
    try {
      const result = await request<DomainModelDocument>(
        "PUT",
        `/microflow-apps/${encodeURIComponent(appId)}/domain-model/modules/${encodeURIComponent(moduleId)}?workspaceId=${encodeURIComponent(workspaceId)}`,
        nextDocument
      );
      applyLocalDocument(result);
      Toast.success(c.saveSuccess);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : c.noApi);
    } finally {
      setSaving(false);
    }
  }, [appId, workspaceId, moduleId, request, applyModuleEntities, c.saveSuccess, c.noApi]);

  const handleSaveBindings = useCallback(async () => {
    if (!document || !bindSourceId) {
      return;
    }
    const source = sources.find(item => item.id === bindSourceId);
    if (!source) {
      return;
    }
    const nextBindings = [...document.bindings, {
      bindingId: `binding:${document.bindings.length + 1}`,
      sourceId: source.id,
      aiDatabaseId: source.aiDatabaseId ?? undefined,
      alias: bindAlias.trim() || `db${document.bindings.length + 1}`,
      driverCode: source.driverCode,
      defaultSchemaName: undefined,
      displayName: source.name,
      enabled: true
    }];
    setSaving(true);
    try {
      const result = await request<DomainModelDocument>(
        "PUT",
        `/microflow-apps/${encodeURIComponent(appId!)}/domain-model/modules/${encodeURIComponent(moduleId)}${`/bindings`}?workspaceId=${encodeURIComponent(workspaceId!)}`,
        nextBindings
      );
      applyLocalDocument(result);
      setBindVisible(false);
      setBindAlias("");
      setBindSourceId(undefined);
      Toast.success(c.bindSuccess);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : c.noApi);
    } finally {
      setSaving(false);
    }
  }, [document, bindSourceId, sources, bindAlias, request, appId, moduleId, workspaceId, applyModuleEntities, c.bindSuccess, c.noApi]);

  const handleImportTables = useCallback(async () => {
    if (!document || !importBindingId || !importSchemaName || importTableNames.length === 0) {
      return;
    }
    const binding = document.bindings.find(item => item.bindingId === importBindingId);
    if (!binding) {
      return;
    }
    setSaving(true);
    try {
      const result = await request<{ document: DomainModelDocument }>(
        "POST",
        `/microflow-apps/${encodeURIComponent(appId!)}/domain-model/modules/${encodeURIComponent(moduleId)}/import-tables?workspaceId=${encodeURIComponent(workspaceId!)}`,
        {
          sourceId: binding.sourceId,
          bindingId: binding.bindingId,
          schemaName: importSchemaName,
          tableNames: importTableNames
        }
      );
      applyLocalDocument(result.document);
      setSelectedEntityId(result.document.entities[0]?.entityId);
      setImportVisible(false);
      setImportTableNames([]);
      Toast.success(c.importSuccess);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : c.noApi);
    } finally {
      setSaving(false);
    }
  }, [document, importBindingId, importSchemaName, importTableNames, request, appId, moduleId, workspaceId, applyModuleEntities, c.importSuccess, c.noApi]);

  const handlePreviewSync = useCallback(async () => {
    if (!appId || !workspaceId || !moduleId) {
      return;
    }
    try {
      const result = await request<DomainModelSyncPlan>(
        "POST",
        `/microflow-apps/${encodeURIComponent(appId)}/domain-model/modules/${encodeURIComponent(moduleId)}/preview-sync?workspaceId=${encodeURIComponent(workspaceId)}`
      );
      setPreviewPlan(result);
      setPreviewVisible(true);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : c.noApi);
    }
  }, [appId, workspaceId, moduleId, request, c.noApi]);

  const handleCreateEntity = useCallback(() => {
    if (!document || !newEntityName.trim()) {
      return;
    }
    const bindingId = newEntityBindingId ?? document.bindings[0]?.bindingId;
    if (!bindingId) {
      Toast.error(c.bindDatabase);
      return;
    }
    const binding = document.bindings.find(item => item.bindingId === bindingId);
    if (!binding) {
      return;
    }
    const schemaName = binding.defaultSchemaName ?? "main";
    const tableName = newEntityName.trim().replace(/\s+/gu, "_").toLowerCase();
    const entityId = `entity:${bindingId}:${schemaName}:${tableName}`;
    const qualifiedName = `${binding.alias}.${schemaName}.${tableName}`;
    const nextDocument: DomainModelDocument = {
      ...document,
      entities: [...document.entities, {
        entityId,
        bindingId,
        name: newEntityName.trim(),
        qualifiedName,
        schemaName,
        tableName,
        origin: "local",
        syncStatus: "dirty",
        persistable: true,
        attributes: [{
          attributeId: `attr:${entityId}:id`,
          name: "id",
          columnName: "id",
          type: "string",
          required: true,
          primaryKey: true,
          indexed: true
        }]
      }],
      layout: {
        ...document.layout,
        entityFrames: {
          ...document.layout.entityFrames,
          [entityId]: {
            x: 48 + (document.entities.length % 3) * 320,
            y: 48 + Math.floor(document.entities.length / 3) * 220,
            width: 280,
            height: 180
          }
        }
      }
    };
    applyLocalDocument(nextDocument);
    setSelectedEntityId(entityId);
    setEntityVisible(false);
    setNewEntityName("");
    setNewEntityBindingId(undefined);
  }, [document, newEntityName, newEntityBindingId, c.bindDatabase]);

  const handleCreateRelation = useCallback(() => {
    if (!document || !newRelationName.trim() || !newRelationSourceEntityId || !newRelationTargetEntityId) {
      return;
    }
    const sourceEntity = document.entities.find(item => item.entityId === newRelationSourceEntityId);
    const targetEntity = document.entities.find(item => item.entityId === newRelationTargetEntityId);
    if (!sourceEntity || !targetEntity) {
      return;
    }
    const sourceAttribute = sourceEntity.attributes.find(item => item.attributeId === newRelationSourceAttributeId) ?? sourceEntity.attributes[0];
    const targetAttribute = targetEntity.attributes.find(item => item.attributeId === newRelationTargetAttributeId) ?? targetEntity.attributes[0];
    const isCrossDatabase = newRelationCrossDatabase || sourceEntity.bindingId !== targetEntity.bindingId;
    const nextDocument: DomainModelDocument = {
      ...document,
      associations: [...document.associations, {
        associationId: `assoc:${sourceEntity.entityId}:${targetEntity.entityId}:${newRelationName.trim()}`,
        name: newRelationName.trim(),
        fromEntityId: sourceEntity.entityId,
        toEntityId: targetEntity.entityId,
        sourceAttributeId: sourceAttribute?.attributeId,
        targetAttributeId: targetAttribute?.attributeId,
        owner: "default",
        cardinality: "manyToOne",
        bindingMode: isCrossDatabase ? "logicalCrossDb" : "physicalFk",
        joinSpec: sourceAttribute && targetAttribute ? {
          sourceBindingId: sourceEntity.bindingId,
          targetBindingId: targetEntity.bindingId,
          sourceField: sourceAttribute.columnName,
          targetField: targetAttribute.columnName,
          joinType: "inner"
        } : undefined
      }]
    };
    applyLocalDocument(nextDocument);
    setRelationVisible(false);
    setNewRelationName("");
    setNewRelationSourceEntityId(undefined);
    setNewRelationTargetEntityId(undefined);
    setNewRelationSourceAttributeId(undefined);
    setNewRelationTargetAttributeId(undefined);
    setNewRelationCrossDatabase(false);
  }, [document, newRelationName, newRelationSourceEntityId, newRelationTargetEntityId, newRelationSourceAttributeId, newRelationTargetAttributeId, newRelationCrossDatabase]);

  const handleSyncDraft = useCallback(async () => {
    if (!appId || !workspaceId || !moduleId) {
      return;
    }
    setSyncing(true);
    try {
      const result = await request<{ document: DomainModelDocument; plan: DomainModelSyncPlan }>(
        "POST",
        `/microflow-apps/${encodeURIComponent(appId)}/domain-model/modules/${encodeURIComponent(moduleId)}/sync-draft?workspaceId=${encodeURIComponent(workspaceId)}`
      );
      applyLocalDocument(result.document);
      setPreviewPlan(result.plan);
      Toast.success(c.syncSuccess);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : c.noApi);
    } finally {
      setSyncing(false);
    }
  }, [appId, workspaceId, moduleId, request, applyModuleEntities, c.syncSuccess, c.noApi]);

  const handleRefreshMetadata = useCallback(async () => {
    if (!appId || !workspaceId || !moduleId) {
      return;
    }
    try {
      await request(
        "POST",
        `/microflow-apps/${encodeURIComponent(appId)}/domain-model/modules/${encodeURIComponent(moduleId)}/refresh-metadata?workspaceId=${encodeURIComponent(workspaceId)}`
      );
      Toast.success(c.refresh);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : c.noApi);
    }
  }, [appId, workspaceId, moduleId, request, c.refresh, c.noApi]);

  const fieldColumns = useMemo<ColumnProps<DomainModelAttribute>[]>(() => [
    {
      title: c.fieldName,
      dataIndex: "name",
      render: (_value, record, index) => (
        <Input
          value={record.name}
          onChange={value => {
            if (!selectedEntity || !document) return;
            const nextEntity: DomainModelEntity = {
              ...selectedEntity,
              attributes: selectedEntity.attributes.map((item, itemIndex) => itemIndex === index ? { ...item, name: value, columnName: item.columnName || value } : item)
            };
            applyLocalDocument({ ...document, entities: document.entities.map(entity => entity.entityId === nextEntity.entityId ? nextEntity : entity) });
          }}
        />
      )
    },
    {
      title: c.fieldType,
      dataIndex: "type",
      render: (_value, record, index) => (
        <Select
          value={record.type}
          optionList={[
            { label: "string", value: "string" },
            { label: "integer", value: "integer" },
            { label: "long", value: "long" },
            { label: "decimal", value: "decimal" },
            { label: "boolean", value: "boolean" },
            { label: "dateTime", value: "dateTime" }
          ]}
          onChange={value => {
            if (!selectedEntity || !document) return;
            const nextEntity: DomainModelEntity = {
              ...selectedEntity,
              attributes: selectedEntity.attributes.map((item, itemIndex) => itemIndex === index ? { ...item, type: String(value) } : item)
            };
            applyLocalDocument({ ...document, entities: document.entities.map(entity => entity.entityId === nextEntity.entityId ? nextEntity : entity) });
          }}
        />
      )
    },
    {
      title: c.required,
      dataIndex: "required",
      render: (_value, record, index) => (
        <Button
          size="small"
          theme="borderless"
          type={record.required ? "danger" : "tertiary"}
          onClick={() => {
            if (!selectedEntity || !document) return;
            const nextEntity: DomainModelEntity = {
              ...selectedEntity,
              attributes: selectedEntity.attributes.map((item, itemIndex) => itemIndex === index ? { ...item, required: !item.required } : item)
            };
            applyLocalDocument({ ...document, entities: document.entities.map(entity => entity.entityId === nextEntity.entityId ? nextEntity : entity) });
          }}
        >
          {record.required ? "Y" : "N"}
        </Button>
      )
    },
    {
      title: c.primaryKey,
      dataIndex: "primaryKey",
      render: (_value, record, index) => (
        <Button
          size="small"
          theme="borderless"
          type={record.primaryKey ? "primary" : "tertiary"}
          onClick={() => {
            if (!selectedEntity || !document) return;
            const nextEntity: DomainModelEntity = {
              ...selectedEntity,
              attributes: selectedEntity.attributes.map((item, itemIndex) => itemIndex === index ? { ...item, primaryKey: !item.primaryKey, required: !item.primaryKey ? true : item.required } : item)
            };
            applyLocalDocument({ ...document, entities: document.entities.map(entity => entity.entityId === nextEntity.entityId ? nextEntity : entity) });
          }}
        >
          {record.primaryKey ? "PK" : "-"}
        </Button>
      )
    },
    {
      title: c.fieldAction,
      dataIndex: "attributeId",
      render: (_value, record) => (
        <Button
          size="small"
          theme="borderless"
          type="danger"
          onClick={() => {
            if (!selectedEntity || !document) return;
            const nextEntity: DomainModelEntity = {
              ...selectedEntity,
              attributes: selectedEntity.attributes.filter(item => item.attributeId !== record.attributeId)
            };
            applyLocalDocument({ ...document, entities: document.entities.map(entity => entity.entityId === nextEntity.entityId ? nextEntity : entity) });
          }}
        >
          {c.deleteField}
        </Button>
      )
    }
  ], [c.fieldName, c.fieldType, c.required, c.primaryKey, c.fieldAction, c.deleteField, selectedEntity, document, applyLocalDocument]);

  if (loading) {
    return <div className="studio-readonly-resource" style={{ display: "grid", placeItems: "center" }}><Spin /></div>;
  }

  if (!document) {
    return <div className="studio-readonly-resource"><Empty description={c.noApi} /></div>;
  }

  return (
    <div className="studio-readonly-resource" data-testid="domain-model-workbench" style={{ display: "flex", flexDirection: "column", gap: 12, padding: 16 }}>
      <Space style={{ justifyContent: "space-between", width: "100%" }}>
        <div>
          <Title heading={5} style={{ margin: 0 }}>{tab.title}</Title>
          <Text type="tertiary">{c.title}</Text>
        </div>
        <Space wrap>
          <Button data-testid="domain-model-save" loading={saving} onClick={() => void saveDocument(document)}>{c.saveModel}</Button>
          <Button data-testid="domain-model-bind-open" onClick={() => { setBindVisible(true); void loadSources(); }}>{c.bindDatabase}</Button>
          <Button data-testid="domain-model-import-open" disabled={document.bindings.length === 0} onClick={() => setImportVisible(true)}>{c.importTables}</Button>
          <Button data-testid="domain-model-create-entity-open" disabled={document.bindings.length === 0} onClick={() => setEntityVisible(true)}>{c.createEntity}</Button>
          <Button data-testid="domain-model-create-relation-open" disabled={document.entities.length < 2} onClick={() => setRelationVisible(true)}>{c.createRelation}</Button>
          <Button data-testid="domain-model-preview-sync" onClick={() => void handlePreviewSync()}>{c.previewSync}</Button>
          <Button data-testid="domain-model-sync-draft" theme="solid" loading={syncing} onClick={() => void handleSyncDraft()}>{c.syncDraft}</Button>
          <Button data-testid="domain-model-refresh-metadata" onClick={() => void handleRefreshMetadata()}>{c.refreshMetadata}</Button>
          <Button data-testid="domain-model-refresh" onClick={() => void loadDocument()}>{c.refresh}</Button>
        </Space>
      </Space>

      {!apiClient ? <Card><Text type="danger">{c.noApi}</Text></Card> : null}

      <div style={{ display: "grid", gridTemplateColumns: "260px minmax(0, 1fr) 360px", gap: 12, minHeight: 520 }}>
        <Card data-testid="domain-model-tree-panel" bodyStyle={{ padding: 12, height: "100%", overflow: "auto" }}>
          <Space vertical align="start" style={{ width: "100%" }}>
            <Title heading={6} style={{ margin: 0 }}>{c.bindingsTitle}</Title>
            {(groupedTree.length === 0) ? <Text type="tertiary">{c.noEntities}</Text> : groupedTree.map(group => (
              <Card key={group.binding.bindingId} data-testid={`domain-model-binding-${group.binding.bindingId}`} style={{ width: "100%" }} bodyStyle={{ padding: 10 }}>
                <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
                  <Space style={{ justifyContent: "space-between", width: "100%" }}>
                    <Text strong>{group.binding.alias}</Text>
                    <Tag>{group.binding.driverCode}</Tag>
                  </Space>
                  <Text type="tertiary">{group.binding.displayName ?? group.binding.sourceId}</Text>
                  {group.schemas.map(schema => (
                    <div key={`${group.binding.bindingId}:${schema.schemaName}`} style={{ width: "100%" }}>
                      <Text type="secondary">{schema.schemaName}</Text>
                      <Space vertical align="start" spacing={6} style={{ width: "100%", marginTop: 6 }}>
                        {schema.entities.map(item => (
                          <div key={item.entity.entityId} style={{ width: "100%", borderLeft: "2px solid #dbe7f5", paddingLeft: 10 }}>
                            <Button
                              data-testid={`domain-model-entity-tree-${item.entity.entityId}`}
                              theme={item.entity.entityId === selectedEntity?.entityId ? "solid" : "borderless"}
                              type={item.entity.entityId === selectedEntity?.entityId ? "primary" : "tertiary"}
                              style={{ justifyContent: "space-between", width: "100%" }}
                              onClick={() => {
                                setSelectedRelationId(undefined);
                                setSelectedEntityId(item.entity.entityId);
                              }}
                            >
                              <span>{item.entity.name}</span>
                            </Button>
                            <Space vertical align="start" spacing={4} style={{ width: "100%", marginTop: 4 }}>
                              {item.entity.attributes.map(attribute => (
                                <Button
                                  data-testid={`domain-model-attribute-tree-${attribute.attributeId}`}
                                  key={attribute.attributeId}
                                  theme="borderless"
                                  type="tertiary"
                                  size="small"
                                  style={{ justifyContent: "flex-start", width: "100%" }}
                                  onClick={() => {
                                    setSelectedRelationId(undefined);
                                    setSelectedEntityId(item.entity.entityId);
                                  }}
                                >
                                  {attribute.name}
                                </Button>
                              ))}
                              {item.associations.map(association => (
                                <Button
                                  data-testid={`domain-model-association-tree-${association.associationId}`}
                                  key={association.associationId}
                                  theme={association.associationId === selectedRelation?.associationId ? "solid" : "borderless"}
                                  type={association.associationId === selectedRelation?.associationId ? "warning" : "tertiary"}
                                  size="small"
                                  style={{ justifyContent: "flex-start", width: "100%" }}
                                  onClick={() => {
                                    setSelectedEntityId(undefined);
                                    setSelectedRelationId(association.associationId);
                                  }}
                                >
                                  {association.name}
                                </Button>
                              ))}
                            </Space>
                          </div>
                        ))}
                      </Space>
                    </div>
                  ))}
                </Space>
              </Card>
            ))}
            <Title heading={6} style={{ margin: "8px 0 0" }}>{c.entitiesTitle}</Title>
            {document.entities.length === 0 ? <Text type="tertiary">{c.noEntities}</Text> : document.entities.map(entity => (
              <Button
                data-testid={`domain-model-entity-list-${entity.entityId}`}
                key={entity.entityId}
                theme={entity.entityId === selectedEntity?.entityId ? "solid" : "borderless"}
                type={entity.entityId === selectedEntity?.entityId ? "primary" : "tertiary"}
                style={{ justifyContent: "flex-start", width: "100%" }}
                onClick={() => {
                  setSelectedRelationId(undefined);
                  setSelectedEntityId(entity.entityId);
                }}
              >
                {entity.qualifiedName}
              </Button>
            ))}
          </Space>
        </Card>

        <Card data-testid="domain-model-canvas-panel" bodyStyle={{ padding: 0, position: "relative", minHeight: 520, overflow: "auto", background: "linear-gradient(180deg, #fbfdff 0%, #f3f7fb 100%)" }}>
          {document.entities.length === 0 ? (
            <div style={{ display: "grid", placeItems: "center", height: "100%" }}>
              <Empty title={c.noEntities} description={c.selectEntityHint} />
            </div>
          ) : (
            <div style={{ position: "relative", minHeight: 700 }}>
              <svg style={{ position: "absolute", inset: 0, width: "100%", height: "100%", pointerEvents: "none", overflow: "visible" }}>
                {relationLines.map(line => (
                  <g key={line.association.associationId} data-testid={`domain-model-association-line-${line.association.associationId}`} style={{ pointerEvents: "auto", cursor: "pointer" }} onClick={() => {
                    setSelectedEntityId(undefined);
                    setSelectedRelationId(line.association.associationId);
                  }}>
                    <line
                      x1={line.x1}
                      y1={line.y1}
                      x2={line.x2}
                      y2={line.y2}
                      stroke={line.association.associationId === selectedRelation?.associationId ? "#f59e0b" : "#94a3b8"}
                      strokeWidth={line.association.associationId === selectedRelation?.associationId ? 3 : 2}
                      strokeDasharray={line.association.bindingMode === "logicalCrossDb" ? "8 6" : undefined}
                    />
                    <text
                      x={(line.x1 + line.x2) / 2}
                      y={(line.y1 + line.y2) / 2 - 6}
                      fill="#475569"
                      fontSize="12"
                      textAnchor="middle"
                    >
                      {line.association.name}
                    </text>
                  </g>
                ))}
              </svg>
              {document.entities.map(entity => {
                const frame = document.layout.entityFrames[entity.entityId] ?? { x: 48, y: 48, width: 280, height: 180 };
                return (
                  <Card
                    data-testid={`domain-model-entity-card-${entity.entityId}`}
                    key={entity.entityId}
                    style={{
                      position: "absolute",
                      left: frame.x,
                      top: frame.y,
                      width: frame.width,
                      minHeight: frame.height,
                      border: entity.entityId === selectedEntity?.entityId ? "2px solid #3b82f6" : "1px solid #d7e3f4",
                      boxShadow: entity.entityId === selectedEntity?.entityId ? "0 10px 24px rgba(59,130,246,0.16)" : "0 6px 18px rgba(15,23,42,0.06)"
                    }}
                    bodyStyle={{ padding: 12 }}
                    onClick={() => {
                      setSelectedRelationId(undefined);
                      setSelectedEntityId(entity.entityId);
                    }}
                  >
                    <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
                      <Space style={{ justifyContent: "space-between", width: "100%" }}>
                        <Text strong>{entity.name}</Text>
                        <Space spacing={4}>
                          <Tag color="blue">{entity.schemaName}</Tag>
                          <Tag color={entity.syncStatus === "clean" ? "green" : "orange"}>{entity.syncStatus}</Tag>
                        </Space>
                      </Space>
                      <Text type="tertiary">{entity.qualifiedName}</Text>
                      {entity.attributes.slice(0, 6).map(attribute => (
                        <Space key={attribute.attributeId} style={{ justifyContent: "space-between", width: "100%" }}>
                          <Text>{attribute.name}</Text>
                          <Text type="tertiary">{attribute.type}</Text>
                        </Space>
                      ))}
                    </Space>
                  </Card>
                );
              })}
            </div>
          )}
        </Card>

        <Card data-testid="domain-model-properties-panel" bodyStyle={{ padding: 12, height: "100%", overflow: "auto" }}>
          {selectedRelation ? (
            <Space vertical align="start" style={{ width: "100%" }}>
              <Title heading={6} style={{ margin: 0 }}>{selectedRelation.name}</Title>
              <Text type="tertiary">{selectedRelation.bindingMode === "logicalCrossDb" ? c.crossDatabaseLabel : c.relationPhysicalHint}</Text>
              <Form style={{ width: "100%" }}>
                <Form.Input label={c.relationNameLabel} value={selectedRelation.name} onChange={value => {
                  if (!document) return;
                  applyLocalDocument({
                    ...document,
                    associations: document.associations.map(association => association.associationId === selectedRelation.associationId ? { ...association, name: value } : association)
                  });
                }} />
                <Form.Select
                  label={c.sourceFieldLabel}
                  optionList={relationFieldOptions.source}
                  value={selectedRelation.sourceAttributeId}
                  onChange={value => {
                    if (!document) return;
                    applyLocalDocument({
                      ...document,
                      associations: document.associations.map(association => association.associationId === selectedRelation.associationId
                        ? { ...association, sourceAttributeId: String(value) }
                        : association)
                    });
                  }}
                />
                <Form.Select
                  label={c.targetFieldLabel}
                  optionList={relationFieldOptions.target}
                  value={selectedRelation.targetAttributeId}
                  onChange={value => {
                    if (!document) return;
                    applyLocalDocument({
                      ...document,
                      associations: document.associations.map(association => association.associationId === selectedRelation.associationId
                        ? { ...association, targetAttributeId: String(value) }
                        : association)
                    });
                  }}
                />
                <Form.Select
                  label={c.cardinalityLabel}
                  optionList={[
                    { label: "oneToOne", value: "oneToOne" },
                    { label: "oneToMany", value: "oneToMany" },
                    { label: "manyToOne", value: "manyToOne" }
                  ]}
                  value={selectedRelation.cardinality}
                  onChange={value => {
                    if (!document) return;
                    applyLocalDocument({
                      ...document,
                      associations: document.associations.map(association => association.associationId === selectedRelation.associationId
                        ? { ...association, cardinality: String(value) }
                        : association)
                    });
                  }}
                />
                <Form.Select
                  label={c.relationModeLabel}
                  optionList={[
                    { label: c.relationModePhysical, value: "physicalFk" },
                    { label: c.relationModeLogical, value: "logicalCrossDb" }
                  ]}
                  value={selectedRelation.bindingMode}
                  onChange={value => {
                    if (!document) return;
                    const bindingMode = String(value);
                    applyLocalDocument({
                      ...document,
                      associations: document.associations.map(association => association.associationId === selectedRelation.associationId
                        ? { ...association, bindingMode }
                        : association)
                    });
                  }}
                />
              </Form>
              <Button
                data-testid="domain-model-delete-relation"
                theme="light"
                type="danger"
                onClick={() => {
                  if (!document) return;
                  applyLocalDocument({
                    ...document,
                    associations: document.associations.filter(association => association.associationId !== selectedRelation.associationId)
                  });
                  setSelectedRelationId(undefined);
                  setSelectedEntityId(document.entities[0]?.entityId);
                }}
              >
                {c.deleteRelation}
              </Button>
            </Space>
          ) : !selectedEntity ? (
            <Empty description={c.noSelection} />
          ) : (
            <Space vertical align="start" style={{ width: "100%" }}>
              <Title heading={6} style={{ margin: 0 }}>{selectedEntity.qualifiedName}</Title>
              <Text type="tertiary">{c.syncState}: {document.syncState.status || "idle"}</Text>
              <Form style={{ width: "100%" }}>
                <Form.Input label={c.entityNameLabel} value={selectedEntity.name} onChange={value => {
                  if (!document) return;
                  const nextEntity = { ...selectedEntity, name: value };
                  applyLocalDocument({ ...document, entities: document.entities.map(entity => entity.entityId === nextEntity.entityId ? nextEntity : entity) });
                }} />
                <Form.Input label={c.schemaLabel} value={selectedEntity.schemaName} onChange={value => {
                  if (!document) return;
                  const nextEntity = { ...selectedEntity, schemaName: value };
                  applyLocalDocument({ ...document, entities: document.entities.map(entity => entity.entityId === nextEntity.entityId ? nextEntity : entity) });
                }} />
                <Form.Input label={c.tableLabel} value={selectedEntity.tableName} onChange={value => {
                  if (!document) return;
                  const nextEntity = { ...selectedEntity, tableName: value };
                  applyLocalDocument({ ...document, entities: document.entities.map(entity => entity.entityId === nextEntity.entityId ? nextEntity : entity) });
                }} />
              </Form>
              <Space style={{ justifyContent: "space-between", width: "100%" }}>
                <Title heading={6} style={{ margin: 0 }}>{c.entitiesTitle}</Title>
                <Button data-testid="domain-model-add-field" size="small" onClick={() => {
                  if (!document || !selectedEntity) return;
                  const nextEntity = {
                    ...selectedEntity,
                    attributes: [...selectedEntity.attributes, {
                      attributeId: `attr:${selectedEntity.entityId}:${selectedEntity.attributes.length + 1}`,
                      name: `Field${selectedEntity.attributes.length + 1}`,
                      columnName: `field_${selectedEntity.attributes.length + 1}`,
                      type: "string",
                      required: false,
                      primaryKey: false,
                      indexed: false
                    }]
                  };
                  applyLocalDocument({ ...document, entities: document.entities.map(entity => entity.entityId === nextEntity.entityId ? nextEntity : entity) });
                }}>{c.addField}</Button>
              </Space>
              <Divider margin="8px 0" />
              <Table pagination={false} columns={fieldColumns} dataSource={selectedEntity.attributes} rowKey="attributeId" />
            </Space>
          )}
        </Card>
      </div>

      <Card data-testid="domain-model-preview-panel" bodyStyle={{ padding: 12 }}>
        <Space vertical align="start" style={{ width: "100%" }}>
          <Title heading={6} style={{ margin: 0 }}>{c.previewTitle}</Title>
          <Text type="tertiary">{c.lastSync}: {document.syncState.lastSyncedAt ?? "-"}</Text>
          {previewPlan ? (
            <Space vertical align="start" style={{ width: "100%" }}>
              <Text>{`createTables: ${previewPlan.createTables.length}, addColumns: ${previewPlan.addColumns.length}`}</Text>
              <Text>{`alterColumns: ${previewPlan.alterColumns.length}, renameColumns: ${previewPlan.renameColumns.length}, dropColumns: ${previewPlan.dropColumns.length}`}</Text>
              <Text>{`createForeignKeys: ${previewPlan.createForeignKeys.length}, dropForeignKeys: ${previewPlan.dropForeignKeys.length}`}</Text>
              {previewPlan.warnings.map(item => <Text key={item} type="warning">{item}</Text>)}
              {previewPlan.errors.map(item => <Text key={item} type="danger">{item}</Text>)}
            </Space>
          ) : (
            <Text type="tertiary">{c.selectEntityHint}</Text>
          )}
        </Space>
      </Card>

      <SideSheet
        data-testid="domain-model-bind-sheet"
        title={c.bindModalTitle}
        visible={bindVisible}
        onCancel={() => setBindVisible(false)}
        footer={
          <Space>
            <Button data-testid="domain-model-bind-cancel" onClick={() => setBindVisible(false)}>Cancel</Button>
            <Button data-testid="domain-model-bind-submit" theme="solid" loading={saving} onClick={() => void handleSaveBindings()}>{c.bindDatabase}</Button>
          </Space>
        }
      >
        <Form>
          <Form.Select field="sourceId" label={c.sourceLabel} optionList={sourceOptions} value={bindSourceId} onChange={value => setBindSourceId(String(value))} />
          <Form.Input field="alias" label={c.aliasLabel} value={bindAlias} onChange={value => setBindAlias(value)} />
        </Form>
      </SideSheet>

      <SideSheet
        data-testid="domain-model-import-sheet"
        title={c.importModalTitle}
        visible={importVisible}
        onCancel={() => setImportVisible(false)}
        footer={
          <Space>
            <Button data-testid="domain-model-import-cancel" onClick={() => setImportVisible(false)}>Cancel</Button>
            <Button data-testid="domain-model-import-submit" theme="solid" loading={saving} onClick={() => void handleImportTables()}>{c.importTables}</Button>
          </Space>
        }
      >
        <Form>
          <Form.Select
            label={c.bindingLabel}
            optionList={bindingOptions}
            value={importBindingId}
            onChange={value => {
              const next = String(value);
              setImportBindingId(next);
              const binding = document.bindings.find(item => item.bindingId === next);
              if (binding) {
                void loadSchemas(binding.sourceId);
              }
            }}
          />
          <Form.Select
            label={c.schemaLabel}
            optionList={schemaOptions}
            value={importSchemaName}
            onChange={value => {
              const nextSchema = String(value);
              setImportSchemaName(nextSchema);
              const binding = document.bindings.find(item => item.bindingId === importBindingId);
              if (binding) {
                void loadTables(binding.sourceId, nextSchema);
              }
            }}
          />
          <Form.Select
            multiple
            label={c.tableLabel}
            optionList={tableOptions}
            value={importTableNames}
            onChange={value => setImportTableNames((value as string[]) ?? [])}
          />
        </Form>
      </SideSheet>

      <Modal data-testid="domain-model-preview-modal" title={c.previewTitle} visible={previewVisible} footer={null} onCancel={() => setPreviewVisible(false)}>
        <Space vertical align="start" style={{ width: "100%" }}>
          <Text>{`createTables: ${previewPlan?.createTables.length ?? 0}`}</Text>
          <Text>{`addColumns: ${previewPlan?.addColumns.length ?? 0}`}</Text>
          <Text>{`alterColumns: ${previewPlan?.alterColumns.length ?? 0}`}</Text>
          <Text>{`renameColumns: ${previewPlan?.renameColumns.length ?? 0}`}</Text>
          <Text>{`dropColumns: ${previewPlan?.dropColumns.length ?? 0}`}</Text>
          <Text>{`createForeignKeys: ${previewPlan?.createForeignKeys.length ?? 0}`}</Text>
          <Text>{`dropForeignKeys: ${previewPlan?.dropForeignKeys.length ?? 0}`}</Text>
          {previewPlan?.warnings.map(item => <Text key={item} type="warning">{item}</Text>)}
          {previewPlan?.errors.map(item => <Text key={item} type="danger">{item}</Text>)}
        </Space>
      </Modal>

      <Modal
        data-testid="domain-model-entity-modal"
        title={c.entityModalTitle}
        visible={entityVisible}
        onCancel={() => setEntityVisible(false)}
        onOk={handleCreateEntity}
      >
        <Form>
          <Form.Input label={c.entityNameLabel} value={newEntityName} onChange={value => setNewEntityName(value)} />
          <Form.Select label={c.bindingLabel} optionList={bindingOptions} value={newEntityBindingId} onChange={value => setNewEntityBindingId(String(value))} />
        </Form>
      </Modal>

      <Modal
        data-testid="domain-model-relation-modal"
        title={c.relationModalTitle}
        visible={relationVisible}
        onCancel={() => setRelationVisible(false)}
        onOk={handleCreateRelation}
      >
        <Form>
          <Form.Input label={c.relationNameLabel} value={newRelationName} onChange={value => setNewRelationName(value)} />
          <Form.Select label={c.sourceEntityLabel} optionList={entityOptions} value={newRelationSourceEntityId} onChange={value => setNewRelationSourceEntityId(String(value))} />
          <Form.Select label={c.sourceFieldLabel} optionList={sourceFieldOptions} value={newRelationSourceAttributeId} onChange={value => setNewRelationSourceAttributeId(String(value))} />
          <Form.Select label={c.targetEntityLabel} optionList={entityOptions} value={newRelationTargetEntityId} onChange={value => setNewRelationTargetEntityId(String(value))} />
          <Form.Select label={c.targetFieldLabel} optionList={targetFieldOptions} value={newRelationTargetAttributeId} onChange={value => setNewRelationTargetAttributeId(String(value))} />
          <Form.Switch label={c.crossDatabaseLabel} checked={newRelationCrossDatabase} onChange={checked => setNewRelationCrossDatabase(checked)} />
        </Form>
      </Modal>
    </div>
  );
}
