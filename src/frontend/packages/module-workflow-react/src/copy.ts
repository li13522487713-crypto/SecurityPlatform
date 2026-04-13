import type { WorkflowResourceMode } from "./types";

export interface WorkflowModuleCopy {
  workflowLabel: string;
  chatflowLabel: string;
  listSubtitleWorkflow: string;
  listSubtitleChatflow: string;
  createButton: (mode: WorkflowResourceMode) => string;
  createSuccess: (mode: WorkflowResourceMode) => string;
  createFailure: (mode: WorkflowResourceMode) => string;
  duplicateSuccess: (mode: WorkflowResourceMode) => string;
  duplicateFailure: (mode: WorkflowResourceMode) => string;
  deleteSuccess: (mode: WorkflowResourceMode) => string;
  deleteTitle: (mode: WorkflowResourceMode) => string;
  deleteContent: (mode: WorkflowResourceMode) => string;
  searchPlaceholder: (mode: WorkflowResourceMode) => string;
  noItems: (mode: WorkflowResourceMode) => string;
  createModalTitle: (mode: WorkflowResourceMode) => string;
  createModalConfirm: (mode: WorkflowResourceMode) => string;
  createFromTemplateTitle: (mode: WorkflowResourceMode) => string;
  createFromTemplateDescription: (mode: WorkflowResourceMode) => string;
  nameLabel: string;
  descriptionLabel: string;
  descriptionPlaceholder: string;
  openLabel: string;
  duplicateLabel: string;
  deleteLabel: string;
  refreshLabel: string;
  allLabel: string;
  draftLabel: string;
  publishedLabel: string;
  updatedAtLabel: string;
  versionLabel: string;
  publishedAtLabel: string;
  draftStatus: string;
  publishedStatus: string;
  archivedStatus: string;
  noDescription: string;
  editorTabLogic: string;
  editorTabUi: string;
  editorUiComingSoon: string;
  editorWorkspaceTitle: string;
  editorWorkspaceSubtitle: string;
  resourcesTab: string;
  referencesTab: string;
  resourcesTitle: string;
  referencesTitle: string;
  sidebarSearchPlaceholder: string;
  sectionWorkflow: string;
  sectionPlugin: string;
  sectionData: string;
  sectionSettings: string;
  sectionReferences: string;
  emptyPlugin: string;
  emptyData: string;
  emptyReferences: string;
  conversationManagement: string;
  variablesLabel: string;
  traceLabel: string;
  testRunLabel: string;
  problemsLabel: string;
  addNodeLabel: string;
  debugLabel: string;
  currentWorkflowLabel: string;
  relatedVersionsLabel: string;
  refreshCanvasLabel: string;
  fitViewLabel: string;
  openUiPreviewLabel: string;
  loadFailure: string;
}

const zhCN: WorkflowModuleCopy = {
  workflowLabel: "工作流",
  chatflowLabel: "Chatflow",
  listSubtitleWorkflow: "通过模板创建工作流，并进入连续编排与测试运行。",
  listSubtitleChatflow: "通过模板创建对话流，并进入连续调试与发布。",
  createButton: mode => `新建${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
  createSuccess: mode => `已创建${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
  createFailure: mode => `创建${mode === "chatflow" ? "Chatflow" : "Workflow"}失败`,
  duplicateSuccess: mode => `已复制${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
  duplicateFailure: mode => `复制${mode === "chatflow" ? "Chatflow" : "Workflow"}失败`,
  deleteSuccess: mode => `已删除${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
  deleteTitle: mode => `删除${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
  deleteContent: mode => `删除后不可恢复，确认删除当前${mode === "chatflow" ? "Chatflow" : "Workflow"}吗？`,
  searchPlaceholder: mode => `搜索${mode === "chatflow" ? "Chatflow" : "Workflow"}名称`,
  noItems: mode => `暂无${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
  createModalTitle: mode => `新建${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
  createModalConfirm: mode => `创建${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
  createFromTemplateTitle: mode => `从模板开始构建${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
  createFromTemplateDescription: mode =>
    `当前支持空白模板与业务模板入口，默认创建草稿后直接进入 ${mode === "chatflow" ? "Chatflow" : "Workflow"} Editor。`,
  nameLabel: "名称",
  descriptionLabel: "描述",
  descriptionPlaceholder: "描述用途或场景",
  openLabel: "打开",
  duplicateLabel: "复制",
  deleteLabel: "删除",
  refreshLabel: "刷新",
  allLabel: "全部",
  draftLabel: "草稿",
  publishedLabel: "已发布",
  updatedAtLabel: "最近更新",
  versionLabel: "版本",
  publishedAtLabel: "发布时间",
  draftStatus: "草稿",
  publishedStatus: "已发布",
  archivedStatus: "已归档",
  noDescription: "暂无描述",
  editorTabLogic: "业务逻辑",
  editorTabUi: "用户界面",
  editorUiComingSoon: "用户界面编辑器仍在迁移，当前先聚焦业务逻辑画布。",
  editorWorkspaceTitle: "Workflow Workspace",
  editorWorkspaceSubtitle: "按 Coze 源码结构重组的 Atlas 工作流宿主。",
  resourcesTab: "资源",
  referencesTab: "引用关系",
  resourcesTitle: "资源",
  referencesTitle: "引用关系",
  sidebarSearchPlaceholder: "搜索当前工作流资源",
  sectionWorkflow: "工作流",
  sectionPlugin: "插件",
  sectionData: "数据",
  sectionSettings: "设置",
  sectionReferences: "关联资源",
  emptyPlugin: "还未添加插件",
  emptyData: "还未添加数据资源",
  emptyReferences: "当前工作流暂未形成额外引用关系。",
  conversationManagement: "会话管理",
  variablesLabel: "变量",
  traceLabel: "Trace",
  testRunLabel: "试运行",
  problemsLabel: "问题",
  addNodeLabel: "添加节点",
  debugLabel: "调试",
  currentWorkflowLabel: "当前工作流",
  relatedVersionsLabel: "相关版本",
  refreshCanvasLabel: "刷新画布",
  fitViewLabel: "适配视图",
  openUiPreviewLabel: "用户界面",
  loadFailure: "加载工作流编辑上下文失败"
};

const enUS: WorkflowModuleCopy = {
  workflowLabel: "Workflow",
  chatflowLabel: "Chatflow",
  listSubtitleWorkflow: "Create workflows from templates and move into continuous orchestration and test runs.",
  listSubtitleChatflow: "Create chatflows from templates and move into continuous debugging and publishing.",
  createButton: mode => `New ${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
  createSuccess: mode => `${mode === "chatflow" ? "Chatflow" : "Workflow"} created`,
  createFailure: mode => `Failed to create ${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
  duplicateSuccess: mode => `${mode === "chatflow" ? "Chatflow" : "Workflow"} duplicated`,
  duplicateFailure: mode => `Failed to duplicate ${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
  deleteSuccess: mode => `${mode === "chatflow" ? "Chatflow" : "Workflow"} deleted`,
  deleteTitle: mode => `Delete ${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
  deleteContent: mode =>
    `This action cannot be undone. Delete the current ${mode === "chatflow" ? "chatflow" : "workflow"}?`,
  searchPlaceholder: mode => `Search ${mode === "chatflow" ? "chatflow" : "workflow"} name`,
  noItems: mode => `No ${mode === "chatflow" ? "chatflows" : "workflows"}`,
  createModalTitle: mode => `New ${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
  createModalConfirm: mode => `Create ${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
  createFromTemplateTitle: mode => `Build ${mode === "chatflow" ? "chatflow" : "workflow"} from a template`,
  createFromTemplateDescription: mode =>
    `Blank and business templates are available. New drafts open directly in the ${mode === "chatflow" ? "Chatflow" : "Workflow"} Editor.`,
  nameLabel: "Name",
  descriptionLabel: "Description",
  descriptionPlaceholder: "Describe the purpose or scenario",
  openLabel: "Open",
  duplicateLabel: "Duplicate",
  deleteLabel: "Delete",
  refreshLabel: "Refresh",
  allLabel: "All",
  draftLabel: "Draft",
  publishedLabel: "Published",
  updatedAtLabel: "Updated",
  versionLabel: "Version",
  publishedAtLabel: "Published",
  draftStatus: "Draft",
  publishedStatus: "Published",
  archivedStatus: "Archived",
  noDescription: "No description",
  editorTabLogic: "Business Logic",
  editorTabUi: "User Interface",
  editorUiComingSoon: "The UI editor is still being migrated. The business logic canvas remains the active workspace for now.",
  editorWorkspaceTitle: "Workflow Workspace",
  editorWorkspaceSubtitle: "Atlas workflow host reorganized around the Coze source layout.",
  resourcesTab: "Resources",
  referencesTab: "References",
  resourcesTitle: "Resources",
  referencesTitle: "References",
  sidebarSearchPlaceholder: "Search workflow resources",
  sectionWorkflow: "Workflow",
  sectionPlugin: "Plugins",
  sectionData: "Data",
  sectionSettings: "Settings",
  sectionReferences: "Related Resources",
  emptyPlugin: "No plugins added yet",
  emptyData: "No data resources added yet",
  emptyReferences: "No extra references are available for this workflow yet.",
  conversationManagement: "Conversation",
  variablesLabel: "Variables",
  traceLabel: "Trace",
  testRunLabel: "Test Run",
  problemsLabel: "Problems",
  addNodeLabel: "Add Node",
  debugLabel: "Debug",
  currentWorkflowLabel: "Current Workflow",
  relatedVersionsLabel: "Related Versions",
  refreshCanvasLabel: "Refresh Canvas",
  fitViewLabel: "Fit View",
  openUiPreviewLabel: "User Interface",
  loadFailure: "Failed to load workflow editor context"
};

export function getWorkflowModuleCopy(locale: "zh-CN" | "en-US"): WorkflowModuleCopy {
  return locale === "en-US" ? enUS : zhCN;
}

