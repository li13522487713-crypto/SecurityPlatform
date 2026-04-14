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
import { CreateWizardModal } from "@atlas/module-studio-react";
import type { TagColor } from "@douyinfe/semi-ui/lib/es/tag";
import type {
  WorkflowCreateRequest,
  WorkflowListItem,
  WorkflowPageProps,
  WorkflowWorkbenchNavigation,
  WorkflowResourceMode,
  WorkflowStatusFilter,
  WorkflowTemplateSummary
} from "./types";
import { getWorkflowModuleCopy } from "./copy";
import { WorkflowEditorShell } from "./workflow-editor-shell";

function formatDate(value?: string) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

const EXPLORE_CREATED_TEMPLATE_STORAGE_KEY = "atlas_explore_created_templates";

interface ExploreCreatedTemplateState {
  route: string;
  workflowId: string;
  mode: "workflow" | "chatflow";
  templateId: number;
  templateName: string;
  createdAt: string;
}

function readExploreCreatedTemplateStateMap(): Record<string, ExploreCreatedTemplateState> {
  if (typeof window === "undefined") {
    return {};
  }

  const raw = window.localStorage.getItem(EXPLORE_CREATED_TEMPLATE_STORAGE_KEY);
  if (!raw) {
    return {};
  }

  try {
    const parsed = JSON.parse(raw) as Record<string, ExploreCreatedTemplateState | string>;
    return Object.fromEntries(
      Object.entries(parsed).map(([key, value]) => {
        if (typeof value === "string") {
          return [key, {
            route: value,
            workflowId: "",
            mode: "workflow",
            templateId: Number(key),
            templateName: "",
            createdAt: ""
          } satisfies ExploreCreatedTemplateState];
        }

        return [key, value];
      })
    );
  } catch {
    return {};
  }
}

function getModeLabel(mode: WorkflowResourceMode, workflowLabel: string, chatflowLabel: string) {
  return mode === "chatflow" ? chatflowLabel : workflowLabel;
}

function getStatusLabel(status: number | undefined, labels: { published: string; archived: string; draft: string }) {
  if (status === 1) {
    return { color: "green" as TagColor, text: labels.published };
  }
  if (status === 2) {
    return { color: "grey" as TagColor, text: labels.archived };
  }
  return { color: "blue" as TagColor, text: labels.draft };
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

export function WorkflowListPage({
  api,
  locale,
  onOpenEditor,
  selectedWorkflowId,
  onSelectWorkflow,
  resolveWorkflowHref,
  contentMode = "canvas",
  onSelectContentMode,
  projectTitle,
  mode = "workflow",
  initialCreateVisible = false
}: WorkflowPageProps & WorkflowWorkbenchNavigation & { onOpenEditor: (workflowId: string) => void; mode?: WorkflowResourceMode; initialCreateVisible?: boolean }) {
  const copy = getWorkflowModuleCopy(locale);
  const [items, setItems] = useState<WorkflowListItem[]>([]);
  const [keyword, setKeyword] = useState("");
  const [status, setStatus] = useState<WorkflowStatusFilter>("all");
  const [templates, setTemplates] = useState<WorkflowTemplateSummary[]>([]);
  const [createVisible, setCreateVisible] = useState(initialCreateVisible);
  const [createdTemplates, setCreatedTemplates] = useState<Record<string, ExploreCreatedTemplateState>>(() => readExploreCreatedTemplateStateMap());

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
    });
  }, [api, mode]);

  useEffect(() => {
    setCreatedTemplates(readExploreCreatedTemplateStateMap());
  }, [items, mode]);

  const filteredItems = useMemo(
    () => items.filter((item) => normalizeItemMode(item) === mode && matchesStatus(item, status)),
    [items, mode, status]
  );

  const selectedItem = useMemo(() => {
    if (filteredItems.length === 0) {
      return null;
    }

    if (selectedWorkflowId) {
      return filteredItems.find(item => item.id === selectedWorkflowId) ?? filteredItems[0];
    }

    return filteredItems[0];
  }, [filteredItems, selectedWorkflowId]);

  const title = mode === "chatflow" ? copy.chatflowLabel : copy.workflowLabel;
  const subtitle = mode === "chatflow" ? copy.listSubtitleChatflow : copy.listSubtitleWorkflow;

  async function handleCreateWizardSubmit(values: { name: string; description: string; templateId?: string }) {
    const safeName = values.name.trim() || `${title}_${Date.now().toString().slice(-6)}`;
    const template = values.templateId ? templates.find((item) => item.id === values.templateId) : undefined;
    const request: WorkflowCreateRequest = {
      name: safeName,
      description: values.description.trim() || template?.description,
      mode,
      createSource: template ? template.createSource : "blank",
      templateId: template?.id
    };

    try {
      const workflowId = await api.createWorkflow(request);
      setCreateVisible(false);
      Toast.success(copy.createSuccess(mode));
      onOpenEditor(workflowId);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.createFailure(mode));
    }
  }

  async function handleDuplicate(id: string) {
    try {
      const duplicatedId = await api.duplicateWorkflow(id);
      Toast.success(copy.duplicateSuccess(mode));
      onOpenEditor(duplicatedId);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.duplicateFailure(mode));
    }
  }

  async function handleDelete(id: string) {
    Modal.confirm({
      title: copy.deleteTitle(mode),
      content: copy.deleteContent(mode),
      onOk: async () => {
        await api.deleteWorkflow(id);
        Toast.success(copy.deleteSuccess(mode));
        await load();
      }
    });
  }

  useEffect(() => {
    if (!selectedItem || !onSelectWorkflow) {
      return;
    }

    if (selectedWorkflowId === selectedItem.id) {
      return;
    }

    onSelectWorkflow(selectedItem.id, mode);
  }, [mode, onSelectWorkflow, selectedItem, selectedWorkflowId]);

  if (selectedItem) {
    return (
      <section className="module-workflow__page module-workflow__editor-page" data-testid={mode === "chatflow" ? "app-chatflows-page" : "app-workflows-page"}>
        <WorkflowEditorShell
          api={api}
          locale={locale}
          workflowId={selectedItem.id}
          mode={mode}
          contentMode={contentMode}
          onSelectContentMode={onSelectContentMode}
          projectTitle={projectTitle}
          onBack={() => window.history.back()}
          onSelectWorkflow={onSelectWorkflow}
          resolveWorkflowHref={resolveWorkflowHref}
        />
      </section>
    );
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
            {copy.createButton(mode)}
          </Button>
        </Space>
      </div>

      <Banner
        type="info"
        bordered={false}
        fullMode={false}
        title={copy.createFromTemplateTitle(mode)}
        description={copy.createFromTemplateDescription(mode)}
      />

      <div className="module-workflow__toolbar">
        <Input
          value={keyword}
          onChange={setKeyword}
          placeholder={copy.searchPlaceholder(mode)}
          showClear
          data-testid={mode === "chatflow" ? "app-chatflows-search" : "app-workflows-search"}
        />
        <div className="module-workflow__filter-group">
          <Radio.Group
            type="button"
            value={status}
            onChange={(event) => setStatus(event.target.value as WorkflowStatusFilter)}
          >
            <Radio value="all">{copy.allLabel}</Radio>
            <Radio value="draft">{copy.draftLabel}</Radio>
            <Radio value="published">{copy.publishedLabel}</Radio>
          </Radio.Group>
          <Button onClick={() => void load()}>{copy.refreshLabel}</Button>
        </div>
      </div>

      <div className="module-workflow__surface">
        {filteredItems.length === 0 ? <Empty title={copy.noItems(mode)} image={null} /> : null}
        <div className="module-workflow__list">
          {filteredItems.map((item) => {
            const statusTag = getStatusLabel(item.status, {
              published: copy.publishedStatus,
              archived: copy.archivedStatus,
              draft: copy.draftStatus
            });
            const templateSource = Object.values(createdTemplates).find(source => source.workflowId === item.id);
            return (
              <article key={item.id} className="module-workflow__item" data-row-key={item.id}>
                <div className="module-workflow__item-main">
                  <div className="module-workflow__item-head">
                    <div>
                      <strong>{item.name}</strong>
                      <p>{item.description || item.code || copy.noDescription}</p>
                    </div>
                    <Space>
                      <Tag color={normalizeItemMode(item) === "chatflow" ? "purple" : "blue"}>
                        {getModeLabel(normalizeItemMode(item), copy.workflowLabel, copy.chatflowLabel)}
                      </Tag>
                      <Tag color={statusTag.color}>{statusTag.text}</Tag>
                    </Space>
                  </div>
                  <div className="module-workflow__item-meta">
                    <span>{copy.updatedAtLabel}：{formatDate(item.updatedAt)}</span>
                    <span>{copy.versionLabel}：v{item.latestVersionNumber ?? 0}</span>
                    <span>{copy.publishedAtLabel}：{formatDate(item.publishedAt)}</span>
                  </div>
                  {templateSource ? (
                    <div className="module-workflow__item-meta">
                      <Tag color="green">来自模板市场</Tag>
                      <span>模板：{templateSource.templateName || `模板#${templateSource.templateId}`}</span>
                      <span>创建时间：{formatDate(templateSource.createdAt)}</span>
                    </div>
                  ) : null}
                </div>
                <div className="module-workflow__item-actions">
                  <Button
                    data-testid={`${mode === "chatflow" ? "app-chatflows-open" : "app-workflows-open"}-${item.id}`}
                    onClick={() => onOpenEditor(item.id)}
                  >
                    {copy.openLabel}
                  </Button>
                  <Button onClick={() => void handleDuplicate(item.id)}>{copy.duplicateLabel}</Button>
                  <Button type="danger" theme="borderless" onClick={() => void handleDelete(item.id)}>
                    {copy.deleteLabel}
                  </Button>
                </div>
              </article>
            );
          })}
        </div>
      </div>

      <div data-testid={mode === "chatflow" ? "app-chatflows-create-modal" : "app-workflows-create-modal"}>
        <CreateWizardModal
          visible={createVisible}
          title={copy.createModalTitle(mode)}
          resourceType={mode === "chatflow" ? "chatflow" : "workflow"}
          templates={templates.map((template) => ({
            id: template.id,
            name: template.title,
            description: template.description
          }))}
          onCancel={() => setCreateVisible(false)}
          onSubmit={async (values) => {
            await handleCreateWizardSubmit(values);
          }}
          texts={{
            okText: copy.createModalConfirm(mode),
            cancelText: copy.cancelLabel,
            blankMode: copy.createWizardBlankMode,
            templateMode: copy.createWizardTemplateMode,
            nameLabel: copy.nameLabel,
            descriptionLabel: copy.descriptionLabel,
            templateSelectLabel: copy.createWizardTemplateSelect,
            namePlaceholder: `${copy.nameLabel} · ${title}`,
            descriptionPlaceholder: copy.descriptionPlaceholder
          }}
        />
      </div>
    </section>
  );
}

export function WorkflowEditorPage({
  api,
  locale,
  workflowId,
  onBack,
  backPath,
  projectTitle,
  mode = "workflow"
}: WorkflowPageProps & { workflowId: string; onBack: () => void; backPath?: string; mode?: WorkflowResourceMode; projectTitle?: string }) {
  return (
    <section className="module-workflow__page module-workflow__editor-page" data-testid={mode === "chatflow" ? "app-chatflow-editor-page" : "app-workflow-editor-page"}>
      <WorkflowEditorShell
        api={api}
        locale={locale}
        workflowId={workflowId}
        onBack={onBack}
        backPath={backPath}
        projectTitle={projectTitle}
        mode={mode}
      />
    </section>
  );
}
