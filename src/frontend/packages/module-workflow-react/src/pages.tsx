import { useEffect, useMemo, useState } from "react";
import {
  Banner,
  Button,
  Empty,
  Input,
  Modal,
  Radio,
  Space,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import type { TagColor } from "@douyinfe/semi-ui/lib/es/tag";
import type {
  WorkflowCreateRequest,
  WorkflowListItem,
  WorkflowPageProps,
  WorkflowResourceMode,
  WorkflowStatusFilter,
  WorkflowTemplateSummary
} from "./types";
import { WorkflowEditorShell } from "./workflow-editor-shell";

function formatDate(value?: string) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function getModeLabel(mode: WorkflowResourceMode) {
  return mode === "chatflow" ? "Chatflow" : "Workflow";
}

function getStatusLabel(status?: number) {
  if (status === 1) {
    return { color: "green" as TagColor, text: "已发布" };
  }
  if (status === 2) {
    return { color: "grey" as TagColor, text: "已归档" };
  }
  return { color: "blue" as TagColor, text: "草稿" };
}

function normalizeItemMode(item: WorkflowListItem): WorkflowResourceMode {
  return item.mode === 1 ? "chatflow" : "workflow";
}

function matchesStatus(item: WorkflowListItem, status: WorkflowStatusFilter) {
  if (status === "all") {
    return true;
  }
  if (status === "published") {
    return item.status === 1;
  }
  return item.status !== 1;
}

function TemplateCard({
  template,
  active,
  onClick
}: {
  template: WorkflowTemplateSummary;
  active: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      className={`module-workflow__template${active ? " is-active" : ""}`}
      onClick={onClick}
      data-testid={`workflow-template-${template.id}`}
    >
      <div className="module-workflow__template-head">
        <strong>{template.title}</strong>
        {template.badge ? <Tag color="light-blue">{template.badge}</Tag> : null}
      </div>
      <p>{template.description}</p>
    </button>
  );
}

export function WorkflowListPage({
  api,
  onOpenEditor,
  mode = "workflow",
  initialCreateVisible = false
}: WorkflowPageProps & { onOpenEditor: (workflowId: string) => void; mode?: WorkflowResourceMode; initialCreateVisible?: boolean }) {
  const [items, setItems] = useState<WorkflowListItem[]>([]);
  const [keyword, setKeyword] = useState("");
  const [status, setStatus] = useState<WorkflowStatusFilter>("all");
  const [templates, setTemplates] = useState<WorkflowTemplateSummary[]>([]);
  const [createVisible, setCreateVisible] = useState(initialCreateVisible);
  const [creating, setCreating] = useState(false);
  const [selectedTemplateId, setSelectedTemplateId] = useState<string>("");
  const [draftName, setDraftName] = useState("");
  const [draftDescription, setDraftDescription] = useState("");

  const load = async () => {
    const result = await api.listWorkflows({ pageIndex: 1, pageSize: 100, keyword, mode, status });
    setItems(result.items);
  };

  useEffect(() => {
    void load();
  }, [mode]);

  useEffect(() => {
    setCreateVisible(initialCreateVisible);
  }, [initialCreateVisible]);

  useEffect(() => {
    void api.listTemplates(mode).then((next) => {
      setTemplates(next);
      const first = next[0];
      if (first) {
        setSelectedTemplateId(first.id);
        setDraftName(`${first.title}_${Date.now().toString().slice(-6)}`);
        setDraftDescription(first.description);
      }
    });
  }, [api, mode]);

  const filteredItems = useMemo(
    () => items.filter((item) => normalizeItemMode(item) === mode && matchesStatus(item, status)),
    [items, mode, status]
  );

  const selectedTemplate = useMemo(
    () => templates.find((item) => item.id === selectedTemplateId) ?? null,
    [selectedTemplateId, templates]
  );

  const title = mode === "chatflow" ? "Chatflow" : "Workflow";
  const subtitle =
    mode === "chatflow"
      ? "通过模板创建对话流，并进入连续调试与发布。"
      : "通过模板创建工作流，并进入连续编排与测试运行。";

  async function handleCreate() {
    const safeName = draftName.trim() || `${title}_${Date.now().toString().slice(-6)}`;
    const request: WorkflowCreateRequest = {
      name: safeName,
      description: draftDescription.trim() || selectedTemplate?.description,
      mode,
      createSource: selectedTemplate ? selectedTemplate.createSource : "blank",
      templateId: selectedTemplate?.id
    };

    setCreating(true);
    try {
      const workflowId = await api.createWorkflow(request);
      setCreateVisible(false);
      Toast.success(`已创建${title}`);
      onOpenEditor(workflowId);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : `创建${title}失败`);
    } finally {
      setCreating(false);
    }
  }

  async function handleDuplicate(id: string) {
    try {
      const duplicatedId = await api.duplicateWorkflow(id);
      Toast.success(`已复制${title}`);
      onOpenEditor(duplicatedId);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : `复制${title}失败`);
    }
  }

  async function handleDelete(id: string) {
    Modal.confirm({
      title: `删除${title}`,
      content: `删除后不可恢复，确认删除当前${title}吗？`,
      onOk: async () => {
        await api.deleteWorkflow(id);
        Toast.success(`已删除${title}`);
        await load();
      }
    });
  }

  return (
    <section className="module-workflow__page" data-testid={mode === "chatflow" ? "app-chatflows-page" : "app-workflows-page"}>
      <div className="module-workflow__hero">
        <div>
          <Typography.Title heading={3} style={{ margin: 0 }}>
            {title}
          </Typography.Title>
          <Typography.Text type="tertiary">{subtitle}</Typography.Text>
        </div>
        <Space>
          <Button
            theme="solid"
            type="primary"
            data-testid={mode === "chatflow" ? "app-chatflows-create" : "app-workflows-create"}
            onClick={() => setCreateVisible(true)}
          >
            新建{title}
          </Button>
        </Space>
      </div>

      <Banner
        type="info"
        bordered={false}
        fullMode={false}
        title={`从模板开始构建${title}`}
        description={`当前支持空白模板与业务模板入口，默认创建草稿后直接进入 ${title} Editor。`}
      />

      <div className="module-workflow__toolbar">
        <Input
          value={keyword}
          onChange={setKeyword}
          placeholder={`搜索${title}名称`}
          showClear
          data-testid={mode === "chatflow" ? "app-chatflows-search" : "app-workflows-search"}
        />
        <div className="module-workflow__filter-group">
          <Radio.Group
            type="button"
            value={status}
            onChange={(event) => setStatus(event.target.value as WorkflowStatusFilter)}
          >
            <Radio value="all">全部</Radio>
            <Radio value="draft">草稿</Radio>
            <Radio value="published">已发布</Radio>
          </Radio.Group>
          <Button onClick={() => void load()}>刷新</Button>
        </div>
      </div>

      <div className="module-workflow__surface">
        {filteredItems.length === 0 ? <Empty title={`暂无${title}`} image={null} /> : null}
        <div className="module-workflow__list">
          {filteredItems.map((item) => {
            const statusTag = getStatusLabel(item.status);
            return (
              <article key={item.id} className="module-workflow__item" data-row-key={item.id}>
                <div className="module-workflow__item-main">
                  <div className="module-workflow__item-head">
                    <div>
                      <strong>{item.name}</strong>
                      <p>{item.description || item.code || "暂无描述"}</p>
                    </div>
                    <Space>
                      <Tag color={normalizeItemMode(item) === "chatflow" ? "purple" : "blue"}>{getModeLabel(normalizeItemMode(item))}</Tag>
                      <Tag color={statusTag.color}>{statusTag.text}</Tag>
                    </Space>
                  </div>
                  <div className="module-workflow__item-meta">
                    <span>最近更新：{formatDate(item.updatedAt)}</span>
                    <span>版本：v{item.latestVersionNumber ?? 0}</span>
                    <span>发布时间：{formatDate(item.publishedAt)}</span>
                  </div>
                </div>
                <div className="module-workflow__item-actions">
                  <Button data-testid={`${mode === "chatflow" ? "app-chatflows-open" : "app-workflows-open"}-${item.id}`} onClick={() => onOpenEditor(item.id)}>
                    打开
                  </Button>
                  <Button onClick={() => void handleDuplicate(item.id)}>复制</Button>
                  <Button type="danger" theme="borderless" onClick={() => void handleDelete(item.id)}>
                    删除
                  </Button>
                </div>
              </article>
            );
          })}
        </div>
      </div>

      <Modal
        title={`新建${title}`}
        visible={createVisible}
        onCancel={() => setCreateVisible(false)}
        okText={`创建${title}`}
        confirmLoading={creating}
        onOk={() => void handleCreate()}
        data-testid={mode === "chatflow" ? "app-chatflows-create-modal" : "app-workflows-create-modal"}
      >
        <div className="module-workflow__create-modal">
          <div className="module-workflow__create-fields">
            <label>
              <span>名称</span>
              <Input value={draftName} onChange={setDraftName} placeholder={`输入${title}名称`} />
            </label>
            <label>
              <span>描述</span>
              <Input value={draftDescription} onChange={setDraftDescription} placeholder="描述用途或场景" />
            </label>
          </div>
          <div className="module-workflow__template-grid">
            {templates.map((template) => (
              <TemplateCard
                key={template.id}
                template={template}
                active={template.id === selectedTemplateId}
                onClick={() => {
                  setSelectedTemplateId(template.id);
                  setDraftName(`${template.title}_${Date.now().toString().slice(-6)}`);
                  setDraftDescription(template.description);
                }}
              />
            ))}
          </div>
        </div>
      </Modal>
    </section>
  );
}

export function WorkflowEditorPage({
  api,
  locale,
  workflowId,
  onBack,
  mode = "workflow"
}: WorkflowPageProps & { workflowId: string; onBack: () => void; mode?: WorkflowResourceMode }) {
  const title = mode === "chatflow" ? "Chatflow Editor" : "Workflow Editor";
  return (
    <section className="module-workflow__page module-workflow__editor-page" data-testid={mode === "chatflow" ? "app-chatflow-editor-page" : "app-workflow-editor-page"}>
      <div className="module-workflow__editor-banner">
        <Typography.Title heading={5} style={{ margin: 0 }}>
          {title}
        </Typography.Title>
        <Typography.Text type="tertiary">
          {mode === "chatflow" ? "面向对话流的连续编排、调试与发布。" : "面向工作流的连续编排、调试与发布。"}
        </Typography.Text>
      </div>
      <WorkflowEditorShell
        api={api}
        locale={locale}
        workflowId={workflowId}
        onBack={onBack}
        mode={mode}
      />
    </section>
  );
}
