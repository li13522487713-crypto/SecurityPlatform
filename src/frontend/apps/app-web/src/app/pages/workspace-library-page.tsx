import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Button,
  Dropdown,
  Empty,
  Form,
  Input,
  InputNumber,
  Modal,
  Select,
  Spin,
  Table,
  Tabs,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import {
  IconPlus,
  IconSearch,
  IconCode,
  IconFolder,
  IconArticle,
  IconPuzzle,
  IconList,
  IconBox,
  IconHistogram,
  IconLink,
  IconChevronDown
} from "@douyinfe/semi-icons";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { useNavigate } from "react-router-dom";
import {
  chatflowEditorPath,
  orgWorkspaceDatabaseDetailPath,
  orgWorkspaceKnowledgeBaseDetailPath,
  orgWorkspacePluginDetailPath,
  workflowEditorPath
} from "@atlas/app-shell-shared";
import { useWorkspaceContext } from "../workspace-context";
import {
  getLibraryPaged,
  importLibraryItem,
  type AiWorkspaceLibraryItem,
  type LibraryResourceType,
  type LibrarySource
} from "../../services/api-ai-workspace";

type LibraryTabKey =
  | "all"
  | "plugin"
  | "workflow"
  | "knowledge-base"
  | "card"
  | "prompt"
  | "database"
  | "voice"
  | "memory";

interface TabDef {
  key: LibraryTabKey;
  label: string;
  resourceType?: LibraryResourceType;
}

const TABS: TabDef[] = [
  { key: "all", label: "全部" },
  { key: "plugin", label: "插件", resourceType: "plugin" },
  { key: "workflow", label: "工作流", resourceType: "workflow" },
  { key: "knowledge-base", label: "知识库", resourceType: "knowledge-base" },
  { key: "card", label: "卡片", resourceType: "card" },
  { key: "prompt", label: "提示词", resourceType: "prompt" },
  { key: "database", label: "数据库", resourceType: "database" },
  { key: "voice", label: "音色", resourceType: "voice" },
  { key: "memory", label: "记忆库", resourceType: "memory" }
];

const CREATE_ITEMS: { key: LibraryResourceType; label: string; icon: React.ReactNode }[] = [
  { key: "plugin", label: "插件", icon: <IconPuzzle /> },
  { key: "workflow", label: "工作流", icon: <IconCode /> },
  { key: "knowledge-base", label: "知识库", icon: <IconFolder /> },
  { key: "card", label: "卡片", icon: <IconBox /> },
  { key: "prompt", label: "提示词", icon: <IconArticle /> },
  { key: "database", label: "数据库", icon: <IconList /> },
  { key: "voice", label: "音色", icon: <IconHistogram /> },
  { key: "memory", label: "记忆库", icon: <IconLink /> }
];

const IMPORTABLE_TYPES: { value: LibraryResourceType; label: string }[] = [
  { value: "workflow", label: "工作流" },
  { value: "plugin", label: "插件" },
  { value: "knowledge-base", label: "知识库" },
  { value: "database", label: "数据库" }
];

const SOURCE_OPTIONS: { value: LibrarySource; label: string }[] = [
  { value: "all", label: "所有来源" },
  { value: "official", label: "扣子官方" },
  { value: "custom", label: "自定义" }
];

function typeLabelOf(rt: LibraryResourceType): string {
  const m: Record<LibraryResourceType, string> = {
    plugin: "插件",
    workflow: "工作流",
    "knowledge-base": "扣子知识库",
    card: "扣子卡片",
    prompt: "提示词",
    database: "数据库",
    voice: "音色",
    memory: "记忆库",
    agent: "智能体",
    app: "应用"
  };
  return m[rt] ?? rt;
}

function iconOf(rt: LibraryResourceType): React.ReactNode {
  const map: Record<string, React.ReactNode> = {
    plugin: <IconPuzzle />,
    workflow: <IconCode />,
    "knowledge-base": <IconFolder />,
    card: <IconBox />,
    prompt: <IconArticle />,
    database: <IconList />,
    voice: <IconHistogram />,
    memory: <IconLink />
  };
  return map[rt] ?? <IconFolder />;
}

function formatDate(iso?: string): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  const pad = (n: number) => n.toString().padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function WorkspaceLibraryPage() {
  const workspace = useWorkspaceContext();
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<LibraryTabKey>("all");
  const [source, setSource] = useState<LibrarySource>("all");
  const [keyword, setKeyword] = useState("");
  const [items, setItems] = useState<AiWorkspaceLibraryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [total, setTotal] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [importOpen, setImportOpen] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const tab = TABS.find(t => t.key === activeTab);
      const result = await getLibraryPaged(
        { pageIndex, pageSize: 20 },
        { resourceType: tab?.resourceType, source, keyword }
      );
      setItems(result.items);
      setTotal(result.total);
    } catch (error) {
      Toast.error((error as Error).message || "查询失败");
      setItems([]);
      setTotal(0);
    } finally {
      setLoading(false);
    }
  }, [activeTab, source, keyword, pageIndex]);

  useEffect(() => {
    void load();
  }, [load]);

  const handleOpen = useCallback(
    (record: AiWorkspaceLibraryItem) => {
      switch (record.resourceType) {
        case "workflow":
          navigate(workflowEditorPath(String(record.resourceId)));
          return;
        case "plugin":
          navigate(orgWorkspacePluginDetailPath(workspace.orgId, workspace.id, record.resourceId));
          return;
        case "knowledge-base":
          navigate(orgWorkspaceKnowledgeBaseDetailPath(workspace.orgId, workspace.id, record.resourceId));
          return;
        case "database":
          navigate(orgWorkspaceDatabaseDetailPath(workspace.orgId, workspace.id, record.resourceId));
          return;
        case "prompt":
          navigate("/ai/prompts");
          return;
        default:
          Toast.info(`${typeLabelOf(record.resourceType)} 详情页即将上线`);
      }
    },
    [navigate, workspace.id, workspace.orgId]
  );

  const handleCreate = useCallback((rt: LibraryResourceType) => {
    Modal.info({
      title: `创建${typeLabelOf(rt)}`,
      content: `${typeLabelOf(rt)} 的创建表单将在后续版本接入。`,
      okText: "我知道了"
    });
  }, []);

  const columns: ColumnProps<AiWorkspaceLibraryItem>[] = useMemo(
    () => [
      {
        title: "资源",
        dataIndex: "name",
        width: "50%",
        render: (_: unknown, record: AiWorkspaceLibraryItem) => (
          <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
            <div
              style={{
                width: 36,
                height: 36,
                borderRadius: 8,
                background: "var(--semi-color-fill-0)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                color: "var(--semi-color-primary)"
              }}
            >
              {iconOf(record.resourceType)}
            </div>
            <div style={{ minWidth: 0 }}>
              <div style={{ fontWeight: 500, color: "var(--semi-color-text-0)" }}>{record.name}</div>
              {record.description ? (
                <div
                  style={{
                    color: "var(--semi-color-text-2)",
                    fontSize: 12,
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                    maxWidth: 360
                  }}
                >
                  {record.description}
                </div>
              ) : null}
            </div>
          </div>
        )
      },
      {
        title: "类型",
        dataIndex: "resourceType",
        width: 160,
        render: (_: unknown, record: AiWorkspaceLibraryItem) => (
          <Typography.Text type="tertiary">{record.typeLabel ?? typeLabelOf(record.resourceType)}</Typography.Text>
        )
      },
      {
        title: "编辑时间",
        dataIndex: "updatedAt",
        width: 180,
        render: (_: unknown, record: AiWorkspaceLibraryItem) => (
          <Typography.Text type="tertiary">{formatDate(record.updatedAt)}</Typography.Text>
        )
      },
      {
        title: "",
        dataIndex: "_action",
        width: 60,
        render: () => (
          <Button theme="borderless" type="tertiary" icon={<IconChevronDown />} size="small" />
        )
      }
    ],
    []
  );

  return (
    <div
      className="coze-page coze-library-page"
      data-testid="coze-library-page"
      style={{ padding: 24, display: "flex", flexDirection: "column", gap: 16, height: "100%" }}
    >
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 16 }}>
        <Tabs
          type="button"
          activeKey={activeTab}
          onChange={key => {
            setActiveTab(key as LibraryTabKey);
            setPageIndex(1);
          }}
          tabList={TABS.map(t => ({ itemKey: t.key, tab: t.label }))}
        />
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <Input
            prefix={<IconSearch />}
            placeholder="搜索资源"
            value={keyword}
            onChange={v => {
              setKeyword(v);
              setPageIndex(1);
            }}
            showClear
            style={{ width: 240 }}
          />
          <Button onClick={() => setImportOpen(true)}>导入</Button>
          <Dropdown
            trigger="click"
            position="bottomRight"
            render={
              <Dropdown.Menu>
                {CREATE_ITEMS.map(item => (
                  <Dropdown.Item key={item.key} icon={item.icon} onClick={() => handleCreate(item.key)}>
                    {item.label}
                  </Dropdown.Item>
                ))}
              </Dropdown.Menu>
            }
          >
            <Button theme="solid" type="primary" icon={<IconPlus />}>
              资源
            </Button>
          </Dropdown>
        </div>
      </div>

      <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
        <Select
          value="all"
          style={{ width: 120 }}
          optionList={[{ value: "all", label: "全部" }]}
          disabled
        />
        <Select
          value={source}
          onChange={v => {
            setSource(v as LibrarySource);
            setPageIndex(1);
          }}
          style={{ width: 140 }}
          optionList={SOURCE_OPTIONS}
        />
      </div>

      <div style={{ flex: 1, minHeight: 0, overflow: "auto" }}>
        {loading ? (
          <div style={{ display: "flex", justifyContent: "center", padding: 48 }}>
            <Spin />
          </div>
        ) : items.length === 0 ? (
          <Empty description="暂无资源" style={{ padding: 48 }} />
        ) : (
          <Table
            columns={columns}
            dataSource={items}
            rowKey={record => `${record.resourceType}-${record.resourceId}`}
            onRow={record => ({
              onClick: () => handleOpen(record),
              style: { cursor: "pointer" }
            })}
            pagination={{
              currentPage: pageIndex,
              pageSize: 20,
              total,
              onPageChange: p => setPageIndex(p)
            }}
          />
        )}
      </div>

      <ImportLibraryModal
        visible={importOpen}
        onClose={() => setImportOpen(false)}
        onImported={() => {
          setImportOpen(false);
          void load();
        }}
      />
    </div>
  );
}

interface ImportLibraryModalProps {
  visible: boolean;
  onClose: () => void;
  onImported: () => void;
}

function ImportLibraryModal({ visible, onClose, onImported }: ImportLibraryModalProps) {
  const [submitting, setSubmitting] = useState(false);
  const [resourceType, setResourceType] = useState<LibraryResourceType>("knowledge-base");
  const [libraryItemId, setLibraryItemId] = useState<number | undefined>(undefined);

  const handleOk = useCallback(async () => {
    if (!libraryItemId || libraryItemId <= 0) {
      Toast.warning("请填写资源库条目 ID");
      return;
    }
    setSubmitting(true);
    try {
      await importLibraryItem({
        resourceType: resourceType as "workflow" | "plugin" | "knowledge-base" | "database",
        libraryItemId
      });
      Toast.success("已导入资源库条目");
      onImported();
    } catch (error) {
      Toast.error((error as Error).message || "导入失败");
    } finally {
      setSubmitting(false);
    }
  }, [libraryItemId, onImported, resourceType]);

  return (
    <Modal
      visible={visible}
      title="从资源库导入"
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={submitting}
      okText="导入"
      cancelText="取消"
    >
      <Form labelPosition="left" labelWidth={96}>
        <Form.Slot label="资源类型">
          <Select
            value={resourceType}
            onChange={v => setResourceType(v as LibraryResourceType)}
            optionList={IMPORTABLE_TYPES}
            style={{ width: "100%" }}
          />
        </Form.Slot>
        <Form.Slot label="条目 ID">
          <InputNumber
            value={libraryItemId}
            onChange={v => setLibraryItemId(typeof v === "number" ? v : Number(v) || undefined)}
            style={{ width: "100%" }}
            placeholder="填写资源库项目 ID"
          />
        </Form.Slot>
      </Form>
    </Modal>
  );
}
