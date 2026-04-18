import { startTransition, useDeferredValue, useEffect, useMemo, useState } from "react";
import type { DragEvent, MouseEvent } from "react";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import {
  Banner,
  Button,
  Dropdown,
  Empty,
  Input,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  TextArea,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import { IconPlus, IconSafe, IconSearch } from "@douyinfe/semi-icons";
import type {
  AiLibraryItem,
  KnowledgeBaseCreateRequest,
  KnowledgeBaseType,
  LibraryPageProps,
  ResourceType
} from "../types";
import { getLibraryCopy } from "../copy";
import {
  formatDateTime,
  isKnowledgeType,
  mapKnowledgeBaseToLibraryItem,
  mapKnowledgeType,
  normalizeResourcePath,
  resolveKnowledgeStatus
} from "../utils";

interface CreateFormState extends KnowledgeBaseCreateRequest {}

interface PluginCreateFormState {
  name: string;
  description: string;
  icon: string;
  category: string;
  type: 0 | 1;
  sourceType: 0 | 1 | 2;
  authType: 0 | 1 | 2 | 3 | 4;
  definitionJson: string;
  authConfigJson: string;
  toolSchemaJson: string;
  openApiSpecJson: string;
}

interface DatabaseSchemaColumn {
  id: string;
  name: string;
  label: string;
  type: string;
  required: boolean;
  defaultValue: string;
  description: string;
  unique: boolean;
  indexed: boolean;
  length: string;
  min: string;
  max: string;
}

interface DatabaseCreateFormState {
  name: string;
  description: string;
  botId: string;
  schemaMode: "structured" | "raw";
  columns: DatabaseSchemaColumn[];
  rawSchemaJson: string;
}

interface PluginPreviewOperation {
  method: string;
  path: string;
  name: string;
}

const DEFAULT_CREATE_FORM: CreateFormState = {
  name: "",
  description: "",
  type: 0
};

const DEFAULT_PLUGIN_FORM: PluginCreateFormState = {
  name: "",
  description: "",
  icon: "",
  category: "",
  type: 0,
  sourceType: 0,
  authType: 0,
  definitionJson: "{}",
  authConfigJson: "{}",
  toolSchemaJson: "{}",
  openApiSpecJson: "{}"
};

const DEFAULT_DATABASE_COLUMNS: DatabaseSchemaColumn[] = [
  {
    id: "col-id",
    name: "id",
    label: "",
    type: "string",
    required: true,
    defaultValue: "",
    description: "",
    unique: true,
    indexed: true,
    length: "",
    min: "",
    max: ""
  }
];

const DEFAULT_DATABASE_FORM: DatabaseCreateFormState = {
  name: "",
  description: "",
  botId: "",
  schemaMode: "structured",
  columns: DEFAULT_DATABASE_COLUMNS,
  rawSchemaJson: '[{"name":"id","type":"string"}]'
};

type CreateResourceKind = "knowledge-base" | "plugin" | "database";

export function LibraryPage({ api, locale, appKey, spaceId, onNavigate }: LibraryPageProps) {
  const copy = getLibraryCopy(locale);
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState<ResourceType | "all">("all");
  const [subTypeFilter, setSubTypeFilter] = useState<string>("all");
  const [statusFilter, setStatusFilter] = useState<string>("all");
  const [pageIndex, setPageIndex] = useState(1);
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<AiLibraryItem[]>([]);
  const [total, setTotal] = useState(0);
  const [createVisible, setCreateVisible] = useState(false);
  const [creating, setCreating] = useState(false);
  const [createKind, setCreateKind] = useState<CreateResourceKind>("knowledge-base");
  const [form, setForm] = useState<CreateFormState>(DEFAULT_CREATE_FORM);
  const [pluginForm, setPluginForm] = useState<PluginCreateFormState>(DEFAULT_PLUGIN_FORM);
  const [databaseForm, setDatabaseForm] = useState<DatabaseCreateFormState>(DEFAULT_DATABASE_FORM);
  const [draggingColumnId, setDraggingColumnId] = useState<string>("");
  const [pluginPreviewStage, setPluginPreviewStage] = useState<"edit" | "preview">("edit");
  const deferredSearch = useDeferredValue(search.trim());
  const pageSize = 20;

  useEffect(() => {
    let disposed = false;

    async function load() {
      setLoading(true);
      try {
        if (typeFilter === "knowledge-base") {
          const response = await api.listKnowledgeBases(
            { pageIndex, pageSize, keyword: deferredSearch },
            deferredSearch || undefined
          );
          if (disposed) {
            return;
          }

          startTransition(() => {
            setItems(response.items.map(mapKnowledgeBaseToLibraryItem));
            setTotal(Number(response.total ?? 0));
          });
          return;
        }

        const response = await api.listLibrary(
          { pageIndex, pageSize, keyword: deferredSearch },
          typeFilter === "all" ? undefined : typeFilter
        );

        if (disposed) {
          return;
        }

        startTransition(() => {
          setItems(response.items);
          setTotal(Number(response.total ?? 0));
        });
      } catch (error) {
        if (!disposed) {
          Toast.error((error as Error).message);
        }
      } finally {
        if (!disposed) {
          setLoading(false);
        }
      }
    }

    void load();
    return () => {
      disposed = true;
    };
  }, [api, deferredSearch, pageIndex, typeFilter]);

  const filteredItems = useMemo(() => (
    items.filter((item: AiLibraryItem) => {
      const matchSubtype = subTypeFilter === "all"
        ? true
        : String(item.resourceSubType ?? "") === subTypeFilter;
      const matchStatus = statusFilter === "all"
        ? true
        : resolveKnowledgeStatus(item.status, item.documentCount, item.chunkCount) === statusFilter;
      return matchSubtype && matchStatus;
    })
  ), [items, statusFilter, subTypeFilter]);

  const structuredDatabaseSchemaJson = useMemo(() => JSON.stringify(
    databaseForm.columns
      .filter(column => column.name.trim())
      .map(column => ({
        name: column.name.trim(),
        label: column.label.trim() || undefined,
        type: column.type.trim() || "string",
        required: column.required,
        defaultValue: column.defaultValue.trim() || undefined,
        description: column.description.trim() || undefined,
        unique: column.unique || undefined,
        indexed: column.indexed || undefined,
        length: column.length.trim() ? Number(column.length.trim()) : undefined,
        min: column.min.trim() ? Number(column.min.trim()) : undefined,
        max: column.max.trim() ? Number(column.max.trim()) : undefined
      })),
    null,
    2
  ), [databaseForm.columns]);

  const pluginPreview = useMemo(() => {
    if (pluginForm.sourceType !== 1 || !pluginForm.openApiSpecJson.trim()) {
      return { valid: true, operations: [], schemaCount: 0 };
    }

    try {
      const parsed = JSON.parse(pluginForm.openApiSpecJson) as {
        paths?: Record<string, Record<string, { operationId?: string; summary?: string }>>;
        components?: { schemas?: Record<string, unknown> };
      };
      const operations = Object.entries(parsed.paths ?? {}).flatMap(([path, methods]) =>
        Object.entries(methods ?? {}).map(([method, config]) => ({
          method: method.toUpperCase(),
          path,
          name: config.operationId || config.summary || `${method.toUpperCase()} ${path}`
        } satisfies PluginPreviewOperation))
      );
      const schemaCount = Object.keys(parsed.components?.schemas ?? {}).length;
      return {
        valid: true,
        operations: operations as PluginPreviewOperation[],
        schemaCount
      };
    } catch {
      return {
        valid: false,
        operations: [] as PluginPreviewOperation[],
        schemaCount: 0
      };
    }
  }, [pluginForm.openApiSpecJson, pluginForm.sourceType]);

  const databaseValidationErrors = useMemo(() => {
    if (createKind !== "database" || databaseForm.schemaMode !== "structured") {
      return [];
    }

    const errors: string[] = [];
    const normalizedNames = new Set<string>();
    for (const column of databaseForm.columns) {
      const name = column.name.trim();
      if (!name) {
        errors.push(`${copy.databaseFieldName} 不能为空`);
        continue;
      }

      if (normalizedNames.has(name.toLowerCase())) {
        errors.push(`${copy.databaseFieldName} 重复: ${name}`);
      } else {
        normalizedNames.add(name.toLowerCase());
      }

      const length = column.length.trim() ? Number(column.length.trim()) : null;
      const min = column.min.trim() ? Number(column.min.trim()) : null;
      const max = column.max.trim() ? Number(column.max.trim()) : null;

      if (length !== null && (!Number.isFinite(length) || length <= 0)) {
        errors.push(`${name}: ${copy.databaseFieldLength} 必须是正数`);
      }

      if (min !== null && !Number.isFinite(min)) {
        errors.push(`${name}: ${copy.databaseFieldMin} 必须是数字`);
      }

      if (max !== null && !Number.isFinite(max)) {
        errors.push(`${name}: ${copy.databaseFieldMax} 必须是数字`);
      }

      if (min !== null && max !== null && min > max) {
        errors.push(`${name}: ${copy.databaseFieldMin} 不能大于 ${copy.databaseFieldMax}`);
      }

      if ((column.type === "string" || column.type === "json") && min !== null) {
        errors.push(`${name}: ${copy.databaseFieldMin} 不适用于 ${column.type}`);
      }

      if ((column.type === "string" || column.type === "json") && max !== null) {
        errors.push(`${name}: ${copy.databaseFieldMax} 不适用于 ${column.type}`);
      }
    }

    return errors;
  }, [copy.databaseFieldLength, copy.databaseFieldMax, copy.databaseFieldMin, copy.databaseFieldName, createKind, databaseForm.columns, databaseForm.schemaMode]);

  const columns = useMemo<ColumnProps<AiLibraryItem>[]>(() => [
    {
      title: copy.title,
      dataIndex: "name",
      render: (_value: unknown, record: AiLibraryItem) => (
        <div className="atlas-library-item">
          <div className="atlas-library-item__icon">
            <IconSafe />
          </div>
          <div className="atlas-library-item__body">
            <div className="atlas-library-item__name">{record.name}</div>
            <div className="atlas-library-item__desc">{record.description || copy.noDescription}</div>
          </div>
        </div>
      )
    },
    {
      title: copy.resourceType,
      dataIndex: "resourceType",
      width: 160,
      render: (_value: unknown, record: AiLibraryItem) => (
        <Space spacing={6}>
          <Tag color="light-blue">{copy.resourceLabels[record.resourceType]}</Tag>
          {record.resourceType === "knowledge-base" && record.resourceSubType ? (
            <Tag color="grey">
              {copy.typeLabels[(record.resourceSubType === "table" ? 1 : record.resourceSubType === "image" ? 2 : 0) as KnowledgeBaseType]}
            </Tag>
          ) : null}
          {record.resourceType === "workflow" ? (
            <Tag color={record.path.startsWith("/chat_flow/") ? "purple" : "cyan"}>
              {record.path.startsWith("/chat_flow/")
                ? copy.workflowModeLabels.chatflow
                : copy.workflowModeLabels.workflow}
            </Tag>
          ) : null}
        </Space>
      )
    },
    {
      title: copy.documents,
      dataIndex: "documentCount",
      width: 110,
      render: (value: unknown) => String(value ?? "-")
    },
    {
      title: copy.chunks,
      dataIndex: "chunkCount",
      width: 110,
      render: (value: unknown) => String(value ?? "-")
    },
    {
      title: copy.resourceStatus,
      dataIndex: "status",
      width: 120,
      render: (_value: unknown, record: AiLibraryItem) => {
        const status = resolveKnowledgeStatus(record.status, record.documentCount, record.chunkCount);
        const color = status === "ready" ? "green" : status === "failed" ? "red" : status === "disabled" ? "grey" : "orange";
        return <Tag color={color}>{copy.statusLabels[status] ?? status}</Tag>;
      }
    },
    {
      title: copy.updatedAt,
      dataIndex: "updatedAt",
      width: 180,
      render: (value: unknown) => formatDateTime(typeof value === "string" ? value : undefined)
    },
    {
      title: copy.actions,
      dataIndex: "path",
      width: 240,
      render: (_value: unknown, record: AiLibraryItem) => {
        const openDetail = (event: MouseEvent) => {
          event.stopPropagation();
          if (isKnowledgeType(record.resourceType)) {
            onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases/${record.resourceId}`);
            return;
          }

          onNavigate(normalizeResourcePath(record.path, appKey, spaceId));
        };

        const openWorkflowCanvas = (event: MouseEvent) => {
          event.stopPropagation();
          onNavigate(normalizeResourcePath(record.path, appKey, spaceId));
        };

        const openKnowledgeUpload = (event: MouseEvent) => {
          event.stopPropagation();
          onNavigate(
            `/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases/${record.resourceId}/upload?type=${mapKnowledgeType(
              record.resourceSubType === "table" ? 1 : record.resourceSubType === "image" ? 2 : 0
            )}`
          );
        };

        const downloadDatabaseTemplate = async (event: MouseEvent) => {
          event.stopPropagation();
          if (!api.downloadDatabaseTemplate) {
            return;
          }

          try {
            await api.downloadDatabaseTemplate(record.resourceId);
            Toast.success(copy.downloadTemplate);
          } catch (error) {
            Toast.error((error as Error).message);
          }
        };

        const publishPlugin = async (event: MouseEvent) => {
          event.stopPropagation();
          if (!api.publishPlugin) {
            return;
          }

          try {
            await api.publishPlugin(record.resourceId);
            Toast.success(copy.publish);
          } catch (error) {
            Toast.error((error as Error).message);
          }
        };

        const openAgentPublish = (event: MouseEvent) => {
          event.stopPropagation();
          const match = record.path.match(/^\/ai\/agents\/([^/]+)\/edit$/);
          if (!match) {
            return;
          }

          onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/assistants/${match[1]}/publish`);
        };

        const openAppPublish = (event: MouseEvent) => {
          event.stopPropagation();
          const match = record.path.match(/^\/ai\/apps\/([^/]+)\/edit$/);
          if (!match) {
            return;
          }

          onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/apps/${match[1]}/publish`);
        };

        const openAppWorkflow = async (event: MouseEvent) => {
          event.stopPropagation();
          if (!api.getApplicationDetail) {
            return;
          }

          try {
            const detail = await api.getApplicationDetail(record.resourceId);
            if (!detail.workflowId) {
              Toast.warning("当前应用还没有关联主工作流。");
              return;
            }

            onNavigate(`/apps/${encodeURIComponent(appKey)}/work_flow/${detail.workflowId}/editor`);
          } catch (error) {
            Toast.error((error as Error).message);
          }
        };

        return (
          <Space spacing={4} wrap>
            <Button theme="borderless" onClick={openDetail}>
              {copy.open}
            </Button>
            {isKnowledgeType(record.resourceType) ? (
              <Button theme="borderless" onClick={openKnowledgeUpload}>
                {copy.upload}
              </Button>
            ) : null}
            {record.resourceType === "database" ? (
              <Button theme="borderless" onClick={event => void downloadDatabaseTemplate(event)}>
                {copy.downloadTemplate}
              </Button>
            ) : null}
            {record.resourceType === "plugin" ? (
              <Button theme="borderless" onClick={event => void publishPlugin(event)}>
                {copy.publish}
              </Button>
            ) : null}
            {record.resourceType === "workflow" ? (
              <Button theme="borderless" onClick={openWorkflowCanvas}>
                {copy.openCanvas}
              </Button>
            ) : null}
            {record.resourceType === "agent" ? (
              <Button theme="borderless" onClick={openAgentPublish}>
                {copy.openPublish}
              </Button>
            ) : null}
            {record.resourceType === "app" ? (
              <Button theme="borderless" onClick={event => void openAppWorkflow(event)}>
                {copy.openWorkflow}
              </Button>
            ) : null}
            {record.resourceType === "app" ? (
              <Button theme="borderless" onClick={openAppPublish}>
                {copy.openPublish}
              </Button>
            ) : null}
          </Space>
        );
      }
    }
  ], [appKey, copy, onNavigate]);

  function moveDatabaseColumn(columnId: string, direction: -1 | 1) {
    setDatabaseForm(current => {
      const index = current.columns.findIndex(item => item.id === columnId);
      if (index < 0) {
        return current;
      }

      const nextIndex = index + direction;
      if (nextIndex < 0 || nextIndex >= current.columns.length) {
        return current;
      }

      const nextColumns = [...current.columns];
      const [currentColumn] = nextColumns.splice(index, 1);
      nextColumns.splice(nextIndex, 0, currentColumn);
      return {
        ...current,
        columns: nextColumns
      };
    });
  }

  function reorderDatabaseColumn(sourceId: string, targetId: string) {
    if (!sourceId || sourceId === targetId) {
      return;
    }

    setDatabaseForm(current => {
      const sourceIndex = current.columns.findIndex(item => item.id === sourceId);
      const targetIndex = current.columns.findIndex(item => item.id === targetId);
      if (sourceIndex < 0 || targetIndex < 0 || sourceIndex === targetIndex) {
        return current;
      }

      const nextColumns = [...current.columns];
      const [sourceColumn] = nextColumns.splice(sourceIndex, 1);
      nextColumns.splice(targetIndex, 0, sourceColumn);
      return {
        ...current,
        columns: nextColumns
      };
    });
  }

  async function handleCreateResource() {
    const normalizedName = createKind === "knowledge-base"
      ? form.name.trim()
      : createKind === "plugin"
        ? pluginForm.name.trim()
        : databaseForm.name.trim();

    if (!normalizedName) {
      Toast.warning(copy.createResource);
      return;
    }

    setCreating(true);
    try {
      let id = 0;
      if (createKind === "knowledge-base") {
        id = await api.createKnowledgeBase({
          name: form.name.trim(),
          description: form.description?.trim(),
          type: form.type
        });
      } else if (createKind === "plugin") {
        if (!api.createPlugin) {
          throw new Error(copy.createPlugin);
        }
        if (pluginForm.sourceType === 1 && !pluginPreview.valid) {
          Toast.error(copy.pluginValidateOpenApi);
          return;
        }
        id = await api.createPlugin({
          name: pluginForm.name.trim(),
          description: pluginForm.description.trim() || undefined,
          icon: pluginForm.icon.trim() || undefined,
          category: pluginForm.category.trim() || undefined,
          type: pluginForm.type,
          sourceType: pluginForm.sourceType,
          authType: pluginForm.authType,
          definitionJson: pluginForm.definitionJson.trim() || "{}",
          authConfigJson: pluginForm.authConfigJson.trim() || "{}",
          toolSchemaJson: pluginForm.toolSchemaJson.trim() || "{}",
          openApiSpecJson: pluginForm.openApiSpecJson.trim() || "{}"
        });
      } else {
        if (!api.createDatabase) {
          throw new Error(copy.createDatabase);
        }
        if (databaseValidationErrors.length > 0) {
          Toast.error(copy.databaseValidationFailed);
          return;
        }
        id = await api.createDatabase({
          name: databaseForm.name.trim(),
          description: databaseForm.description.trim() || undefined,
          botId: databaseForm.botId.trim() ? Number(databaseForm.botId.trim()) : undefined,
          tableSchema: databaseForm.schemaMode === "structured"
            ? structuredDatabaseSchemaJson
            : databaseForm.rawSchemaJson.trim()
        });
      }
      setCreateVisible(false);
      setForm(DEFAULT_CREATE_FORM);
      setPluginForm(DEFAULT_PLUGIN_FORM);
      setDatabaseForm(DEFAULT_DATABASE_FORM);
      setPluginPreviewStage("edit");
      Toast.success(createKind === "knowledge-base" ? copy.createKnowledge : createKind === "plugin" ? copy.createPlugin : copy.createDatabase);
      if (createKind === "knowledge-base") {
        onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases/${id}`);
      } else if (createKind === "plugin") {
        onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/plugins/${id}`);
      } else {
        onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/databases/${id}`);
      }
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setCreating(false);
    }
  }

  return (
    <div className="atlas-library-page" data-testid="app-library-page">
      <div className="atlas-page-header">
        <div>
          <Typography.Title heading={3} style={{ margin: 0 }}>
            {copy.title}
          </Typography.Title>
          <Typography.Text type="tertiary">
            {copy.createKnowledgeHint}
          </Typography.Text>
        </div>
        <Dropdown
          trigger="click"
          position="bottomRight"
          render={(
            <Dropdown.Menu>
              <Dropdown.Item icon={<IconPlus />} onClick={() => {
                setCreateKind("knowledge-base");
                setCreateVisible(true);
              }}>
                {copy.createKnowledge}
              </Dropdown.Item>
              {api.createPlugin ? (
                <Dropdown.Item icon={<IconPlus />} onClick={() => {
                  setCreateKind("plugin");
                  setCreateVisible(true);
                }}>
                  {copy.createPlugin}
                </Dropdown.Item>
              ) : null}
              {api.createDatabase ? (
                <Dropdown.Item icon={<IconPlus />} onClick={() => {
                  setCreateKind("database");
                  setCreateVisible(true);
                }}>
                  {copy.createDatabase}
                </Dropdown.Item>
              ) : null}
            </Dropdown.Menu>
          )}
        >
          <Button type="primary" icon={<IconPlus />}>
            {copy.createResource}
          </Button>
        </Dropdown>
      </div>

      <Banner type="info" icon={<IconSafe />} description={copy.createKnowledgeHint} />

      <div className="atlas-filter-card semi-card semi-card-bordered semi-card-shadow">
        <div className="semi-card-body">
          <Space wrap align="center">
            <Input
              prefix={<IconSearch />}
              placeholder={copy.searchPlaceholder}
              value={search}
              onChange={value => {
                setPageIndex(1);
                setSearch(value);
              }}
              style={{ width: 280 }}
            />
            <Select
              value={typeFilter}
              style={{ width: 180 }}
              onChange={value => {
                setPageIndex(1);
                setSubTypeFilter("all");
                setTypeFilter((value ?? "all") as ResourceType | "all");
              }}
              optionList={[
                { label: copy.allTypes, value: "all" },
                { label: copy.resourceLabels["knowledge-base"], value: "knowledge-base" },
                { label: copy.resourceLabels.agent, value: "agent" },
                { label: copy.resourceLabels.workflow, value: "workflow" },
                { label: copy.resourceLabels.app, value: "app" },
                { label: copy.resourceLabels.prompt, value: "prompt" }
              ]}
            />
            <Select
              value={subTypeFilter}
              style={{ width: 180 }}
              disabled={typeFilter !== "knowledge-base"}
              onChange={value => setSubTypeFilter(String(value ?? "all"))}
              optionList={[
                { label: copy.allTypes, value: "all" },
                { label: copy.typeLabels[0], value: "text" },
                { label: copy.typeLabels[1], value: "table" },
                { label: copy.typeLabels[2], value: "image" }
              ]}
            />
            <Select
              value={statusFilter}
              style={{ width: 180 }}
              onChange={value => setStatusFilter(String(value ?? "all"))}
              optionList={[
                { label: copy.allStatus, value: "all" },
                { label: copy.statusLabels.ready, value: "ready" },
                { label: copy.statusLabels.processing, value: "processing" },
                { label: copy.statusLabels.disabled, value: "disabled" },
                { label: copy.statusLabels.failed, value: "failed" }
              ]}
            />
          </Space>
        </div>
      </div>

      <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
        <div className="semi-card-body" style={{ padding: 0 }}>
          <Table
            rowKey="resourceId"
            loading={loading}
            columns={columns}
            dataSource={filteredItems}
            empty={<Empty description={copy.listEmpty} />}
            pagination={{
              currentPage: pageIndex,
              pageSize,
              total,
              onPageChange: setPageIndex
            }}
            onRow={(record?: AiLibraryItem) => ({
              onClick: () => {
                if (!record) {
                  return;
                }

                if (record.resourceType === "knowledge-base") {
                  onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases/${record.resourceId}`);
                  return;
                }

                onNavigate(normalizeResourcePath(record.path, appKey, spaceId));
              }
            })}
          />
        </div>
      </div>

      <Modal
        title={createKind === "knowledge-base" ? copy.createKnowledge : createKind === "plugin" ? copy.createPlugin : copy.createDatabase}
        visible={createVisible}
        confirmLoading={creating}
        onOk={handleCreateResource}
        onCancel={() => {
          setCreateVisible(false);
          setForm(DEFAULT_CREATE_FORM);
          setPluginForm(DEFAULT_PLUGIN_FORM);
          setDatabaseForm(DEFAULT_DATABASE_FORM);
          setPluginPreviewStage("edit");
        }}
        okText={copy.create}
        cancelText={copy.cancel}
      >
        <Space vertical align="start" style={{ width: "100%" }}>
          <Input
            value={createKind === "knowledge-base" ? form.name : createKind === "plugin" ? pluginForm.name : databaseForm.name}
            placeholder={createKind === "knowledge-base" ? copy.createKnowledge : createKind === "plugin" ? copy.createPlugin : copy.createDatabase}
            onChange={value => {
              if (createKind === "knowledge-base") {
                setForm((current: CreateFormState) => ({ ...current, name: value }));
              } else if (createKind === "plugin") {
                setPluginForm(current => ({ ...current, name: value }));
              } else {
                setDatabaseForm(current => ({ ...current, name: value }));
              }
            }}
          />
          {createKind === "knowledge-base" ? (
            <>
              <Select
                value={form.type}
                style={{ width: "100%" }}
                optionList={[
                  { label: copy.typeLabels[0], value: 0 },
                  { label: copy.typeLabels[1], value: 1 },
                  { label: copy.typeLabels[2], value: 2 }
                ]}
                onChange={value => setForm((current: CreateFormState) => ({ ...current, type: Number(value) as KnowledgeBaseType }))}
              />
              <Typography.Text type="tertiary">
                {form.type === 1 ? copy.createTableKbHint : form.type === 2 ? copy.createImageKbHint : copy.createTextKbHint}
              </Typography.Text>
            </>
          ) : null}
          {createKind === "plugin" ? (
            <div className="atlas-library-create-stack">
              {pluginPreviewStage === "edit" ? (
                <>
                  <div className="atlas-library-create-section">
                    <Typography.Text strong>{copy.pluginBasicInfo}</Typography.Text>
                    <Input value={pluginForm.description} placeholder={copy.noDescription} onChange={value => setPluginForm(current => ({ ...current, description: value }))} />
                    <Input value={pluginForm.icon} placeholder="icon" onChange={value => setPluginForm(current => ({ ...current, icon: value }))} />
                    <Input value={pluginForm.category} placeholder={copy.resourceType} onChange={value => setPluginForm(current => ({ ...current, category: value }))} />
                  </div>
                  <div className="atlas-library-create-section">
                    <Typography.Text strong>{copy.pluginSourceAndAuth}</Typography.Text>
                    <Select
                      value={pluginForm.type}
                      optionList={[
                        { label: "Custom", value: 0 },
                        { label: "BuiltIn", value: 1 }
                      ]}
                      onChange={value => setPluginForm(current => ({ ...current, type: Number(value) as 0 | 1 }))}
                    />
                    <Select
                      value={pluginForm.sourceType}
                      optionList={[
                        { label: "Manual", value: 0 },
                        { label: "OpenApiImport", value: 1 },
                        { label: "BuiltInCatalog", value: 2 }
                      ]}
                      onChange={value => {
                        const nextSourceType = Number(value) as 0 | 1 | 2;
                        setPluginPreviewStage("edit");
                        setPluginForm(current => ({ ...current, sourceType: nextSourceType }));
                      }}
                    />
                    <Select
                      value={pluginForm.authType}
                      optionList={[
                        { label: "None", value: 0 },
                        { label: "ApiKey", value: 1 },
                        { label: "BearerToken", value: 2 },
                        { label: "Basic", value: 3 },
                        { label: "Custom", value: 4 }
                      ]}
                      onChange={value => setPluginForm(current => ({ ...current, authType: Number(value) as 0 | 1 | 2 | 3 | 4 }))}
                    />
                  </div>
                  {pluginForm.sourceType === 1 ? (
                    <div className="atlas-library-create-guide">
                      <Typography.Text strong>{copy.pluginOpenApiGuide}</Typography.Text>
                      <Typography.Text type="tertiary">{copy.pluginOpenApiHint}</Typography.Text>
                      <Button
                        theme="light"
                        onClick={() => setPluginForm(current => ({
                          ...current,
                          openApiSpecJson: JSON.stringify({
                            openapi: "3.0.0",
                            info: { title: current.name || "Sample API", version: "1.0.0" },
                            paths: {
                              "/health": {
                                get: {
                                  operationId: "healthCheck",
                                  summary: "Health Check",
                                  responses: {
                                    "200": {
                                      description: "OK"
                                    }
                                  }
                                }
                              },
                              "/items": {
                                post: {
                                  operationId: "createItem",
                                  summary: "Create Item",
                                  responses: {
                                    "200": {
                                      description: "OK"
                                    }
                                  }
                                }
                              }
                            },
                            components: {
                              schemas: {
                                Item: {
                                  type: "object",
                                  properties: {
                                    id: { type: "string" },
                                    title: { type: "string" }
                                  }
                                }
                              }
                            }
                          }, null, 2)
                        }))}
                      >
                        {copy.pluginPrefillOpenApi}
                      </Button>
                      <TextArea autosize value={pluginForm.openApiSpecJson} onChange={value => setPluginForm(current => ({ ...current, openApiSpecJson: value }))} placeholder="openApiSpecJson" />
                      <Button
                        type="primary"
                        theme="light"
                        onClick={() => {
                          if (!pluginPreview.valid) {
                            Toast.error(copy.pluginValidateOpenApi);
                            return;
                          }
                          setPluginPreviewStage("preview");
                        }}
                      >
                        {copy.pluginPreviewNext}
                      </Button>
                    </div>
                  ) : null}
                  <div className="atlas-library-create-section">
                    <Typography.Text strong>{copy.pluginAdvancedConfig}</Typography.Text>
                    <TextArea autosize value={pluginForm.definitionJson} onChange={value => setPluginForm(current => ({ ...current, definitionJson: value }))} placeholder="definitionJson" />
                    {pluginForm.authType !== 0 ? (
                      <TextArea autosize value={pluginForm.authConfigJson} onChange={value => setPluginForm(current => ({ ...current, authConfigJson: value }))} placeholder="authConfigJson" />
                    ) : null}
                    <TextArea autosize value={pluginForm.toolSchemaJson} onChange={value => setPluginForm(current => ({ ...current, toolSchemaJson: value }))} placeholder="toolSchemaJson" />
                  </div>
                </>
              ) : (
                <div className="atlas-library-create-stack">
                  <div className="atlas-library-create-section">
                    <Typography.Text strong>{copy.pluginPreviewSummary}</Typography.Text>
                    <div className="atlas-library-preview-grid">
                      <div className="atlas-library-preview-metric">
                        <span>{copy.pluginPreviewOperations}</span>
                        <strong>{pluginPreview.operations.length}</strong>
                      </div>
                      <div className="atlas-library-preview-metric">
                        <span>{copy.pluginPreviewSchemas}</span>
                        <strong>{pluginPreview.schemaCount}</strong>
                      </div>
                    </div>
                  </div>
                  {pluginPreview.operations.length === 0 ? (
                    <Empty description={copy.pluginPreviewEmpty} />
                  ) : (
                    <div className="atlas-library-preview-list">
                      {pluginPreview.operations.map(operation => (
                        <div key={`${operation.method}:${operation.path}`} className="atlas-library-preview-list__item">
                          <Tag color="cyan">{operation.method}</Tag>
                          <div>
                            <strong>{operation.name}</strong>
                            <span>{operation.path}</span>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                  <Button theme="light" onClick={() => setPluginPreviewStage("edit")}>
                    {copy.pluginPreviewBack}
                  </Button>
                </div>
              )}
            </div>
          ) : null}
          {createKind === "knowledge-base" ? (
            <TextArea
              autosize
              value={form.description}
              placeholder={copy.noDescription}
              onChange={value => setForm((current: CreateFormState) => ({ ...current, description: value }))}
            />
          ) : null}
          {createKind === "database" ? (
            <div className="atlas-library-create-stack">
              <div className="atlas-library-create-section">
                <Typography.Text strong>{copy.databaseBasicInfo}</Typography.Text>
                <Input value={databaseForm.description} placeholder={copy.noDescription} onChange={value => setDatabaseForm(current => ({ ...current, description: value }))} />
                <Input value={databaseForm.botId} placeholder="botId（可选）" onChange={value => setDatabaseForm(current => ({ ...current, botId: value }))} />
              </div>
              <div className="atlas-library-create-section">
                <Typography.Text strong>{copy.databaseSchemaMode}</Typography.Text>
                <Select
                  value={databaseForm.schemaMode}
                  optionList={[
                    { label: copy.databaseSchemaStructured, value: "structured" },
                    { label: copy.databaseSchemaRaw, value: "raw" }
                  ]}
                  onChange={value => setDatabaseForm(current => ({ ...current, schemaMode: String(value) as "structured" | "raw" }))}
                />
                {databaseForm.schemaMode === "structured" ? (
                  <div className="atlas-library-database-columns">
                    {databaseForm.columns.map(column => (
                      <div
                        key={column.id}
                        className="atlas-library-database-columns__row"
                        draggable
                        onDragStart={(event: DragEvent<HTMLDivElement>) => {
                          setDraggingColumnId(column.id);
                          event.dataTransfer.effectAllowed = "move";
                        }}
                        onDragOver={event => {
                          event.preventDefault();
                          event.dataTransfer.dropEffect = "move";
                        }}
                        onDrop={event => {
                          event.preventDefault();
                          reorderDatabaseColumn(draggingColumnId, column.id);
                          setDraggingColumnId("");
                        }}
                        onDragEnd={() => setDraggingColumnId("")}
                      >
                        <Input value={column.name} placeholder={copy.databaseFieldName} onChange={value => setDatabaseForm(current => ({
                          ...current,
                          columns: current.columns.map(item => item.id === column.id ? { ...item, name: value } : item)
                        }))} />
                        <Input value={column.label} placeholder={copy.databaseFieldLabel} onChange={value => setDatabaseForm(current => ({
                          ...current,
                          columns: current.columns.map(item => item.id === column.id ? { ...item, label: value } : item)
                        }))} />
                        <Select
                          value={column.type}
                          optionList={[
                            { label: "string", value: "string" },
                            { label: "number", value: "number" },
                            { label: "boolean", value: "boolean" },
                            { label: "json", value: "json" }
                          ]}
                          onChange={value => setDatabaseForm(current => ({
                            ...current,
                            columns: current.columns.map(item => item.id === column.id ? { ...item, type: String(value) } : item)
                          }))}
                        />
                        <Select
                          value={column.required ? "required" : "optional"}
                          optionList={[
                            { label: copy.databaseFieldRequired, value: "required" },
                            { label: copy.databaseFieldOptional, value: "optional" }
                          ]}
                          onChange={value => setDatabaseForm(current => ({
                            ...current,
                            columns: current.columns.map(item => item.id === column.id ? { ...item, required: value === "required" } : item)
                          }))}
                        />
                        <Input value={column.defaultValue} placeholder={copy.databaseFieldDefaultValue} onChange={value => setDatabaseForm(current => ({
                          ...current,
                          columns: current.columns.map(item => item.id === column.id ? { ...item, defaultValue: value } : item)
                        }))} />
                        <Input value={column.description} placeholder={copy.databaseFieldDescription} onChange={value => setDatabaseForm(current => ({
                          ...current,
                          columns: current.columns.map(item => item.id === column.id ? { ...item, description: value } : item)
                        }))} />
                        <Select
                          value={column.unique ? "unique" : "not-unique"}
                          optionList={[
                            { label: copy.databaseFieldUnique, value: "unique" },
                            { label: copy.databaseFieldOptional, value: "not-unique" }
                          ]}
                          onChange={value => setDatabaseForm(current => ({
                            ...current,
                            columns: current.columns.map(item => item.id === column.id ? { ...item, unique: value === "unique" } : item)
                          }))}
                        />
                        <Select
                          value={column.indexed ? "indexed" : "not-indexed"}
                          optionList={[
                            { label: copy.databaseFieldIndexed, value: "indexed" },
                            { label: copy.databaseFieldOptional, value: "not-indexed" }
                          ]}
                          onChange={value => setDatabaseForm(current => ({
                            ...current,
                            columns: current.columns.map(item => item.id === column.id ? { ...item, indexed: value === "indexed" } : item)
                          }))}
                        />
                        <Input value={column.length} placeholder={copy.databaseFieldLength} onChange={value => setDatabaseForm(current => ({
                          ...current,
                          columns: current.columns.map(item => item.id === column.id ? { ...item, length: value } : item)
                        }))} />
                        <Input value={column.min} placeholder={copy.databaseFieldMin} onChange={value => setDatabaseForm(current => ({
                          ...current,
                          columns: current.columns.map(item => item.id === column.id ? { ...item, min: value } : item)
                        }))} />
                        <Input value={column.max} placeholder={copy.databaseFieldMax} onChange={value => setDatabaseForm(current => ({
                          ...current,
                          columns: current.columns.map(item => item.id === column.id ? { ...item, max: value } : item)
                        }))} />
                        <div className="atlas-library-database-columns__actions">
                          <Button theme="borderless" onClick={() => moveDatabaseColumn(column.id, -1)}>{copy.databaseMoveUp}</Button>
                          <Button theme="borderless" onClick={() => moveDatabaseColumn(column.id, 1)}>{copy.databaseMoveDown}</Button>
                        </div>
                        <Button
                          theme="borderless"
                          type="danger"
                          onClick={() => setDatabaseForm(current => ({
                            ...current,
                            columns: current.columns.length <= 1
                              ? current.columns
                              : current.columns.filter(item => item.id !== column.id)
                          }))}
                        >
                          {copy.databaseDeleteColumn}
                        </Button>
                      </div>
                    ))}
                    <Button
                      theme="light"
                      onClick={() => setDatabaseForm(current => ({
                        ...current,
                        columns: [...current.columns, {
                          id: `col-${current.columns.length + 1}-${Date.now()}`,
                          name: "",
                          label: "",
                          type: "string",
                          required: false,
                          defaultValue: "",
                          description: "",
                          unique: false,
                          indexed: false,
                          length: "",
                          min: "",
                          max: ""
                        }]
                      }))}
                    >
                      {copy.databaseAddColumn}
                    </Button>
                    {databaseValidationErrors.length > 0 ? (
                      <Banner type="danger" description={databaseValidationErrors.join("；")} />
                    ) : null}
                    <div className="atlas-library-create-section">
                      <Typography.Text strong>{copy.databaseSchemaPreview}</Typography.Text>
                      <TextArea autosize value={structuredDatabaseSchemaJson} readOnly />
                    </div>
                  </div>
                ) : (
                  <TextArea
                    autosize
                    value={databaseForm.rawSchemaJson}
                    placeholder='[{"name":"id","type":"string"}]'
                    onChange={value => setDatabaseForm(current => ({ ...current, rawSchemaJson: value }))}
                  />
                )}
              </div>
            </div>
          ) : null}
        </Space>
      </Modal>
    </div>
  );
}
