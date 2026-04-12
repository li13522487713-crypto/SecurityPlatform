import { startTransition, useDeferredValue, useEffect, useMemo, useState } from "react";
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
  normalizeResourcePath,
  resolveKnowledgeStatus
} from "../utils";

interface CreateFormState extends KnowledgeBaseCreateRequest {}

const DEFAULT_CREATE_FORM: CreateFormState = {
  name: "",
  description: "",
  type: 0
};

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
  const [form, setForm] = useState<CreateFormState>(DEFAULT_CREATE_FORM);
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
            setTotal(response.total);
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
          setTotal(response.total);
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
      width: 120,
      render: (_value: unknown, record: AiLibraryItem) => (
        <Button
          theme="borderless"
          onClick={event => {
            event.stopPropagation();
            if (isKnowledgeType(record.resourceType)) {
              onNavigate(`/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(spaceId)}/knowledge/${record.resourceId}?biz=library`);
              return;
            }

            onNavigate(normalizeResourcePath(record.path, appKey, spaceId));
          }}
        >
          {copy.open}
        </Button>
      )
    }
  ], [appKey, copy, onNavigate]);

  async function handleCreateKnowledge() {
    if (!form.name.trim()) {
      Toast.warning(copy.createKnowledge);
      return;
    }

    setCreating(true);
    try {
      const id = await api.createKnowledgeBase({
        name: form.name.trim(),
        description: form.description?.trim(),
        type: form.type
      });
      setCreateVisible(false);
      setForm(DEFAULT_CREATE_FORM);
      Toast.success(copy.createKnowledge);
      onNavigate(`/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(spaceId)}/knowledge/${id}?biz=library`);
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
              <Dropdown.Item icon={<IconPlus />} onClick={() => setCreateVisible(true)}>
                {copy.createKnowledge}
              </Dropdown.Item>
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
                  onNavigate(`/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(spaceId)}/knowledge/${record.resourceId}?biz=library`);
                  return;
                }

                onNavigate(normalizeResourcePath(record.path, appKey, spaceId));
              }
            })}
          />
        </div>
      </div>

      <Modal
        title={copy.createKnowledge}
        visible={createVisible}
        confirmLoading={creating}
        onOk={handleCreateKnowledge}
        onCancel={() => {
          setCreateVisible(false);
          setForm(DEFAULT_CREATE_FORM);
        }}
        okText={copy.create}
        cancelText={copy.cancel}
      >
        <Space vertical align="start" style={{ width: "100%" }}>
          <Input
            value={form.name}
            placeholder={copy.createKnowledge}
            onChange={value => setForm((current: CreateFormState) => ({ ...current, name: value }))}
          />
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
          <TextArea
            autosize
            value={form.description}
            placeholder={copy.noDescription}
            onChange={value => setForm((current: CreateFormState) => ({ ...current, description: value }))}
          />
        </Space>
      </Modal>
    </div>
  );
}
