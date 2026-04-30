import { useCallback, useEffect, useMemo, useState } from "react";
import { Button, Card, Empty, Form, Input, Modal, Select, SideSheet, Space, Spin, Table, Tag, Toast, Typography } from "@douyinfe/semi-ui";
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

interface DomainModelDocument {
  appId: string;
  workspaceId: string;
  moduleId: string;
  bindings: DomainModelBinding[];
  entities: DomainModelEntity[];
  associations: Array<Record<string, unknown>>;
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
    associationCount: 0,
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
  const [saving, setSaving] = useState(false);
  const [syncing, setSyncing] = useState(false);
  const [sources, setSources] = useState<DatabaseSourceSummary[]>([]);
  const [schemas, setSchemas] = useState<DatabaseSchemaSummary[]>([]);
  const [tables, setTables] = useState<string[]>([]);
  const [selectedEntityId, setSelectedEntityId] = useState<string>();
  const [bindSourceId, setBindSourceId] = useState<string>();
  const [bindAlias, setBindAlias] = useState("");
  const [importBindingId, setImportBindingId] = useState<string>();
  const [importSchemaName, setImportSchemaName] = useState<string>();
  const [importTableNames, setImportTableNames] = useState<string[]>([]);
  const setAppAssetModules = useMendixStudioStore(state => state.setAppAssetModules);

  const moduleId = tab.moduleId ?? tab.resourceId ?? "";

  const applyModuleEntities = useCallback((nextDocument: DomainModelDocument) => {
    const nextModules = modules.map(module => module.moduleId === moduleId
      ? { ...module, entities: toModuleEntities(nextDocument) }
      : module);
    setAppAssetModules(nextModules);
  }, [moduleId, modules, setAppAssetModules]);

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
      setDocument(result);
      setSelectedEntityId(current => current ?? result.entities[0]?.entityId);
      applyModuleEntities(result);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : c.noApi);
      const fallback = createEmptyDocument(appId, workspaceId, moduleId);
      setDocument(fallback);
      applyModuleEntities(fallback);
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
      setDocument(result);
      applyModuleEntities(result);
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
      setDocument(result);
      applyModuleEntities(result);
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
      setDocument(result.document);
      setSelectedEntityId(result.document.entities[0]?.entityId);
      applyModuleEntities(result.document);
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
      setDocument(result.document);
      setPreviewPlan(result.plan);
      applyModuleEntities(result.document);
      Toast.success(c.syncSuccess);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : c.noApi);
    } finally {
      setSyncing(false);
    }
  }, [appId, workspaceId, moduleId, request, applyModuleEntities, c.syncSuccess, c.noApi]);

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
            setDocument({ ...document, entities: document.entities.map(entity => entity.entityId === nextEntity.entityId ? nextEntity : entity) });
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
            setDocument({ ...document, entities: document.entities.map(entity => entity.entityId === nextEntity.entityId ? nextEntity : entity) });
          }}
        />
      )
    },
    {
      title: c.required,
      dataIndex: "required",
      render: (_value, record) => record.required ? <Tag color="red">Y</Tag> : <Tag>N</Tag>
    },
    {
      title: c.primaryKey,
      dataIndex: "primaryKey",
      render: (_value, record) => record.primaryKey ? <Tag color="blue">PK</Tag> : "-"
    }
  ], [c.fieldName, c.fieldType, c.required, c.primaryKey, selectedEntity, document]);

  if (loading) {
    return <div className="studio-readonly-resource" style={{ display: "grid", placeItems: "center" }}><Spin /></div>;
  }

  if (!document) {
    return <div className="studio-readonly-resource"><Empty description={c.noApi} /></div>;
  }

  return (
    <div className="studio-readonly-resource" style={{ display: "flex", flexDirection: "column", gap: 12, padding: 16 }}>
      <Space style={{ justifyContent: "space-between", width: "100%" }}>
        <div>
          <Title heading={5} style={{ margin: 0 }}>{tab.title}</Title>
          <Text type="tertiary">{c.title}</Text>
        </div>
        <Space wrap>
          <Button loading={saving} onClick={() => void saveDocument(document)}>{c.saveModel}</Button>
          <Button onClick={() => { setBindVisible(true); void loadSources(); }}>{c.bindDatabase}</Button>
          <Button disabled={document.bindings.length === 0} onClick={() => setImportVisible(true)}>{c.importTables}</Button>
          <Button onClick={() => void handlePreviewSync()}>{c.previewSync}</Button>
          <Button theme="solid" loading={syncing} onClick={() => void handleSyncDraft()}>{c.syncDraft}</Button>
          <Button onClick={() => void loadDocument()}>{c.refresh}</Button>
        </Space>
      </Space>

      {!apiClient ? <Card><Text type="danger">{c.noApi}</Text></Card> : null}

      <div style={{ display: "grid", gridTemplateColumns: "260px minmax(0, 1fr) 360px", gap: 12, minHeight: 520 }}>
        <Card bodyStyle={{ padding: 12, height: "100%", overflow: "auto" }}>
          <Space vertical align="start" style={{ width: "100%" }}>
            <Title heading={6} style={{ margin: 0 }}>{c.bindingsTitle}</Title>
            {(document.bindings.length === 0) ? <Text type="tertiary">{c.noEntities}</Text> : document.bindings.map(binding => (
              <Card key={binding.bindingId} style={{ width: "100%" }} bodyStyle={{ padding: 10 }}>
                <Space vertical align="start" spacing={4}>
                  <Text strong>{binding.alias}</Text>
                  <Text type="tertiary">{binding.displayName ?? binding.sourceId}</Text>
                  <Tag>{binding.driverCode}</Tag>
                </Space>
              </Card>
            ))}
            <Title heading={6} style={{ margin: "8px 0 0" }}>{c.entitiesTitle}</Title>
            {document.entities.length === 0 ? <Text type="tertiary">{c.noEntities}</Text> : document.entities.map(entity => (
              <Button
                key={entity.entityId}
                theme={entity.entityId === selectedEntity?.entityId ? "solid" : "borderless"}
                type={entity.entityId === selectedEntity?.entityId ? "primary" : "tertiary"}
                style={{ justifyContent: "flex-start", width: "100%" }}
                onClick={() => setSelectedEntityId(entity.entityId)}
              >
                {entity.qualifiedName}
              </Button>
            ))}
          </Space>
        </Card>

        <Card bodyStyle={{ padding: 0, position: "relative", minHeight: 520, overflow: "auto", background: "linear-gradient(180deg, #fbfdff 0%, #f3f7fb 100%)" }}>
          {document.entities.length === 0 ? (
            <div style={{ display: "grid", placeItems: "center", height: "100%" }}>
              <Empty title={c.noEntities} description={c.selectEntityHint} />
            </div>
          ) : (
            <div style={{ position: "relative", minHeight: 700 }}>
              {document.entities.map(entity => {
                const frame = document.layout.entityFrames[entity.entityId] ?? { x: 48, y: 48, width: 280, height: 180 };
                return (
                  <Card
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
                    onClick={() => setSelectedEntityId(entity.entityId)}
                  >
                    <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
                      <Space style={{ justifyContent: "space-between", width: "100%" }}>
                        <Text strong>{entity.name}</Text>
                        <Tag color="blue">{entity.schemaName}</Tag>
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

        <Card bodyStyle={{ padding: 12, height: "100%", overflow: "auto" }}>
          {!selectedEntity ? (
            <Empty description={c.noSelection} />
          ) : (
            <Space vertical align="start" style={{ width: "100%" }}>
              <Title heading={6} style={{ margin: 0 }}>{selectedEntity.qualifiedName}</Title>
              <Text type="tertiary">{c.syncState}: {document.syncState.status || "idle"}</Text>
              <Form style={{ width: "100%" }}>
                <Form.Input label="Entity" value={selectedEntity.name} onChange={value => {
                  if (!document) return;
                  const nextEntity = { ...selectedEntity, name: value };
                  setDocument({ ...document, entities: document.entities.map(entity => entity.entityId === nextEntity.entityId ? nextEntity : entity) });
                }} />
                <Form.Input label={c.schemaLabel} value={selectedEntity.schemaName} onChange={value => {
                  if (!document) return;
                  const nextEntity = { ...selectedEntity, schemaName: value };
                  setDocument({ ...document, entities: document.entities.map(entity => entity.entityId === nextEntity.entityId ? nextEntity : entity) });
                }} />
                <Form.Input label="Table" value={selectedEntity.tableName} onChange={value => {
                  if (!document) return;
                  const nextEntity = { ...selectedEntity, tableName: value };
                  setDocument({ ...document, entities: document.entities.map(entity => entity.entityId === nextEntity.entityId ? nextEntity : entity) });
                }} />
              </Form>
              <Space style={{ justifyContent: "space-between", width: "100%" }}>
                <Title heading={6} style={{ margin: 0 }}>{c.entitiesTitle}</Title>
                <Button size="small" onClick={() => {
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
                  setDocument({ ...document, entities: document.entities.map(entity => entity.entityId === nextEntity.entityId ? nextEntity : entity) });
                }}>{c.addField}</Button>
              </Space>
              <Table pagination={false} columns={fieldColumns} dataSource={selectedEntity.attributes} rowKey="attributeId" />
            </Space>
          )}
        </Card>
      </div>

      <Card bodyStyle={{ padding: 12 }}>
        <Space vertical align="start" style={{ width: "100%" }}>
          <Title heading={6} style={{ margin: 0 }}>{c.previewTitle}</Title>
          <Text type="tertiary">{c.lastSync}: {document.syncState.lastSyncedAt ?? "-"}</Text>
          {previewPlan ? (
            <Space vertical align="start" style={{ width: "100%" }}>
              <Text>{`createTables: ${previewPlan.createTables.length}, addColumns: ${previewPlan.addColumns.length}`}</Text>
              {previewPlan.warnings.map(item => <Text key={item} type="warning">{item}</Text>)}
              {previewPlan.errors.map(item => <Text key={item} type="danger">{item}</Text>)}
            </Space>
          ) : (
            <Text type="tertiary">{c.selectEntityHint}</Text>
          )}
        </Space>
      </Card>

      <SideSheet
        title={c.bindModalTitle}
        visible={bindVisible}
        onCancel={() => setBindVisible(false)}
        footer={
          <Space>
            <Button onClick={() => setBindVisible(false)}>Cancel</Button>
            <Button theme="solid" loading={saving} onClick={() => void handleSaveBindings()}>{c.bindDatabase}</Button>
          </Space>
        }
      >
        <Form>
          <Form.Select label={c.sourceLabel} optionList={sourceOptions} value={bindSourceId} onChange={value => setBindSourceId(String(value))} />
          <Form.Input label={c.aliasLabel} value={bindAlias} onChange={value => setBindAlias(value)} />
        </Form>
      </SideSheet>

      <SideSheet
        title={c.importModalTitle}
        visible={importVisible}
        onCancel={() => setImportVisible(false)}
        footer={
          <Space>
            <Button onClick={() => setImportVisible(false)}>Cancel</Button>
            <Button theme="solid" loading={saving} onClick={() => void handleImportTables()}>{c.importTables}</Button>
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

      <Modal title={c.previewTitle} visible={previewVisible} footer={null} onCancel={() => setPreviewVisible(false)}>
        <Space vertical align="start" style={{ width: "100%" }}>
          <Text>{`createTables: ${previewPlan?.createTables.length ?? 0}`}</Text>
          <Text>{`addColumns: ${previewPlan?.addColumns.length ?? 0}`}</Text>
          {previewPlan?.warnings.map(item => <Text key={item} type="warning">{item}</Text>)}
          {previewPlan?.errors.map(item => <Text key={item} type="danger">{item}</Text>)}
        </Space>
      </Modal>
    </div>
  );
}
