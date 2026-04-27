import { useEffect, useMemo, useState } from "react";
import { Button, Card, Dropdown, Empty, Input, Modal, Select, Space, Spin, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { IconCode, IconCopy, IconDelete, IconEdit, IconMore, IconPlus, IconSearch, IconStar, IconStarStroked } from "@douyinfe/semi-icons";

import { createMicroflowAdapterBundle, type MicroflowAdapterBundle } from "../adapter/microflow-adapter-factory";
import { getMicroflowErrorUserMessage } from "../adapter/http/microflow-api-error";
import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";
import { MicroflowErrorState } from "../components/error";
import type { MicroflowAdapterFactoryConfig } from "../config/microflow-adapter-config";
import { PublishMicroflowModal } from "../publish/PublishMicroflowModal";
import { MicroflowReferencesDrawer } from "../references/MicroflowReferencesDrawer";
import { MicroflowVersionsDrawer } from "../versions/MicroflowVersionsDrawer";
import { useMicroflowResources } from "./resource-hooks";
import type { MicroflowCreateInput, MicroflowResource, MicroflowResourceQuery } from "./resource-types";
import { canRunMicroflowAction, formatMicroflowDate, microflowPublishStatusLabel, microflowStatusColor, microflowStatusLabel } from "./resource-utils";

const { Text } = Typography;

function createFailingResourceAdapter(error: Error): MicroflowResourceAdapter {
  const reject = async <T,>(): Promise<T> => {
    throw error;
  };
  return {
    listMicroflows: () => reject(),
    getMicroflow: () => reject(),
    createMicroflow: () => reject(),
    updateMicroflow: () => reject(),
    saveMicroflowSchema: () => reject(),
    duplicateMicroflow: () => reject(),
    renameMicroflow: () => reject(),
    toggleFavorite: () => reject(),
    archiveMicroflow: () => reject(),
    restoreMicroflow: () => reject(),
    deleteMicroflow: () => reject(),
    publishMicroflow: () => reject(),
    getMicroflowReferences: () => reject(),
    getMicroflowVersions: () => reject(),
    getMicroflowVersionDetail: () => reject(),
    rollbackMicroflowVersion: () => reject(),
    duplicateMicroflowVersion: () => reject(),
    analyzeMicroflowPublishImpact: () => reject(),
    compareMicroflowVersion: () => reject(),
  };
}

export interface MicroflowResourceTabProps {
  adapter?: MicroflowResourceAdapter;
  adapterBundle?: MicroflowAdapterBundle;
  adapterConfig?: MicroflowAdapterFactoryConfig;
  workspaceId?: string;
  tenantId?: string;
  currentUser?: { id: string; name: string; roles?: string[] };
  onOpenMicroflow?: (resourceId: string) => void;
  onOpenStudio?: () => void;
}

interface CreateMicroflowModalProps {
  visible: boolean;
  onClose: () => void;
  onSubmit: (input: MicroflowCreateInput) => Promise<void>;
}

interface RenameMicroflowModalProps {
  resource?: MicroflowResource;
  onClose: () => void;
  onSubmit: (resource: MicroflowResource, name: string, displayName?: string) => Promise<void>;
}

function CreateMicroflowModal({ visible, onClose, onSubmit }: CreateMicroflowModalProps) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [moduleId, setModuleId] = useState("sales");
  const [tags, setTags] = useState("order, sample");
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit() {
    const trimmedName = name.trim();
    if (!trimmedName) {
      Toast.warning("请输入微流名称");
      return;
    }
    setSubmitting(true);
    try {
      await onSubmit({
        name: trimmedName,
        displayName: trimmedName,
        description: description.trim(),
        moduleId: moduleId.trim() || "default",
        moduleName: moduleId.trim() || "Default",
        tags: tags.split(",").map(tag => tag.trim()).filter(Boolean),
        parameters: [],
        returnType: { kind: "void" },
        template: "blank"
      });
      onClose();
    } catch (caught) {
      Toast.error(getMicroflowErrorUserMessage(caught));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal visible={visible} title="新建微流" onCancel={onClose} onOk={() => void handleSubmit()} confirmLoading={submitting}>
      <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
        <Input value={name} onChange={setName} placeholder="OrderProcessing" prefix="名称" />
        <Input value={description} onChange={setDescription} placeholder="描述" prefix="描述" />
        <Input value={moduleId} onChange={setModuleId} placeholder="sales" prefix="模块" />
        <Input value={tags} onChange={setTags} placeholder="order, crm" prefix="标签" />
      </Space>
    </Modal>
  );
}

function RenameMicroflowModal({ resource, onClose, onSubmit }: RenameMicroflowModalProps) {
  const [name, setName] = useState(resource?.name ?? "");
  const [displayName, setDisplayName] = useState(resource?.displayName ?? "");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    setName(resource?.name ?? "");
    setDisplayName(resource?.displayName ?? "");
  }, [resource]);

  async function handleSubmit() {
    if (!resource) {
      return;
    }
    const trimmedName = name.trim();
    if (!trimmedName) {
      Toast.warning("请输入微流 name");
      return;
    }
    setSubmitting(true);
    try {
      await onSubmit(resource, trimmedName, displayName.trim() || trimmedName);
      onClose();
    } catch (caught) {
      Toast.error(getMicroflowErrorUserMessage(caught));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal visible={Boolean(resource)} title="重命名微流" onCancel={onClose} onOk={() => void handleSubmit()} confirmLoading={submitting}>
      <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
        <Input value={name} onChange={setName} placeholder="OrderProcessing" prefix="Name" />
        <Input value={displayName} onChange={setDisplayName} placeholder="订单处理" prefix="显示名" />
      </Space>
    </Modal>
  );
}

export function ResourceCard({
  resource,
  onOpen,
  actions
}: {
  resource: MicroflowResource;
  onOpen: (resource: MicroflowResource) => void;
  actions: (resource: MicroflowResource) => JSX.Element;
}) {
  return (
    <Card
      style={{ cursor: "pointer", minHeight: 180 }}
      bodyStyle={{ display: "flex", flexDirection: "column", gap: 10 }}
      onClick={() => onOpen(resource)}
    >
      <Space align="start" style={{ justifyContent: "space-between", width: "100%" }}>
        <Space align="center">
          <div style={{ width: 40, height: 40, borderRadius: 12, background: "var(--semi-color-primary-light-default)", display: "flex", alignItems: "center", justifyContent: "center", color: "var(--semi-color-primary)" }}>
            <IconCode />
          </div>
          <div>
            <Text strong>{resource.displayName || resource.name}</Text>
            <div><Text type="tertiary" size="small">{resource.moduleName || resource.moduleId}</Text></div>
          </div>
        </Space>
        <Dropdown trigger="click" position="bottomRight" render={actions(resource)}>
          <Button theme="borderless" type="tertiary" icon={<IconMore />} onClick={event => event.stopPropagation()} />
        </Dropdown>
      </Space>
      <Text type="tertiary" size="small" ellipsis={{ showTooltip: true }}>{resource.description || "无描述"}</Text>
      <Space wrap>
        <Tag color={microflowStatusColor(resource.status)}>{microflowStatusLabel(resource.status)}</Tag>
        <Tag color={resource.publishStatus === "changedAfterPublish" ? "orange" : resource.publishStatus === "published" ? "green" : "grey"}>{microflowPublishStatusLabel(resource.publishStatus)}</Tag>
        <Tag>当前 {resource.version}</Tag>
        <Tag color="blue">已发布 {resource.latestPublishedVersion ?? "-"}</Tag>
      </Space>
      <Space wrap>
        <Tag>引用 {resource.referenceCount}</Tag>
        <Tag>运行 {resource.lastRunStatus ?? "neverRun"}</Tag>
        {resource.tags.slice(0, 3).map(tag => <Tag key={tag}>{tag}</Tag>)}
      </Space>
      <Text type="tertiary" size="small">{resource.ownerName ?? "-"} · {formatMicroflowDate(resource.updatedAt)}</Text>
    </Card>
  );
}

export function ResourceTable({
  resources,
  onOpen,
  actions,
  onToggleFavorite
}: {
  resources: MicroflowResource[];
  onOpen: (resource: MicroflowResource) => void;
  actions: (resource: MicroflowResource) => JSX.Element;
  onToggleFavorite: (resource: MicroflowResource) => void;
}) {
  return (
    <Card bodyStyle={{ padding: 0 }}>
      {resources.map(resource => (
        <div
          key={resource.id}
          role="button"
          tabIndex={0}
          onClick={() => onOpen(resource)}
          onKeyDown={event => {
            if (event.key === "Enter" || event.key === " ") {
              onOpen(resource);
            }
          }}
          style={{ display: "grid", gridTemplateColumns: "minmax(0, 1fr) 96px 120px 86px 96px 36px 36px", gap: 12, alignItems: "center", padding: "12px 14px", borderBottom: "1px solid var(--semi-color-border)", cursor: "pointer" }}
        >
          <Space align="center" style={{ minWidth: 0 }}>
            <IconCode />
            <div style={{ minWidth: 0 }}>
              <Text strong ellipsis={{ showTooltip: true }}>{resource.displayName || resource.name}</Text>
              <div><Text type="tertiary" size="small" ellipsis={{ showTooltip: true }}>{resource.description || "无描述"}</Text></div>
            </div>
          </Space>
          <Text strong>{resource.version}</Text>
          <Tag color="blue">{resource.latestPublishedVersion ?? "-"}</Tag>
          <Tag color={microflowStatusColor(resource.status)}>{microflowStatusLabel(resource.status)}</Tag>
          <Tag color={resource.publishStatus === "changedAfterPublish" ? "orange" : resource.publishStatus === "published" ? "green" : "grey"}>{microflowPublishStatusLabel(resource.publishStatus)}</Tag>
          <Button theme="borderless" type="tertiary" icon={resource.favorite ? <IconStar /> : <IconStarStroked />} onClick={event => { event.stopPropagation(); onToggleFavorite(resource); }} />
          <Dropdown trigger="click" position="bottomRight" render={actions(resource)}>
            <Button theme="borderless" type="tertiary" icon={<IconMore />} onClick={event => event.stopPropagation()} />
          </Dropdown>
        </div>
      ))}
    </Card>
  );
}

export function MendixMicroflowResourceTab({ adapter: adapterInput, adapterBundle, adapterConfig, workspaceId, tenantId, currentUser, onOpenMicroflow, onOpenStudio }: MicroflowResourceTabProps) {
  const bundleResult = useMemo(() => {
    try {
      return { bundle: adapterBundle ?? createMicroflowAdapterBundle({ ...adapterConfig, workspaceId: adapterConfig?.workspaceId ?? workspaceId, tenantId: adapterConfig?.tenantId ?? tenantId, currentUser: adapterConfig?.currentUser ?? currentUser }) };
    } catch (caught) {
      return { error: caught instanceof Error ? caught : new Error(String(caught)) };
    }
  }, [adapterBundle, adapterConfig, currentUser, tenantId, workspaceId]);
  const bundle = bundleResult.bundle;
  const failingAdapter = useMemo(() => createFailingResourceAdapter(bundleResult.error ?? new Error("微流服务未配置。")), [bundleResult.error]);
  const [query, setQuery] = useState<MicroflowResourceQuery>({ sortBy: "updatedAt", sortOrder: "desc" });
  const [view, setView] = useState<"card" | "table">("table");
  const { adapter, items, allItems, loading, error, reload } = useMicroflowResources({ adapter: adapterInput ?? bundle?.resourceAdapter ?? failingAdapter, workspaceId, currentUser, query });
  const [createOpen, setCreateOpen] = useState(false);
  const [publishOpen, setPublishOpen] = useState(false);
  const [versionsOpen, setVersionsOpen] = useState(false);
  const [referencesOpen, setReferencesOpen] = useState(false);
  const [selectedResource, setSelectedResource] = useState<MicroflowResource>();
  const [renamingResource, setRenamingResource] = useState<MicroflowResource>();

  const tagOptions = useMemo(() => {
    const tags = [...new Set(allItems.flatMap(item => item.tags))].sort();
    return [{ value: "", label: "全部标签" }, ...tags.map(tag => ({ value: tag, label: tag }))];
  }, [allItems]);

  async function refreshAfter(action: Promise<unknown>, message: string) {
    try {
      await action;
      Toast.success(message);
      await reload();
    } catch (caught) {
      Toast.error(getMicroflowErrorUserMessage(caught));
    }
  }

  async function handleCreate(input: MicroflowCreateInput) {
    try {
      const resource = await adapter.createMicroflow(input);
      Toast.success("微流已创建");
      await reload();
      onOpenMicroflow?.(resource.id);
    } catch (caught) {
      throw caught;
    }
  }

  async function handleRename(resource: MicroflowResource, name: string, displayName?: string) {
    await adapter.renameMicroflow(resource.id, name, displayName);
    Toast.success("微流已重命名");
    await reload();
  }

  function openPublish(resource: MicroflowResource) {
    setSelectedResource(resource);
    setPublishOpen(true);
  }

  function openVersions(resource: MicroflowResource) {
    setSelectedResource(resource);
    setVersionsOpen(true);
  }

  function openReferences(resource: MicroflowResource) {
    setSelectedResource(resource);
    setReferencesOpen(true);
  }

  function handlePublished(resultResource: MicroflowResource) {
    setSelectedResource(resultResource);
    void reload();
  }

  function renderActionMenu(resource: MicroflowResource) {
    return (
      <Dropdown.Menu>
        <Dropdown.Item icon={<IconEdit />} onClick={() => onOpenMicroflow?.(resource.id)}>编辑</Dropdown.Item>
        <Dropdown.Item onClick={() => setRenamingResource(resource)} disabled={!canRunMicroflowAction(resource, "canEdit")}>重命名</Dropdown.Item>
        <Dropdown.Item icon={<IconCopy />} disabled={!canRunMicroflowAction(resource, "canDuplicate")} onClick={() => void refreshAfter(adapter.duplicateMicroflow(resource.id), "已复制微流")}>复制</Dropdown.Item>
        <Dropdown.Item disabled={!canRunMicroflowAction(resource, "canPublish")} onClick={() => openPublish(resource)}>发布</Dropdown.Item>
        <Dropdown.Item onClick={() => openVersions(resource)}>查看版本</Dropdown.Item>
        <Dropdown.Item onClick={() => openReferences(resource)}>查看引用</Dropdown.Item>
        {resource.archived ? (
          <Dropdown.Item onClick={() => void refreshAfter(adapter.restoreMicroflow(resource.id), "已恢复微流")}>恢复</Dropdown.Item>
        ) : (
          <Dropdown.Item disabled={!canRunMicroflowAction(resource, "canArchive")} onClick={() => void refreshAfter(adapter.archiveMicroflow(resource.id), "已归档微流")}>归档</Dropdown.Item>
        )}
        <Dropdown.Item type="danger" icon={<IconDelete />} disabled={!canRunMicroflowAction(resource, "canDelete")} onClick={() => {
          Modal.confirm({
            title: "确认删除微流",
            content: `确认删除 ${resource.displayName || resource.name}？`,
            onOk: () => refreshAfter(adapter.deleteMicroflow(resource.id), "已删除微流")
          });
        }}>删除</Dropdown.Item>
      </Dropdown.Menu>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 14, height: "100%", minHeight: 0 }}>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 12, flexWrap: "wrap" }}>
        <Space>
          <Button type={view === "table" ? "primary" : "tertiary"} theme={view === "table" ? "solid" : "borderless"} onClick={() => setView("table")}>表格</Button>
          <Button type={view === "card" ? "primary" : "tertiary"} theme={view === "card" ? "solid" : "borderless"} onClick={() => setView("card")}>卡片</Button>
          {bundle ? <Tag color={bundle.mode === "http" ? "green" : "orange"}>{bundle.mode === "http" ? `HTTP ${bundle.apiBaseUrl ?? ""}`.trim() : "本地模拟数据"}</Tag> : <Tag color="red">disconnected</Tag>}
          {onOpenStudio ? <Button onClick={onOpenStudio}>打开 Mendix Studio</Button> : null}
        </Space>
        <Space wrap>
          <Input prefix={<IconSearch />} showClear value={query.keyword ?? ""} onChange={value => setQuery(current => ({ ...current, keyword: value }))} placeholder="搜索微流" style={{ width: 260 }} />
          <Select
            value={query.sortBy ?? "updatedAt"}
            onChange={value => setQuery(current => ({ ...current, sortBy: value as MicroflowResourceQuery["sortBy"] }))}
            style={{ width: 128 }}
            optionList={[
              { value: "updatedAt", label: "更新时间" },
              { value: "createdAt", label: "创建时间" },
              { value: "name", label: "名称" },
              { value: "version", label: "版本" },
              { value: "referenceCount", label: "引用数" }
            ]}
          />
          <Select
            value={query.status?.[0] ?? ""}
            onChange={value => setQuery(current => ({ ...current, status: value ? [value as MicroflowResource["status"]] : undefined }))}
            style={{ width: 116 }}
            optionList={[
              { value: "", label: "全部状态" },
              { value: "draft", label: "草稿" },
              { value: "published", label: "已发布" },
              { value: "archived", label: "已归档" }
            ]}
          />
          <Select value={query.tags?.[0] ?? ""} onChange={value => setQuery(current => ({ ...current, tags: value ? [String(value)] : undefined }))} style={{ width: 120 }} optionList={tagOptions} />
          <Button
            icon={<IconStar />}
            type={query.favoriteOnly ? "primary" : "tertiary"}
            theme={query.favoriteOnly ? "solid" : "borderless"}
            onClick={() => setQuery(current => ({ ...current, favoriteOnly: current.favoriteOnly ? undefined : true }))}
          >
            仅收藏
          </Button>
          <Button icon={<IconPlus />} type="primary" theme="solid" onClick={() => setCreateOpen(true)}>新建微流</Button>
        </Space>
      </div>

      <div style={{ flex: 1, minHeight: 0, overflow: "auto" }}>
        {loading ? (
          <div style={{ display: "flex", justifyContent: "center", padding: 48 }}><Spin /></div>
        ) : error ? (
          <MicroflowErrorState error={error} title={bundle?.apiBaseUrl ? `微流服务未连接：${bundle.apiBaseUrl}` : "微流服务未连接"} onRetry={() => void reload()} />
        ) : items.length === 0 ? (
          <Empty title="暂无微流" description="新建微流后可在这里管理版本、发布和引用关系。" style={{ padding: 48 }} />
        ) : view === "card" ? (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))", gap: 12 }}>
            {items.map(resource => (
              <ResourceCard key={resource.id} resource={resource} onOpen={item => onOpenMicroflow?.(item.id)} actions={renderActionMenu} />
            ))}
          </div>
        ) : (
          <ResourceTable resources={items} onOpen={item => onOpenMicroflow?.(item.id)} actions={renderActionMenu} onToggleFavorite={resource => void refreshAfter(adapter.toggleFavorite(resource.id, !resource.favorite), resource.favorite ? "已取消收藏" : "已收藏")} />
        )}
      </div>

      <CreateMicroflowModal visible={createOpen} onClose={() => setCreateOpen(false)} onSubmit={handleCreate} />
      <RenameMicroflowModal resource={renamingResource} onClose={() => setRenamingResource(undefined)} onSubmit={handleRename} />
      <PublishMicroflowModal
        visible={publishOpen}
        resource={selectedResource}
        adapter={adapter}
        validationAdapter={bundle?.validationAdapter}
        onClose={() => setPublishOpen(false)}
        onPublished={handlePublished}
        onViewProblems={issues => Modal.info({ title: "校验问题", content: issues.map(issue => `${issue.severity}: ${issue.message}`).join("\n") || "无问题" })}
        onViewReferences={() => {
          setPublishOpen(false);
          setReferencesOpen(true);
        }}
      />
      <MicroflowVersionsDrawer
        visible={versionsOpen}
        resource={selectedResource}
        adapter={adapter}
        onClose={() => setVersionsOpen(false)}
        onResourceChange={resource => {
          setSelectedResource(resource);
          void reload();
        }}
        onCreated={() => void reload()}
      />
      <MicroflowReferencesDrawer visible={referencesOpen} resource={selectedResource} adapter={adapter} onClose={() => setReferencesOpen(false)} />
    </div>
  );
}

export const MicroflowResourceTab = MendixMicroflowResourceTab;
