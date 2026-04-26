import { useCallback, useEffect, useMemo, useState } from "react";
import { Button, Card, Dropdown, Empty, Input, Modal, Select, Space, Spin, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { IconCode, IconCopy, IconDelete, IconEdit, IconExport, IconMore, IconPlus, IconSearch, IconStar, IconStarStroked } from "@douyinfe/semi-icons";
import { createLocalMicroflowApiClient, type CreateMicroflowInput, type MicroflowListQuery, type MicroflowResource, type MicroflowResourceStatus } from "@atlas/microflow";
import { microflowEditorPath } from "@atlas/app-shell-shared";
import { useNavigate } from "react-router-dom";
import { useAppI18n } from "../i18n";

const { Text, Title } = Typography;

function formatDate(iso?: string): string {
  if (!iso) {
    return "";
  }
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return iso;
  }
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")} ${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
}

function statusColor(status: MicroflowResourceStatus): "blue" | "green" | "grey" {
  if (status === "published") {
    return "green";
  }
  if (status === "archived") {
    return "grey";
  }
  return "blue";
}

function statusLabel(status: MicroflowResourceStatus, t: (key: "microflowStatusDraft" | "microflowStatusPublished" | "microflowStatusArchived") => string): string {
  if (status === "published") {
    return t("microflowStatusPublished");
  }
  if (status === "archived") {
    return t("microflowStatusArchived");
  }
  return t("microflowStatusDraft");
}

function nextVersion(version: string): string {
  const normalized = version.trim().replace(/^v/u, "");
  const parts = normalized.split(".").map(part => Number(part));
  if (parts.every(part => Number.isFinite(part))) {
    const nextParts = parts.length > 1 ? parts : [parts[0] ?? 0, 0];
    nextParts[nextParts.length - 1] = (nextParts[nextParts.length - 1] ?? 0) + 1;
    return `v${nextParts.join(".")}`;
  }
  return "v1";
}

function displayResource(item: MicroflowResource): MicroflowResource {
  const map: Record<string, Pick<MicroflowResource, "name" | "description" | "tags">> = {
    "mf-order-process": {
      name: "订单处理微流",
      description: "处理用户订单的完整流程",
      tags: ["订单", "处理", "示例"]
    },
    "mf-customer-onboarding": {
      name: "用户注册微流",
      description: "新用户注册与初始化配置",
      tags: ["用户", "注册"]
    },
    "mf-payment-archive": {
      name: "库存调整微流",
      description: "库存容量调整和校验逻辑",
      tags: ["库存", "调整"]
    }
  };
  const override = map[item.id];
  return override ? { ...item, ...override } : item;
}

interface CreateMicroflowModalProps {
  visible: boolean;
  onClose: () => void;
  onSubmit: (input: CreateMicroflowInput) => Promise<void>;
}

function CreateMicroflowModal({ visible, onClose, onSubmit }: CreateMicroflowModalProps) {
  const { t } = useAppI18n();
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [moduleId, setModuleId] = useState("order");
  const [tags, setTags] = useState("order, demo");
  const [returnType, setReturnType] = useState("void");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!visible) {
      return;
    }
    setName("");
    setDescription("");
    setModuleId("order");
    setTags("order, demo");
    setReturnType("void");
  }, [visible]);

  async function handleSubmit() {
    const trimmedName = name.trim();
    if (!trimmedName) {
      Toast.warning(t("microflowCreateNameRequired"));
      return;
    }
    setSubmitting(true);
    try {
      await onSubmit({
        name: trimmedName,
        description: description.trim(),
        moduleId: moduleId.trim() || "default",
        moduleName: moduleId.trim() || "Default",
        tags: tags.split(",").map(tag => tag.trim()).filter(Boolean),
        returnType: returnType === "void" ? { kind: "void", name: "Void" } : { kind: "primitive", name: returnType }
      });
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal
      visible={visible}
      title={t("microflowCreateTitle")}
      onCancel={onClose}
      onOk={() => void handleSubmit()}
      confirmLoading={submitting}
      okText={t("microflowCreateSubmit")}
    >
      <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
        <Input value={name} onChange={setName} placeholder={t("microflowCreateNamePlaceholder")} prefix={t("microflowCreateName")} />
        <Input value={description} onChange={setDescription} placeholder={t("microflowCreateDescriptionPlaceholder")} prefix={t("microflowCreateDescription")} />
        <Input value={moduleId} onChange={setModuleId} placeholder="order" prefix={t("microflowCreateModule")} />
        <Input value={tags} onChange={setTags} placeholder="order, approval" prefix={t("microflowCreateTags")} />
        <Select
          value={returnType}
          onChange={value => setReturnType(String(value))}
          style={{ width: "100%" }}
          prefix={t("microflowCreateReturnType")}
          optionList={[
            { value: "void", label: "Void" },
            { value: "Boolean", label: "Boolean" },
            { value: "String", label: "String" },
            { value: "Integer", label: "Integer" }
          ]}
        />
      </Space>
    </Modal>
  );
}

export function MicroflowResourceTab() {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const apiClient = useMemo(() => createLocalMicroflowApiClient(), []);
  const [items, setItems] = useState<MicroflowResource[]>([]);
  const [allItems, setAllItems] = useState<MicroflowResource[]>([]);
  const [loading, setLoading] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [query, setQuery] = useState<MicroflowListQuery>({
    scope: "all",
    status: "all",
    sortBy: "updatedAt",
    updatedRange: "all"
  });

  const tagOptions = useMemo(() => {
    const tags = [...new Set(allItems.flatMap(item => item.tags))].sort();
    return [{ value: "", label: t("microflowFilterAllTags") }, ...tags.map(tag => ({ value: tag, label: tag }))];
  }, [allItems, t]);

  const ownerOptions = useMemo(() => {
    const owners = [...new Set(allItems.map(item => item.ownerName))].sort();
    return [{ value: "", label: t("microflowFilterAllOwners") }, ...owners.map(owner => ({ value: owner, label: owner }))];
  }, [allItems, t]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [filtered, all] = await Promise.all([
        apiClient.listMicroflows(query),
        apiClient.listMicroflows()
      ]);
      setItems(filtered.map(displayResource));
      setAllItems(all.map(displayResource));
    } catch (error) {
      Toast.error((error as Error).message || t("microflowLoadFailed"));
    } finally {
      setLoading(false);
    }
  }, [apiClient, query, t]);

  useEffect(() => {
    void load();
  }, [load]);

  async function handleCreate(input: CreateMicroflowInput) {
    const resource = await apiClient.createMicroflow(input);
    Toast.success(t("microflowCreateSuccess"));
    setCreateOpen(false);
    navigate(microflowEditorPath(resource.id));
  }

  async function refreshAfter(action: Promise<unknown>, successMessage: string) {
    await action;
    Toast.success(successMessage);
    await load();
  }

  function renderActionMenu(item: MicroflowResource) {
    return (
      <Dropdown.Menu>
        <Dropdown.Item icon={<IconEdit />} onClick={() => navigate(microflowEditorPath(item.id))}>{t("microflowActionEdit")}</Dropdown.Item>
        <Dropdown.Item icon={<IconCopy />} onClick={() => void refreshAfter(apiClient.duplicateMicroflow(item.id), t("microflowActionDuplicateSuccess"))}>{t("microflowActionDuplicate")}</Dropdown.Item>
        <Dropdown.Item onClick={() => Toast.info(t("microflowActionRenameReserved"))}>{t("microflowActionRename")}</Dropdown.Item>
        <Dropdown.Item onClick={() => void refreshAfter(apiClient.publishMicroflow(item.id, { version: nextVersion(item.version), releaseNote: "Published from resource library.", overwriteCurrent: true }), t("microflowPublishSuccess"))}>{t("microflowActionPublish")}</Dropdown.Item>
        <Dropdown.Item onClick={() => void apiClient.getMicroflowReferences(item.id).then(refs => Modal.info({ title: t("microflowActionReferences"), content: refs.length ? refs.map(ref => `${ref.sourceName} (${ref.sourceType})`).join("\n") : t("microflowNoReferences") }))}>{t("microflowActionReferences")}</Dropdown.Item>
        <Dropdown.Item icon={<IconExport />} onClick={() => Toast.info(t("microflowActionExportReserved"))}>{t("microflowActionExport")}</Dropdown.Item>
        <Dropdown.Item onClick={() => void refreshAfter(apiClient.archiveMicroflow(item.id), t("microflowActionArchiveSuccess"))}>{t("microflowActionArchive")}</Dropdown.Item>
        <Dropdown.Item type="danger" icon={<IconDelete />} onClick={() => void refreshAfter(apiClient.deleteMicroflow(item.id), t("microflowActionDeleteSuccess"))}>{t("microflowActionDelete")}</Dropdown.Item>
      </Dropdown.Menu>
    );
  }

  return (
    <div style={{ display: "grid", gridTemplateColumns: "minmax(0, 1fr) 260px", gap: 16, minHeight: 0, height: "100%" }}>
      <div style={{ display: "flex", flexDirection: "column", gap: 14, minHeight: 0 }}>
        <div style={{ display: "flex", justifyContent: "space-between", gap: 12, flexWrap: "wrap" }}>
          <Space>
            {(["all", "mine", "shared", "favorite"] as const).map(scope => (
              <Button
                key={scope}
                theme={(query.scope ?? "all") === scope ? "solid" : "borderless"}
                type={(query.scope ?? "all") === scope ? "primary" : "tertiary"}
                onClick={() => setQuery(current => ({ ...current, scope }))}
              >
                {scope === "all" ? t("microflowScopeAll") : scope === "mine" ? t("microflowScopeMine") : scope === "shared" ? t("microflowScopeShared") : t("microflowScopeFavorite")}
              </Button>
            ))}
          </Space>
          <Space wrap>
            <Input
              prefix={<IconSearch />}
              showClear
              value={query.keyword ?? ""}
              onChange={value => setQuery(current => ({ ...current, keyword: value }))}
              placeholder={t("microflowSearchPlaceholder")}
              style={{ width: 260 }}
            />
            <Select
              value={query.sortBy ?? "updatedAt"}
              onChange={value => setQuery(current => ({ ...current, sortBy: value as MicroflowListQuery["sortBy"] }))}
              style={{ width: 140 }}
              optionList={[
                { value: "updatedAt", label: t("microflowSortUpdatedAt") },
                { value: "createdAt", label: t("microflowSortCreatedAt") },
                { value: "name", label: t("microflowSortName") },
                { value: "version", label: t("microflowSortVersion") }
              ]}
            />
            <Select
              value={query.status ?? "all"}
              onChange={value => setQuery(current => ({ ...current, status: value as MicroflowListQuery["status"] }))}
              style={{ width: 128 }}
              optionList={[
                { value: "all", label: t("microflowStatusAll") },
                { value: "draft", label: t("microflowStatusDraft") },
                { value: "published", label: t("microflowStatusPublished") },
                { value: "archived", label: t("microflowStatusArchived") }
              ]}
            />
            <Select value={query.tag ?? ""} onChange={value => setQuery(current => ({ ...current, tag: String(value) || undefined }))} style={{ width: 120 }} optionList={tagOptions} />
            <Select value={query.ownerName ?? ""} onChange={value => setQuery(current => ({ ...current, ownerName: String(value) || undefined }))} style={{ width: 130 }} optionList={ownerOptions} />
          </Space>
        </div>

        <div style={{ flex: 1, minHeight: 0, overflow: "auto" }}>
          {loading ? (
            <div style={{ display: "flex", justifyContent: "center", padding: 48 }}>
              <Spin />
            </div>
          ) : (
            <div style={{ display: "grid", gridTemplateColumns: "180px minmax(0, 1fr)", gap: 12, alignItems: "start" }}>
              <Card
                style={{ minHeight: 184, border: "1px dashed var(--semi-color-primary)", cursor: "pointer", background: "var(--semi-color-primary-light-default)" }}
                bodyStyle={{ minHeight: 184, display: "flex", alignItems: "center", justifyContent: "center", padding: 16 }}
                onClick={() => setCreateOpen(true)}
              >
                <Space vertical align="center">
                  <Button icon={<IconPlus />} theme="solid" type="primary" />
                  <Title heading={6} style={{ margin: 0 }}>{t("microflowCreateCardTitle")}</Title>
                  <Text type="tertiary">{t("microflowCreateCardDesc")}</Text>
                </Space>
              </Card>
              <Card bodyStyle={{ padding: 0 }}>
                {items.length === 0 ? (
                  <Empty title={t("microflowEmptyTitle")} description={t("microflowEmptyDesc")} style={{ padding: 48 }} />
                ) : items.map(item => (
                  <div
                    key={item.id}
                    role="button"
                    tabIndex={0}
                    onClick={() => navigate(microflowEditorPath(item.id))}
                    style={{ display: "grid", gridTemplateColumns: "minmax(0, 1fr) 56px 84px 36px 36px", gap: 12, alignItems: "center", padding: "12px 14px", borderBottom: "1px solid var(--semi-color-border)", cursor: "pointer" }}
                  >
                    <Space align="center" style={{ minWidth: 0 }}>
                      <div style={{ width: 42, height: 42, borderRadius: 12, background: "var(--semi-color-primary-light-default)", display: "flex", alignItems: "center", justifyContent: "center", color: "var(--semi-color-primary)" }}>
                        <IconCode />
                      </div>
                      <div style={{ minWidth: 0 }}>
                        <Space spacing={6}>
                          <Text strong ellipsis={{ showTooltip: true }} style={{ maxWidth: 260 }}>{item.name}</Text>
                          {item.tags.slice(0, 3).map(tag => <Tag key={tag} size="small">{tag}</Tag>)}
                        </Space>
                        <div><Text type="tertiary" size="small" ellipsis={{ showTooltip: true }} style={{ maxWidth: 420 }}>{item.description}</Text></div>
                        <div><Text type="tertiary" size="small">{item.ownerName} · {formatDate(item.updatedAt)}</Text></div>
                      </div>
                    </Space>
                    <Text strong style={{ justifySelf: "start" }}>{item.version}</Text>
                    <Tag color={statusColor(item.status)} style={{ justifySelf: "start", minWidth: 0 }}>{statusLabel(item.status, t)}</Tag>
                    <Button
                      theme="borderless"
                      type="tertiary"
                      icon={item.favorite ? <IconStar /> : <IconStarStroked />}
                      onClick={event => {
                        event.stopPropagation();
                        void refreshAfter(apiClient.toggleFavorite(item.id, !item.favorite), item.favorite ? t("microflowUnfavoriteSuccess") : t("microflowFavoriteSuccess"));
                      }}
                    />
                    <Dropdown trigger="click" position="bottomRight" render={renderActionMenu(item)}>
                      <Button
                        theme="borderless"
                        type="tertiary"
                        icon={<IconMore />}
                        onClick={event => event.stopPropagation()}
                      />
                    </Dropdown>
                  </div>
                ))}
                {items.length > 0 ? (
                  <div style={{ display: "flex", justifyContent: "flex-end", alignItems: "center", gap: 16, padding: "10px 14px" }}>
                    <Text type="tertiary" size="small">共 {items.length} 条</Text>
                    <Button size="small" theme="borderless" disabled>{"<"}</Button>
                    <Tag color="blue">1</Tag>
                    <Button size="small" theme="borderless" disabled>{">"}</Button>
                    <Select size="small" value="10" style={{ width: 92 }} optionList={[{ value: "10", label: "10 条/页" }]} />
                  </div>
                ) : null}
              </Card>
            </div>
          )}
        </div>
      </div>

      <Card title={t("microflowGuideTitle")} bodyStyle={{ display: "flex", flexDirection: "column", gap: 12 }}>
        {[
          t("microflowGuideStep1"),
          t("microflowGuideStep2"),
          t("microflowGuideStep3"),
          t("microflowGuideStep4"),
          t("microflowGuideStep5"),
          t("microflowGuideStep6"),
          t("microflowGuideStep7")
        ].map((step, index) => (
          <Space key={step} align="start">
            <Tag color="blue">{index + 1}</Tag>
            <Text>{step}</Text>
          </Space>
        ))}
      </Card>

      <CreateMicroflowModal
        visible={createOpen}
        onClose={() => setCreateOpen(false)}
        onSubmit={handleCreate}
      />
    </div>
  );
}
