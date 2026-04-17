import { useEffect, useMemo, useState } from "react";
import { Button, Dropdown, Empty, Input, Modal, Select, Spin, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { IconChevronDown, IconPlus } from "@douyinfe/semi-icons";
import { useNavigate } from "react-router-dom";
import {
  chatflowEditorPath,
  workflowEditorPath
} from "@atlas/app-shell-shared";
import { useAppI18n } from "../i18n";
import { listWorkflows, type WorkflowListItem } from "../../services/api-workflow";
import { CreateWorkflowModal, type CreateWorkflowMode } from "./create-workflow-modal";

interface AddWorkflowModalProps {
  visible: boolean;
  workspaceId: string;
  /**
   * 已经被当前对象（智能体/应用）绑定的工作流 / 对话流 ID 集合，
   * 用于在列表上禁用"添加"按钮并显示"已添加"。
   */
  selectedIds?: Set<string>;
  onClose: () => void;
  onAdd: (workflow: WorkflowListItem) => Promise<void> | void;
}

type SourceKey = "create" | "import" | "library";
type TypeFilter = "all" | "workflow" | "chatflow";
type StatusFilter = "all" | "published" | "draft";

/**
 * 添加工作流弹窗（PRD 05-3）。
 *
 * 三栏式：
 * - 左侧来源区：搜索 + 创建工作流（含下拉创建工作流/对话流） + 导入 + 资源库工作流
 * - 顶部筛选区：类型 + 状态
 * - 中间列表区：复用 listWorkflows REST，按筛选展示
 *
 * 列表点击"添加"会回调外层 onAdd（通常去 bindAiAssistantWorkflow）。
 */
export function AddWorkflowModal({
  visible,
  workspaceId,
  selectedIds,
  onClose,
  onAdd
}: AddWorkflowModalProps) {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const [keyword, setKeyword] = useState("");
  const [source, setSource] = useState<SourceKey>("library");
  const [typeFilter, setTypeFilter] = useState<TypeFilter>("all");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
  const [items, setItems] = useState<WorkflowListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [creatingMode, setCreatingMode] = useState<CreateWorkflowMode | null>(null);
  const [adding, setAdding] = useState<string | null>(null);

  const refresh = () => {
    setLoading(true);
    listWorkflows(1, 100, keyword.trim() || undefined, workspaceId)
      .then(response => {
        setItems(response.data?.items ?? []);
      })
      .catch(() => {
        setItems([]);
      })
      .finally(() => {
        setLoading(false);
      });
  };

  useEffect(() => {
    if (visible) {
      refresh();
    }
  }, [visible, keyword, workspaceId]);

  const filtered = useMemo(() => {
    return items.filter(item => {
      const matchType =
        typeFilter === "all" ||
        (typeFilter === "workflow" && (item.mode ?? 0) === 0) ||
        (typeFilter === "chatflow" && item.mode === 1);
      const matchStatus =
        statusFilter === "all" ||
        (statusFilter === "published" && item.status === 1) ||
        (statusFilter === "draft" && item.status !== 1);
      return matchType && matchStatus;
    });
  }, [items, statusFilter, typeFilter]);

  const handleAdd = async (workflow: WorkflowListItem) => {
    setAdding(workflow.id);
    try {
      await onAdd(workflow);
      Toast.success(t("cozeAddWorkflowAdded"));
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
    } finally {
      setAdding(null);
    }
  };

  return (
    <>
      <Modal
        title={t("cozeAddWorkflowTitle")}
        visible={visible && creatingMode === null}
        onCancel={onClose}
        footer={null}
        width={780}
        data-testid="coze-add-workflow-modal"
      >
        <div className="coze-add-workflow">
          <aside className="coze-add-workflow__sidebar">
            <Input
              value={keyword}
              onChange={value => setKeyword(value)}
              placeholder={t("cozeAddWorkflowSearchPlaceholder")}
              showClear
            />
            <Dropdown
              trigger="click"
              position="bottomLeft"
              render={(
                <div className="coze-add-workflow__dropdown" role="menu">
                  <button
                    type="button"
                    className="coze-add-workflow__dropdown-item"
                    onClick={() => setCreatingMode("workflow")}
                    data-testid="coze-add-workflow-create-workflow"
                  >
                    {t("cozeAddWorkflowSourceCreateWorkflow")}
                  </button>
                  <button
                    type="button"
                    className="coze-add-workflow__dropdown-item"
                    onClick={() => setCreatingMode("chatflow")}
                    data-testid="coze-add-workflow-create-chatflow"
                  >
                    {t("cozeAddWorkflowSourceCreateChatflow")}
                  </button>
                </div>
              )}
            >
              <button
                type="button"
                className={`coze-add-workflow__source${source === "create" ? " is-active" : ""}`}
                onClick={() => setSource("create")}
                data-testid="coze-add-workflow-source-create"
              >
                <IconPlus size="small" />
                <span>{t("cozeAddWorkflowSourceCreate")}</span>
                <IconChevronDown size="small" />
              </button>
            </Dropdown>
            <button
              type="button"
              className={`coze-add-workflow__source${source === "import" ? " is-active" : ""}`}
              onClick={() => {
                setSource("import");
                Toast.info(t("cozeAddWorkflowImportComingSoon"));
              }}
              data-testid="coze-add-workflow-source-import"
            >
              <span>{t("cozeAddWorkflowSourceImport")}</span>
            </button>
            <button
              type="button"
              className={`coze-add-workflow__source${source === "library" ? " is-active" : ""}`}
              onClick={() => setSource("library")}
              data-testid="coze-add-workflow-source-library"
            >
              <span>{t("cozeAddWorkflowSourceLibrary")}</span>
            </button>
          </aside>

          <section className="coze-add-workflow__main">
            <div className="coze-add-workflow__filters">
              <Select
                value={typeFilter}
                style={{ width: 140 }}
                optionList={[
                  { label: t("cozeAddWorkflowFilterTypeAll"), value: "all" },
                  { label: t("cozeAddWorkflowFilterTypeWorkflow"), value: "workflow" },
                  { label: t("cozeAddWorkflowFilterTypeChatflow"), value: "chatflow" }
                ]}
                onChange={value => setTypeFilter(value as TypeFilter)}
              />
              <Select
                value={statusFilter}
                style={{ width: 140 }}
                optionList={[
                  { label: t("cozeAddWorkflowFilterStatusAll"), value: "all" },
                  { label: t("cozeAddWorkflowFilterStatusPublished"), value: "published" },
                  { label: t("cozeAddWorkflowFilterStatusDraft"), value: "draft" }
                ]}
                onChange={value => setStatusFilter(value as StatusFilter)}
              />
            </div>

            {loading ? (
              <div className="coze-page__loading"><Spin /></div>
            ) : filtered.length === 0 ? (
              <Empty description={t("cozeAddWorkflowEmpty")} />
            ) : (
              <ul className="coze-add-workflow__list">
                {filtered.map(item => {
                  const isAdded = selectedIds?.has(item.id) ?? false;
                  const isAdding = adding === item.id;
                  return (
                    <li key={item.id} className="coze-add-workflow__item" data-testid={`coze-add-workflow-item-${item.id}`}>
                      <div className="coze-add-workflow__item-meta">
                        <strong>{item.name}</strong>
                        <Typography.Text type="tertiary" size="small">{item.description ?? ""}</Typography.Text>
                        <div className="coze-add-workflow__item-tags">
                          <Tag size="small" color={item.mode === 1 ? "violet" : "blue"}>
                            {item.mode === 1 ? t("cozeAddWorkflowFilterTypeChatflow") : t("cozeAddWorkflowFilterTypeWorkflow")}
                          </Tag>
                          <Tag size="small" color={item.status === 1 ? "green" : "grey"}>
                            {item.status === 1 ? t("cozeAddWorkflowFilterStatusPublished") : t("cozeAddWorkflowFilterStatusDraft")}
                          </Tag>
                        </div>
                      </div>
                      <div className="coze-add-workflow__item-actions">
                        <Button
                          theme="borderless"
                          onClick={() => navigate(item.mode === 1 ? chatflowEditorPath(item.id) : workflowEditorPath(item.id))}
                        >
                          {t("cozeAddWorkflowOpenInNewTab")}
                        </Button>
                        <Button
                          type="primary"
                          theme="solid"
                          loading={isAdding}
                          disabled={isAdded}
                          onClick={() => void handleAdd(item)}
                          data-testid={`coze-add-workflow-add-${item.id}`}
                        >
                          {isAdded ? t("cozeAddWorkflowAdded") : t("cozeAddWorkflowAdd")}
                        </Button>
                      </div>
                    </li>
                  );
                })}
              </ul>
            )}
          </section>
        </div>
      </Modal>

      <CreateWorkflowModal
        visible={creatingMode !== null}
        mode={creatingMode ?? "workflow"}
        workspaceId={workspaceId}
        onClose={() => setCreatingMode(null)}
        onCreated={() => {
          setCreatingMode(null);
          onClose();
        }}
      />
    </>
  );
}
