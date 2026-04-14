import type { WorkflowResourceMode } from "./types";

type CreateKind = "workflow" | "chatflow" | "plugin" | "knowledge-base" | "database";

export interface WorkflowModuleCopy {
  workflowLabel: string;
  chatflowLabel: string;
  pluginLabel: string;
  knowledgeLabel: string;
  databaseLabel: string;
  listSubtitleWorkflow: string;
  listSubtitleChatflow: string;
  createButton: (mode: WorkflowResourceMode) => string;
  createSuccess: (mode: WorkflowResourceMode) => string;
  createFailure: (mode: WorkflowResourceMode) => string;
  duplicateSuccess: (mode: WorkflowResourceMode) => string;
  duplicateFailure: (mode: WorkflowResourceMode) => string;
  deleteSuccess: (label: string) => string;
  deleteTitle: (mode: WorkflowResourceMode) => string;
  deleteContent: (mode: WorkflowResourceMode) => string;
  searchPlaceholder: (mode: WorkflowResourceMode) => string;
  noItems: (mode: WorkflowResourceMode) => string;
  createModalTitle: (mode: WorkflowResourceMode) => string;
  createModalConfirm: (mode: WorkflowResourceMode) => string;
  createFromTemplateTitle: (mode: WorkflowResourceMode) => string;
  createFromTemplateDescription: (mode: WorkflowResourceMode) => string;
  createWizardBlankMode: string;
  createWizardTemplateMode: string;
  createWizardTemplateSelect: string;
  createDialogTitle: (kind: CreateKind) => string;
  createDialogConfirm: (kind: CreateKind) => string;
  createDialogGlyph: (kind: CreateKind) => string;
  workflowNameValidation: string;
  variableKeyValidation: string;
  databaseSchemaValidationFailed: string;
  requiredField: string;
  nameLabel: string;
  descriptionLabel: string;
  descriptionPlaceholder: string;
  openLabel: string;
  duplicateLabel: string;
  deleteLabel: string;
  editLabel: string;
  saveLabel: string;
  publishLabel: string;
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
  resourcesTab: string;
  referencesTab: string;
  resourcesTitle: string;
  referencesTitle: string;
  sidebarSearchPlaceholder: string;
  searchLibraryPlaceholder: string;
  sectionWorkflow: string;
  sectionPlugin: string;
  sectionData: string;
  sectionSettings: string;
  sectionReferences: string;
  emptyWorkflow: string;
  emptyPlugin: string;
  emptyData: string;
  emptyReferences: string;
  conversationManagement: string;
  conversationLabel: string;
  conversationDescription: string;
  conversationCreateLabel: string;
  conversationCreateFailure: string;
  conversationLoadFailure: string;
  conversationAgentRequired: string;
  conversationTitleLabel: string;
  conversationUnit: string;
  agentLabel: string;
  agentLoadFailure: string;
  selectAgentPlaceholder: string;
  appendMessageLabel: string;
  variablesLabel: string;
  variablesDescription: string;
  variableCreateLabel: string;
  variableEditLabel: string;
  variableKeyLabel: string;
  variableValueLabel: string;
  variableLoadFailure: string;
  variableSaveFailure: string;
  systemVariableReadonly: string;
  traceLabel: string;
  testRunLabel: string;
  problemsLabel: string;
  addNodeLabel: string;
  debugLabel: string;
  currentWorkflowLabel: string;
  relatedVersionsLabel: string;
  refreshCanvasLabel: string;
  loadFailure: string;
  libraryDialogTitle: string;
  libraryLoadFailure: string;
  libraryImportSuccess: string;
  libraryImportFailure: string;
  librarySelectRequired: string;
  libraryExportLabel: string;
  libraryMoveLabel: string;
  menuCreateWorkflow: string;
  menuCreateChatflow: string;
  menuCreatePlugin: string;
  menuCreateKnowledge: string;
  menuCreateDatabase: string;
  menuImportLibrary: string;
  pluginCategoryLabel: string;
  knowledgeTypeLabel: string;
  knowledgeTypeTextLabel: string;
  knowledgeTypeTableLabel: string;
  knowledgeTypeImageLabel: string;
  pluginApiCountLabel: string;
  pluginLoadFailure: string;
  knowledgeLoadFailure: string;
  databaseLoadFailure: string;
  databaseSchemaLabel: string;
  databaseRecordCountLabel: string;
  documentCountLabel: string;
  chunkCountLabel: string;
  documentUnit: string;
  chunkUnit: string;
  recordUnit: string;
  variableScopeLabel: string;
  variableScopeIdLabel: string;
  variableScopeGlobalLabel: string;
  variableScopeProjectLabel: string;
  variableScopeBotLabel: string;
  clearContextLabel: string;
  clearHistoryLabel: string;
  cancelLabel: string;
  traceDockRunningHint: string;
  traceDockClearTrace: string;
  traceDockCollapse: string;
  traceDockExpand: string;
  variableRefTitle: string;
  variableRefGlobalsTitle: string;
  variableRefDependencyTitle: string;
  variableRefEmptyGlobals: string;
  variableRefEmptyDependencies: string;
  variableRefOpenVariables: string;
  variableRefSourceNodes: string;
  variableRefToggleShow: string;
  variableRefToggleHide: string;
}

function createCopy(locale: "zh-CN" | "en-US"): WorkflowModuleCopy {
  const zh = locale === "zh-CN";
  const workflowWord = zh ? "工作流" : "Workflow";
  const chatflowWord = zh ? "对话流" : "Chatflow";

  return {
    workflowLabel: zh ? "工作流" : "Workflow",
    chatflowLabel: zh ? "Chatflow" : "Chatflow",
    pluginLabel: zh ? "插件" : "Plugin",
    knowledgeLabel: zh ? "知识库" : "Knowledge Base",
    databaseLabel: zh ? "数据库" : "Database",
    listSubtitleWorkflow: zh ? "通过模板创建工作流，并进入连续编排与测试运行。" : "Create workflows from templates and move into orchestration and test runs.",
    listSubtitleChatflow: zh ? "通过模板创建对话流，并进入连续调试与发布。" : "Create chatflows from templates and move into debugging and publishing.",
    createButton: mode => zh ? `新建${mode === "chatflow" ? "Chatflow" : "Workflow"}` : `New ${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
    createSuccess: mode => zh ? `已创建${mode === "chatflow" ? "Chatflow" : "Workflow"}` : `${mode === "chatflow" ? "Chatflow" : "Workflow"} created`,
    createFailure: mode => zh ? `创建${mode === "chatflow" ? "Chatflow" : "Workflow"}失败` : `Failed to create ${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
    duplicateSuccess: mode => zh ? `已复制${mode === "chatflow" ? "Chatflow" : "Workflow"}` : `${mode === "chatflow" ? "Chatflow" : "Workflow"} duplicated`,
    duplicateFailure: mode => zh ? `复制${mode === "chatflow" ? "Chatflow" : "Workflow"}失败` : `Failed to duplicate ${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
    deleteSuccess: label => zh ? `已删除${label}` : `${label} deleted`,
    deleteTitle: mode => zh ? `删除${mode === "chatflow" ? "Chatflow" : "Workflow"}` : `Delete ${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
    deleteContent: mode => zh ? `删除后不可恢复，确认删除当前${mode === "chatflow" ? "Chatflow" : "Workflow"}吗？` : `This action cannot be undone. Delete the current ${mode === "chatflow" ? "chatflow" : "workflow"}?`,
    searchPlaceholder: mode => zh ? `搜索${mode === "chatflow" ? "Chatflow" : "Workflow"}名称` : `Search ${mode === "chatflow" ? "chatflow" : "workflow"} name`,
    noItems: mode => zh ? `暂无${mode === "chatflow" ? "Chatflow" : "Workflow"}` : `No ${mode === "chatflow" ? "chatflows" : "workflows"}`,
    createModalTitle: mode => zh ? `新建${mode === "chatflow" ? "Chatflow" : "Workflow"}` : `New ${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
    createModalConfirm: mode => zh ? `创建${mode === "chatflow" ? "Chatflow" : "Workflow"}` : `Create ${mode === "chatflow" ? "Chatflow" : "Workflow"}`,
    createFromTemplateTitle: mode => zh ? `从模板开始构建${mode === "chatflow" ? "Chatflow" : "Workflow"}` : `Build ${mode === "chatflow" ? "chatflow" : "workflow"} from a template`,
    createFromTemplateDescription: mode => zh ? `当前支持空白模板与业务模板入口，默认创建草稿后直接进入 ${mode === "chatflow" ? "Chatflow" : "Workflow"} Editor。` : `Blank and business templates are available. New drafts open directly in the ${mode === "chatflow" ? "Chatflow" : "Workflow"} Editor.`,
    createWizardBlankMode: zh ? "从空白创建" : "Create blank",
    createWizardTemplateMode: zh ? "从模板创建" : "From template",
    createWizardTemplateSelect: zh ? "选择模板" : "Template",
    createDialogTitle: kind => zh
      ? kind === "workflow" ? "创建工作流" : kind === "chatflow" ? "创建对话流" : kind === "plugin" ? "创建插件" : kind === "knowledge-base" ? "创建知识库" : "创建数据库"
      : kind === "workflow" ? "Create workflow" : kind === "chatflow" ? "Create chatflow" : kind === "plugin" ? "Create plugin" : kind === "knowledge-base" ? "Create knowledge base" : "Create database",
    createDialogConfirm: kind => zh ? (kind === "workflow" || kind === "chatflow" ? "确认" : "保存") : (kind === "workflow" || kind === "chatflow" ? "Confirm" : "Save"),
    createDialogGlyph: kind => kind === "workflow" ? "W" : kind === "chatflow" ? "C" : kind === "plugin" ? "P" : kind === "knowledge-base" ? "K" : "D",
    workflowNameValidation: zh ? "工作流名称长度需在 2 到 100 个字符之间。" : "Workflow names must be between 2 and 100 characters.",
    variableKeyValidation: zh ? "变量名首字符不能为数字，且只能包含字母、数字、$ 和 _。" : "Variable keys cannot start with a number and may only contain letters, numbers, $ and _.",
    databaseSchemaValidationFailed: zh ? "数据库 Schema 校验失败。" : "Database schema validation failed.",
    requiredField: zh ? "请填写必填项。" : "This field is required.",
    nameLabel: zh ? "名称" : "Name",
    descriptionLabel: zh ? "描述" : "Description",
    descriptionPlaceholder: zh ? "描述用途或场景" : "Describe the purpose or scenario",
    openLabel: zh ? "打开" : "Open",
    duplicateLabel: zh ? "复制" : "Duplicate",
    deleteLabel: zh ? "删除" : "Delete",
    editLabel: zh ? "编辑" : "Edit",
    saveLabel: zh ? "保存" : "Save",
    publishLabel: zh ? "发布" : "Publish",
    refreshLabel: zh ? "刷新" : "Refresh",
    allLabel: zh ? "全部" : "All",
    draftLabel: zh ? "草稿" : "Draft",
    publishedLabel: zh ? "已发布" : "Published",
    updatedAtLabel: zh ? "最近更新" : "Updated",
    versionLabel: zh ? "版本" : "Version",
    publishedAtLabel: zh ? "发布时间" : "Published",
    draftStatus: zh ? "草稿" : "Draft",
    publishedStatus: zh ? "已发布" : "Published",
    archivedStatus: zh ? "已归档" : "Archived",
    noDescription: zh ? "暂无描述" : "No description",
    editorTabLogic: zh ? "业务逻辑" : "Business Logic",
    editorTabUi: zh ? "用户界面" : "User Interface",
    editorUiComingSoon: zh ? "用户界面编辑器仍在迁移，当前先聚焦业务逻辑画布。" : "The UI editor is still being migrated. The business logic canvas remains the active workspace for now.",
    resourcesTab: zh ? "资源" : "Resources",
    referencesTab: zh ? "引用关系" : "References",
    resourcesTitle: zh ? "资源" : "Resources",
    referencesTitle: zh ? "引用关系" : "References",
    sidebarSearchPlaceholder: zh ? "搜索当前工作流资源" : "Search workflow resources",
    searchLibraryPlaceholder: zh ? "搜索资源库文件" : "Search library resources",
    sectionWorkflow: zh ? "工作流" : "Workflow",
    sectionPlugin: zh ? "插件" : "Plugins",
    sectionData: zh ? "数据" : "Data",
    sectionSettings: zh ? "设置" : "Settings",
    sectionReferences: zh ? "关联资源" : "Related Resources",
    emptyWorkflow: zh ? "还未添加工作流" : "No workflows added yet",
    emptyPlugin: zh ? "还未添加插件" : "No plugins added yet",
    emptyData: zh ? "还未添加数据资源" : "No data resources added yet",
    emptyReferences: zh ? "当前工作流暂未形成额外引用关系。" : "No extra references are available for this workflow yet.",
    conversationManagement: zh ? "会话管理" : "Conversation Management",
    conversationLabel: zh ? "会话" : "Conversation",
    conversationDescription: zh ? "管理多轮对话、上下文和消息历史。" : "Manage multi-turn conversations, context, and message history.",
    conversationCreateLabel: zh ? "新建会话" : "New Conversation",
    conversationCreateFailure: zh ? "创建会话失败" : "Failed to create conversation",
    conversationLoadFailure: zh ? "加载会话数据失败" : "Failed to load conversation data",
    conversationAgentRequired: zh ? "请选择一个智能体。" : "Please select an agent.",
    conversationTitleLabel: zh ? "会话标题" : "Conversation Title",
    conversationUnit: zh ? "个会话" : " conversations",
    agentLabel: zh ? "智能体" : "Agent",
    agentLoadFailure: zh ? "加载智能体列表失败" : "Failed to load agents",
    selectAgentPlaceholder: zh ? "请选择智能体" : "Select an agent",
    appendMessageLabel: zh ? "发送消息" : "Send Message",
    variablesLabel: zh ? "变量" : "Variables",
    variablesDescription: zh ? "管理工作流与应用级变量。" : "Manage workflow and app level variables.",
    variableCreateLabel: zh ? "新建变量" : "New Variable",
    variableEditLabel: zh ? "编辑变量" : "Edit Variable",
    variableKeyLabel: zh ? "变量名" : "Variable Key",
    variableValueLabel: zh ? "变量值" : "Variable Value",
    variableLoadFailure: zh ? "加载变量失败" : "Failed to load variables",
    variableSaveFailure: zh ? "保存变量失败" : "Failed to save variable",
    systemVariableReadonly: zh ? "只读" : "Read only",
    traceLabel: zh ? "执行追踪" : "Trace",
    testRunLabel: zh ? "试运行" : "Test Run",
    problemsLabel: zh ? "问题" : "Problems",
    addNodeLabel: zh ? "添加节点" : "Add Node",
    debugLabel: zh ? "调试" : "Debug",
    currentWorkflowLabel: zh ? "当前工作流" : "Current Workflow",
    relatedVersionsLabel: zh ? "相关版本" : "Related Versions",
    refreshCanvasLabel: zh ? "刷新画布" : "Refresh Canvas",
    loadFailure: zh ? "加载工作流编辑上下文失败" : "Failed to load workflow editor context",
    libraryDialogTitle: zh ? "导入资源库文件" : "Import library resource",
    libraryLoadFailure: zh ? "加载资源库失败" : "Failed to load library resources",
    libraryImportSuccess: zh ? "资源已导入" : "Resource imported",
    libraryImportFailure: zh ? "导入资源库文件失败" : "Failed to import library resource",
    librarySelectRequired: zh ? "请选择一个资源库文件。" : "Please select a library resource.",
    libraryExportLabel: zh ? "复制到资源库" : "Copy to Library",
    libraryMoveLabel: zh ? "移动到资源库" : "Move to Library",
    menuCreateWorkflow: zh ? "新建工作流" : "New workflow",
    menuCreateChatflow: zh ? "新建对话流" : "New chatflow",
    menuCreatePlugin: zh ? "新建插件" : "New plugin",
    menuCreateKnowledge: zh ? "新建知识库" : "New knowledge base",
    menuCreateDatabase: zh ? "新建数据库" : "New database",
    menuImportLibrary: zh ? "导入资源库文件" : "Import from library",
    pluginCategoryLabel: zh ? "分类" : "Category",
    knowledgeTypeLabel: zh ? "知识类型" : "Knowledge Type",
    knowledgeTypeTextLabel: zh ? "文本知识" : "Text Knowledge",
    knowledgeTypeTableLabel: zh ? "表格知识" : "Table Knowledge",
    knowledgeTypeImageLabel: zh ? "图片知识" : "Image Knowledge",
    pluginApiCountLabel: zh ? "接口数" : "API Count",
    pluginLoadFailure: zh ? "加载插件详情失败" : "Failed to load plugin detail",
    knowledgeLoadFailure: zh ? "加载知识库详情失败" : "Failed to load knowledge base detail",
    databaseLoadFailure: zh ? "加载数据库详情失败" : "Failed to load database detail",
    databaseSchemaLabel: zh ? "Schema JSON" : "Schema JSON",
    databaseRecordCountLabel: zh ? "记录数" : "Record Count",
    documentCountLabel: zh ? "文档数" : "Document Count",
    chunkCountLabel: zh ? "分片数" : "Chunk Count",
    documentUnit: zh ? "文档" : " docs",
    chunkUnit: zh ? "分片" : " chunks",
    recordUnit: zh ? "条记录" : " records",
    variableScopeLabel: zh ? "作用域" : "Scope",
    variableScopeIdLabel: zh ? "作用域 ID" : "Scope ID",
    variableScopeGlobalLabel: zh ? "全局" : "Global",
    variableScopeProjectLabel: zh ? "项目" : "Project",
    variableScopeBotLabel: zh ? "智能体" : "Bot",
    clearContextLabel: zh ? "清空上下文" : "Clear Context",
    clearHistoryLabel: zh ? "清空历史" : "Clear History",
    cancelLabel: zh ? "取消" : "Cancel",
    traceDockRunningHint: zh ? "运行中…" : "Running…",
    traceDockClearTrace: zh ? "清空追踪" : "Clear trace",
    traceDockCollapse: zh ? "收起" : "Collapse",
    traceDockExpand: zh ? "展开" : "Expand",
    variableRefTitle: zh ? "变量引用" : "Variable references",
    variableRefGlobalsTitle: zh ? "画布全局变量" : "Canvas globals",
    variableRefDependencyTitle: zh ? "依赖变量" : "Dependency variables",
    variableRefEmptyGlobals: zh ? "画布未配置全局变量。" : "No canvas globals configured.",
    variableRefEmptyDependencies: zh ? "暂无变量依赖。" : "No variable dependencies.",
    variableRefOpenVariables: zh ? "管理变量" : "Manage variables",
    variableRefSourceNodes: zh ? "来源节点" : "Source nodes",
    variableRefToggleShow: zh ? "引用侧栏" : "Ref panel",
    variableRefToggleHide: zh ? "隐藏引用" : "Hide refs"
  };
}

const zhCN = createCopy("zh-CN");
const enUS = createCopy("en-US");

export function getWorkflowModuleCopy(locale: "zh-CN" | "en-US"): WorkflowModuleCopy {
  return locale === "en-US" ? enUS : zhCN;
}
