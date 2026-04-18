import type { DocumentProcessingStatus, KnowledgeBaseType, ResourceType, SupportedLocale } from "./types";

type CopyTree = {
  title: string;
  createResource: string;
  createKnowledge: string;
  createPlugin: string;
  createDatabase: string;
  pluginBasicInfo: string;
  pluginSourceAndAuth: string;
  pluginAdvancedConfig: string;
  pluginOpenApiGuide: string;
  pluginOpenApiHint: string;
  pluginPrefillOpenApi: string;
  pluginPreviewSummary: string;
  pluginPreviewOperations: string;
  pluginPreviewSchemas: string;
  pluginPreviewNext: string;
  pluginPreviewBack: string;
  pluginPreviewEmpty: string;
  pluginValidateOpenApi: string;
  databaseBasicInfo: string;
  databaseSchemaMode: string;
  databaseSchemaStructured: string;
  databaseSchemaRaw: string;
  databaseAddColumn: string;
  databaseDeleteColumn: string;
  databaseFieldName: string;
  databaseFieldLabel: string;
  databaseFieldType: string;
  databaseFieldRequired: string;
  databaseFieldOptional: string;
  databaseFieldDefaultValue: string;
  databaseFieldDescription: string;
  databaseFieldUnique: string;
  databaseFieldIndexed: string;
  databaseFieldLength: string;
  databaseFieldMin: string;
  databaseFieldMax: string;
  databaseMoveUp: string;
  databaseMoveDown: string;
  databaseSchemaPreview: string;
  databaseValidationFailed: string;
  createKnowledgeHint: string;
  searchPlaceholder: string;
  resourceType: string;
  resourceStatus: string;
  allTypes: string;
  allStatus: string;
  listEmpty: string;
  noDescription: string;
  knowledgeBase: string;
  documents: string;
  chunks: string;
  updatedAt: string;
  actions: string;
  open: string;
  openCanvas: string;
  openWorkflow: string;
  openPublish: string;
  publish: string;
  downloadTemplate: string;
  delete: string;
  resegment: string;
  upload: string;
  create: string;
  save: string;
  cancel: string;
  edit: string;
  backToLibrary: string;
  retrievalTest: string;
  retrievalTestHint: string;
  retrievalQueryPlaceholder: string;
  runTest: string;
  noTestResult: string;
  hitOffsetLabel: string;
  hitTagsLabel: string;
  rowIndexLabel: string;
  columnHeadersLabel: string;
  imagePreviewLabel: string;
  tablePreviewLabel: string;
  /* ---------- v5 §32-44 知识库专题扩展 ---------- */
  detailTabOverview: string;
  detailTabDocuments: string;
  detailTabSlices: string;
  detailTabRetrieval: string;
  detailTabBindings: string;
  detailTabJobs: string;
  detailTabPermissions: string;
  detailTabVersions: string;
  detailTabAlertsCount: string;
  wizardChooseKind: string;
  wizardKindText: string;
  wizardKindTable: string;
  wizardKindImage: string;
  wizardKindTextDesc: string;
  wizardKindTableDesc: string;
  wizardKindImageDesc: string;
  wizardBasicInfo: string;
  wizardConfigureProfile: string;
  wizardSummary: string;
  wizardName: string;
  wizardDescription: string;
  wizardTagsLabel: string;
  wizardTagsHint: string;
  wizardChunkSize: string;
  wizardChunkOverlap: string;
  wizardChunkSeparators: string;
  wizardTopK: string;
  wizardEnableRerank: string;
  wizardEnableHybrid: string;
  wizardEnableQueryRewrite: string;
  wizardProviderLabel: string;
  wizardProviderBuiltin: string;
  wizardProviderQdrant: string;
  wizardProviderExternal: string;
  wizardNext: string;
  wizardBack: string;
  wizardFinish: string;
  wizardCreateSuccess: string;
  wizardValidationName: string;
  jobStatusQueued: string;
  jobStatusRunning: string;
  jobStatusSucceeded: string;
  jobStatusFailed: string;
  jobStatusRetrying: string;
  jobStatusDeadLetter: string;
  jobStatusCanceled: string;
  jobTypeParse: string;
  jobTypeIndex: string;
  jobTypeRebuild: string;
  jobTypeGc: string;
  jobAttempts: string;
  jobEnqueuedAt: string;
  jobFinishedAt: string;
  jobLogsTitle: string;
  jobActionRetry: string;
  jobActionCancel: string;
  jobActionRebuildIndex: string;
  jobsEmpty: string;
  jobsTitle: string;
  jobsSubtitle: string;
  bindingsTitle: string;
  bindingsSubtitle: string;
  bindingsAddTitle: string;
  bindingsCallerType: string;
  bindingsCallerId: string;
  bindingsCallerName: string;
  bindingsActionRemove: string;
  bindingsCallerAgent: string;
  bindingsCallerApp: string;
  bindingsCallerWorkflow: string;
  bindingsCallerChatflow: string;
  bindingsEmpty: string;
  bindingsBlockingDelete: string;
  permissionsTitle: string;
  permissionsSubtitle: string;
  permissionsAddTitle: string;
  permissionsScope: string;
  permissionsSubject: string;
  permissionsActions: string;
  permissionsScopeSpace: string;
  permissionsScopeProject: string;
  permissionsScopeKb: string;
  permissionsScopeDocument: string;
  permissionsActionView: string;
  permissionsActionEdit: string;
  permissionsActionDelete: string;
  permissionsActionPublish: string;
  permissionsActionManage: string;
  permissionsActionRetrieve: string;
  permissionsRevoke: string;
  permissionsEmpty: string;
  versionsTitle: string;
  versionsSubtitle: string;
  versionsCreateTitle: string;
  versionsLabelPlaceholder: string;
  versionsNotePlaceholder: string;
  versionsRelease: string;
  versionsRollback: string;
  versionsDiff: string;
  versionsStatusDraft: string;
  versionsStatusReleased: string;
  versionsStatusArchived: string;
  versionsEmpty: string;
  versionsDiffTitle: string;
  versionsCreateSuccess: string;
  versionsRolledBack: string;
  retrievalDebugTitle: string;
  retrievalDebugHint: string;
  retrievalCallerType: string;
  retrievalCallerStudio: string;
  retrievalCallerAgent: string;
  retrievalCallerWorkflow: string;
  retrievalCallerApp: string;
  retrievalCallerChatflow: string;
  retrievalEnableDebug: string;
  retrievalRawQuery: string;
  retrievalRewrittenQuery: string;
  retrievalCandidates: string;
  retrievalReranked: string;
  retrievalFinalContext: string;
  retrievalLatency: string;
  retrievalLogsTitle: string;
  retrievalLogsEmpty: string;
  retrievalLogTraceId: string;
  retrievalLogCreatedAt: string;
  retrievalProfileTitle: string;
  retrievalProfileTopK: string;
  retrievalProfileMinScore: string;
  retrievalWeightLabel: string;
  retrievalSourceLabel: string;
  retrievalSourceVector: string;
  retrievalSourceBm25: string;
  retrievalSourceTable: string;
  retrievalSourceImage: string;
  slicesTabTextHeader: string;
  slicesTabTableHeader: string;
  slicesTabImageHeader: string;
  slicesEmpty: string;
  chunkingProfileTitle: string;
  chunkingProfileSave: string;
  chunkingProfileMode: string;
  chunkingProfileModeFixed: string;
  chunkingProfileModeSemantic: string;
  chunkingProfileModeTableRow: string;
  chunkingProfileModeImageItem: string;
  chunkingProfileSize: string;
  chunkingProfileOverlap: string;
  chunkingProfileSeparators: string;
  chunkingProfileIndexColumns: string;
  retrievalProfileSave: string;
  rebuildIndex: string;
  rebuildIndexHint: string;
  rebuildIndexConfirm: string;
  imageItemAnnotationCaption: string;
  imageItemAnnotationOcr: string;
  imageItemAnnotationTag: string;
  imageItemAnnotationVlm: string;
  jobsCenterTitle: string;
  jobsCenterSubtitle: string;
  providerCenterTitle: string;
  providerCenterSubtitle: string;
  providerRoleUpload: string;
  providerRoleStorage: string;
  providerRoleVector: string;
  providerRoleEmbedding: string;
  providerRoleGeneration: string;
  providerStatusActive: string;
  providerStatusDegraded: string;
  providerStatusInactive: string;
  resourcePickerTitle: string;
  resourcePickerEmpty: string;
  resourcePickerSelect: string;
  parsingFormParsingType: string;
  parsingFormParsingTypeQuick: string;
  parsingFormParsingTypePrecise: string;
  parsingFormExtractImage: string;
  parsingFormExtractTable: string;
  parsingFormImageOcr: string;
  parsingFormFilterPages: string;
  parsingFormSheetId: string;
  parsingFormHeaderLine: string;
  parsingFormDataStartLine: string;
  parsingFormRowsCount: string;
  parsingFormCaptionType: string;
  parsingFormCaptionAuto: string;
  parsingFormCaptionManual: string;
  parsingFormCaptionFilename: string;
  parsingCompareTitle: string;
  parsingCompareLeft: string;
  parsingCompareRight: string;
  parsingCompareRun: string;
  uploadTitle: string;
  uploadSubtitle: string;
  uploadSelectFile: string;
  uploadSubmit: string;
  uploadEmpty: string;
  uploadProgress: string;
  uploadDone: string;
  uploadFailed: string;
  stepType: string;
  stepFile: string;
  stepProcessing: string;
  stepComplete: string;
  documentList: string;
  chunkList: string;
  selectDocumentHint: string;
  addChunk: string;
  editChunk: string;
  chunkContent: string;
  chunkStart: string;
  chunkEnd: string;
  summary: string;
  detailEmpty: string;
  processing: string;
  disabled: string;
  ready: string;
  failed: string;
  uploadProcessingHint: string;
  uploadTagsLabel: string;
  uploadTagsPlaceholder: string;
  uploadImageMetaLabel: string;
  uploadImageMetaPlaceholder: string;
  uploadTagsInvalid: string;
  createTextKbHint: string;
  createTableKbHint: string;
  createImageKbHint: string;
  typeLabels: Record<KnowledgeBaseType, string>;
  workflowModeLabels: {
    workflow: string;
    chatflow: string;
  };
  resourceLabels: Record<ResourceType, string>;
  statusLabels: Record<string, string>;
  docStatusLabels: Record<DocumentProcessingStatus, string>;
  /* === M3：从 8 个组件迁入的散落 CJK === */
  close: string;
  bindingsRetrievalProfileOverrideHint: string;
  permissionsSelectDocumentRequired: string;
  permissionsSelectDocumentPlaceholder: string;
  retrievalProfileCustomRerankerPlaceholder: string;
  commonRemove: string;
  commonAddFilter: string;
  commonClear: string;
  tablePreviewFilterByColumnPlaceholder: string;
  tablePreviewKeywordsPlaceholder: string;
  databaseValidationFieldRequired: string;
  databaseValidationFieldDuplicate: string;
  databaseValidationMustBePositive: string;
  databaseValidationMustBeNumber: string;
  databaseValidationMinGreaterThanMax: string;
  databaseValidationNotApplicable: string;
  imageAnnotationFilterAll: string;
  imageAnnotationKeywordPlaceholder: string;
  appNoMainWorkflowWarning: string;
  databaseBotIdPlaceholder: string;
};

const zhCN: CopyTree = {
  title: "资源库",
  createResource: "创建资源",
  createKnowledge: "新建知识库",
  createPlugin: "新建插件",
  createDatabase: "新建数据库",
  pluginBasicInfo: "基础信息",
  pluginSourceAndAuth: "来源与认证",
  pluginAdvancedConfig: "高级 JSON 配置",
  pluginOpenApiGuide: "OpenAPI 导入引导",
  pluginOpenApiHint: "选择 OpenApiImport 后，优先粘贴完整 OpenAPI 文档，再按需补充工具和鉴权配置。",
  pluginPrefillOpenApi: "填入示例 OpenAPI",
  pluginPreviewSummary: "预解析摘要",
  pluginPreviewOperations: "接口操作数",
  pluginPreviewSchemas: "Schema 字段数",
  pluginPreviewNext: "预解析并预览",
  pluginPreviewBack: "返回编辑",
  pluginPreviewEmpty: "当前 OpenAPI 内容无法解析出任何接口。",
  pluginValidateOpenApi: "OpenAPI JSON 不合法，请先修正后再预解析。",
  databaseBasicInfo: "基础信息",
  databaseSchemaMode: "Schema 编辑模式",
  databaseSchemaStructured: "结构编辑",
  databaseSchemaRaw: "原始 JSON",
  databaseAddColumn: "新增列",
  databaseDeleteColumn: "删除",
  databaseFieldName: "字段名",
  databaseFieldLabel: "显示名",
  databaseFieldType: "字段类型",
  databaseFieldRequired: "必填",
  databaseFieldOptional: "可选",
  databaseFieldDefaultValue: "默认值",
  databaseFieldDescription: "字段说明",
  databaseFieldUnique: "唯一",
  databaseFieldIndexed: "索引",
  databaseFieldLength: "长度",
  databaseFieldMin: "最小值",
  databaseFieldMax: "最大值",
  databaseMoveUp: "上移",
  databaseMoveDown: "下移",
  databaseSchemaPreview: "Schema 预览",
  databaseValidationFailed: "请先修正字段规则后再创建数据库。",
  createKnowledgeHint: "统一按 Coze 风格维护文本、表格、图片知识。",
  searchPlaceholder: "搜索资源名称",
  resourceType: "资源类型",
  resourceStatus: "状态",
  allTypes: "全部类型",
  allStatus: "全部状态",
  listEmpty: "当前筛选下没有资源。",
  noDescription: "暂无描述",
  knowledgeBase: "知识库",
  documents: "文档数",
  chunks: "分片数",
  updatedAt: "最近更新",
  actions: "操作",
  open: "打开",
  openCanvas: "打开画布",
  openWorkflow: "打开工作流",
  openPublish: "进入发布页",
  publish: "发布插件",
  downloadTemplate: "下载模板",
  delete: "删除",
  resegment: "重分段",
  upload: "上传文档",
  create: "创建",
  save: "保存",
  cancel: "取消",
  edit: "编辑",
  backToLibrary: "返回资源库",
  retrievalTest: "检索测试",
  retrievalTestHint: "直接调用知识库检索链路验证召回结果。",
  retrievalQueryPlaceholder: "输入测试问题，例如：平台有哪些安全能力？",
  runTest: "开始测试",
  noTestResult: "暂时还没有检索结果。",
  hitOffsetLabel: "命中位置",
  hitTagsLabel: "命中标签",
  rowIndexLabel: "行号",
  columnHeadersLabel: "列头",
  imagePreviewLabel: "图片预览",
  tablePreviewLabel: "表格预览",
  uploadTitle: "导入知识文档",
  uploadSubtitle: "按照 Coze 式四段流程维护知识导入。",
  uploadSelectFile: "选择文件",
  uploadSubmit: "开始导入",
  uploadEmpty: "请先选择至少一个文件。",
  uploadProgress: "处理中",
  uploadDone: "已完成",
  uploadFailed: "失败",
  stepType: "1. 选择知识类型",
  stepFile: "2. 上传或导入文件",
  stepProcessing: "3. 解析与分段",
  stepComplete: "4. 完成并返回详情",
  documentList: "文档列表",
  chunkList: "分片列表",
  selectDocumentHint: "请选择一个文档查看分片与处理状态。",
  addChunk: "新增分片",
  editChunk: "编辑分片",
  chunkContent: "内容",
  chunkStart: "起始偏移",
  chunkEnd: "结束偏移",
  summary: "摘要信息",
  detailEmpty: "没有找到该知识库。",
  processing: "处理中",
  disabled: "已停用",
  ready: "就绪",
  failed: "失败",
  uploadProcessingHint: "上传成功后会自动轮询处理状态，直到完成或失败。",
  uploadTagsLabel: "文档标签（可选）",
  uploadTagsPlaceholder: 'JSON 数组，例如 ["产品","FAQ"]',
  uploadImageMetaLabel: "图片标注元数据（可选）",
  uploadImageMetaPlaceholder: 'JSON 对象，例如 {"caption":"示意图","ocr":""}',
  uploadTagsInvalid: "标签需为合法 JSON 数组。",
  createTextKbHint: "适合长文档、手册等纯文本分段与向量检索。",
  createTableKbHint: "上传 CSV/TSV 等文本表格，将按行建分片并保留列头。",
  createImageKbHint: "仅支持 image/* 文件；可附带 JSON 标注元数据。",
  typeLabels: {
    0: "文本知识",
    1: "表格知识",
    2: "图片知识"
  },
  workflowModeLabels: {
    workflow: "标准工作流",
    chatflow: "对话流"
  },
  resourceLabels: {
    "agent": "Agent",
    "knowledge-base": "知识库",
    "workflow": "工作流",
    "plugin": "插件",
    "database": "数据库",
    "app": "应用",
    "prompt": "Prompt"
  },
  statusLabels: {
    all: "全部状态",
    ready: "就绪",
    processing: "处理中",
    disabled: "已停用",
    failed: "失败"
  },
  docStatusLabels: {
    0: "待处理",
    1: "处理中",
    2: "已完成",
    3: "失败"
  },
  detailTabOverview: "概览",
  detailTabDocuments: "文档",
  detailTabSlices: "切片",
  detailTabRetrieval: "检索",
  detailTabBindings: "绑定关系",
  detailTabJobs: "任务",
  detailTabPermissions: "权限",
  detailTabVersions: "版本",
  detailTabAlertsCount: "告警",
  wizardChooseKind: "选择知识库类型",
  wizardKindText: "文本知识",
  wizardKindTable: "表格知识",
  wizardKindImage: "图片知识",
  wizardKindTextDesc: "适合长文档、手册、FAQ：按字符或语义切分后入向量库。",
  wizardKindTableDesc: "上传 CSV/Excel：按行切分并保留列头，支持索引列加权检索。",
  wizardKindImageDesc: "图片标注 + OCR + Caption；适合巡检图集、警示牌、应急演练等。",
  wizardBasicInfo: "基本信息",
  wizardConfigureProfile: "切片与检索策略",
  wizardSummary: "确认创建",
  wizardName: "知识库名称",
  wizardDescription: "用途描述",
  wizardTagsLabel: "标签",
  wizardTagsHint: "用 Enter 录入；标签便于在资源中心快速过滤。",
  wizardChunkSize: "切片大小",
  wizardChunkOverlap: "切片重叠",
  wizardChunkSeparators: "分隔符",
  wizardTopK: "默认召回 TopK",
  wizardEnableRerank: "启用重排序",
  wizardEnableHybrid: "启用混合检索",
  wizardEnableQueryRewrite: "启用查询改写",
  wizardProviderLabel: "向量后端",
  wizardProviderBuiltin: "内置向量索引",
  wizardProviderQdrant: "Qdrant 向量库",
  wizardProviderExternal: "外部向量服务",
  wizardNext: "下一步",
  wizardBack: "上一步",
  wizardFinish: "完成创建",
  wizardCreateSuccess: "知识库创建成功",
  wizardValidationName: "请填写知识库名称",
  jobStatusQueued: "排队中",
  jobStatusRunning: "运行中",
  jobStatusSucceeded: "已完成",
  jobStatusFailed: "失败",
  jobStatusRetrying: "重试中",
  jobStatusDeadLetter: "死信",
  jobStatusCanceled: "已取消",
  jobTypeParse: "解析",
  jobTypeIndex: "索引",
  jobTypeRebuild: "重建索引",
  jobTypeGc: "回收",
  jobAttempts: "重试次数",
  jobEnqueuedAt: "入队时间",
  jobFinishedAt: "完成时间",
  jobLogsTitle: "运行日志",
  jobActionRetry: "重投",
  jobActionCancel: "取消",
  jobActionRebuildIndex: "全量重建索引",
  jobsEmpty: "暂无任务",
  jobsTitle: "任务列表",
  jobsSubtitle: "知识库的解析、切片、索引、重建与回收任务的状态机视图。",
  bindingsTitle: "绑定关系",
  bindingsSubtitle: "记录哪些 Agent / App / Workflow / Chatflow 正在引用本知识库；删除前必须先解除绑定。",
  bindingsAddTitle: "新增绑定",
  bindingsCallerType: "调用方类型",
  bindingsCallerId: "调用方 ID",
  bindingsCallerName: "调用方名称",
  bindingsActionRemove: "解除绑定",
  bindingsCallerAgent: "Agent",
  bindingsCallerApp: "应用",
  bindingsCallerWorkflow: "工作流",
  bindingsCallerChatflow: "对话流",
  bindingsEmpty: "尚无绑定关系",
  bindingsBlockingDelete: "存在绑定关系，无法删除知识库；请先解除以下绑定：",
  permissionsTitle: "权限管理",
  permissionsSubtitle: "四层权限：空间 / 项目 / 知识库 / 文档；继承空间默认值，可在更细粒度上覆盖。",
  permissionsAddTitle: "授权",
  permissionsScope: "授权范围",
  permissionsSubject: "授权对象",
  permissionsActions: "可执行动作",
  permissionsScopeSpace: "空间",
  permissionsScopeProject: "项目",
  permissionsScopeKb: "知识库",
  permissionsScopeDocument: "文档",
  permissionsActionView: "查看",
  permissionsActionEdit: "编辑",
  permissionsActionDelete: "删除",
  permissionsActionPublish: "发布",
  permissionsActionManage: "管理",
  permissionsActionRetrieve: "检索",
  permissionsRevoke: "撤销",
  permissionsEmpty: "暂无权限授予",
  versionsTitle: "版本治理",
  versionsSubtitle: "对知识库进行快照、发布与回退；回退仅恢复 Schema 元信息，已上传文件保持不变。",
  versionsCreateTitle: "新建快照",
  versionsLabelPlaceholder: "版本标签，例如 v1.2.0",
  versionsNotePlaceholder: "可选：本版本变更摘要",
  versionsRelease: "发布",
  versionsRollback: "回退到此版本",
  versionsDiff: "对比",
  versionsStatusDraft: "草稿",
  versionsStatusReleased: "已发布",
  versionsStatusArchived: "已归档",
  versionsEmpty: "尚无版本快照",
  versionsDiffTitle: "版本对比",
  versionsCreateSuccess: "快照已创建",
  versionsRolledBack: "已回退到选定版本",
  retrievalDebugTitle: "检索调试",
  retrievalDebugHint: "实时调用知识库检索链路，查看改写后的 Query、命中切片、分数与最终注入上下文。",
  retrievalCallerType: "调用方场景",
  retrievalCallerStudio: "Studio 调试",
  retrievalCallerAgent: "Agent 对话",
  retrievalCallerWorkflow: "工作流执行",
  retrievalCallerApp: "应用页面",
  retrievalCallerChatflow: "对话流",
  retrievalEnableDebug: "返回完整 RetrievalLog（debug=true）",
  retrievalRawQuery: "原始查询",
  retrievalRewrittenQuery: "改写后查询",
  retrievalCandidates: "原始候选",
  retrievalReranked: "重排后命中",
  retrievalFinalContext: "注入大模型的上下文",
  retrievalLatency: "耗时（ms）",
  retrievalLogsTitle: "检索日志",
  retrievalLogsEmpty: "暂未记录检索调用",
  retrievalLogTraceId: "TraceId",
  retrievalLogCreatedAt: "时间",
  retrievalProfileTitle: "检索策略（默认）",
  retrievalProfileTopK: "TopK",
  retrievalProfileMinScore: "最低分数",
  retrievalWeightLabel: "混合检索权重",
  retrievalSourceLabel: "命中来源",
  retrievalSourceVector: "向量",
  retrievalSourceBm25: "关键词",
  retrievalSourceTable: "表格",
  retrievalSourceImage: "图片",
  slicesTabTextHeader: "文本切片",
  slicesTabTableHeader: "表格行视图",
  slicesTabImageHeader: "图片项目视图",
  slicesEmpty: "暂无切片，等待解析与索引完成",
  chunkingProfileTitle: "切片策略",
  chunkingProfileSave: "保存切片策略",
  chunkingProfileMode: "切片模式",
  chunkingProfileModeFixed: "固定窗口",
  chunkingProfileModeSemantic: "语义分段",
  chunkingProfileModeTableRow: "按表格行",
  chunkingProfileModeImageItem: "按图片项",
  chunkingProfileSize: "切片大小（字符）",
  chunkingProfileOverlap: "重叠（字符）",
  chunkingProfileSeparators: "分隔符（逗号分隔）",
  chunkingProfileIndexColumns: "索引列（仅表格 KB）",
  retrievalProfileSave: "保存检索策略",
  rebuildIndex: "全量重建索引",
  rebuildIndexHint: "执行全量重建会删除当前向量并重新嵌入；通常在更换 embedding 模型或修改切片策略后执行。",
  rebuildIndexConfirm: "确认重建索引？过程中检索质量可能波动。",
  imageItemAnnotationCaption: "标题",
  imageItemAnnotationOcr: "OCR",
  imageItemAnnotationTag: "标签",
  imageItemAnnotationVlm: "VLM 描述",
  jobsCenterTitle: "任务中心",
  jobsCenterSubtitle: "跨知识库统一查看解析、索引、重建、回收任务，支持死信重投与调用链追踪。",
  providerCenterTitle: "Provider 配置中心",
  providerCenterSubtitle: "上传 / 存储 / 向量 / 嵌入 / 生成五类 Provider 的连接信息（前端阶段只读，后端补齐后开放写入）。",
  providerRoleUpload: "上传",
  providerRoleStorage: "对象存储",
  providerRoleVector: "向量索引",
  providerRoleEmbedding: "嵌入模型",
  providerRoleGeneration: "生成模型",
  providerStatusActive: "正常",
  providerStatusDegraded: "降级",
  providerStatusInactive: "未启用",
  resourcePickerTitle: "选择知识库",
  resourcePickerEmpty: "没有可选知识库",
  resourcePickerSelect: "确定",
  parsingFormParsingType: "解析模式",
  parsingFormParsingTypeQuick: "快速解析（仅文本）",
  parsingFormParsingTypePrecise: "精准解析（图文表）",
  parsingFormExtractImage: "提取图片",
  parsingFormExtractTable: "提取表格",
  parsingFormImageOcr: "图片 OCR",
  parsingFormFilterPages: "页码筛选（如 1-5,8,12-）",
  parsingFormSheetId: "Sheet ID",
  parsingFormHeaderLine: "表头行号",
  parsingFormDataStartLine: "数据起始行号",
  parsingFormRowsCount: "最大行数（0 表示不限）",
  parsingFormCaptionType: "图片标题来源",
  parsingFormCaptionAuto: "自动 VLM 生成",
  parsingFormCaptionManual: "手动维护",
  parsingFormCaptionFilename: "使用文件名",
  parsingCompareTitle: "解析策略对比",
  parsingCompareLeft: "策略 A",
  parsingCompareRight: "策略 B",
  parsingCompareRun: "并排重跑解析",
  close: "关闭",
  bindingsRetrievalProfileOverrideHint: "此 RetrievalProfile 将作为新增绑定时的 retrievalProfileOverride 写入；后端检索时优先使用绑定上的 override。",
  permissionsSelectDocumentRequired: "请选择 documentId",
  permissionsSelectDocumentPlaceholder: "选择目标文档",
  retrievalProfileCustomRerankerPlaceholder: "自定义 reranker model id",
  commonRemove: "移除",
  commonAddFilter: "+ 添加 filter",
  commonClear: "清除",
  tablePreviewFilterByColumnPlaceholder: "按列筛选",
  tablePreviewKeywordsPlaceholder: "关键词",
  databaseValidationFieldRequired: "{field} 不能为空",
  databaseValidationFieldDuplicate: "{field} 重复: {name}",
  databaseValidationMustBePositive: "{name}: {field} 必须是正数",
  databaseValidationMustBeNumber: "{name}: {field} 必须是数字",
  databaseValidationMinGreaterThanMax: "{name}: {min} 不能大于 {max}",
  databaseValidationNotApplicable: "{name}: {field} 不适用于 {type}",
  imageAnnotationFilterAll: "全部标注",
  imageAnnotationKeywordPlaceholder: "标注关键词",
  appNoMainWorkflowWarning: "当前应用还没有关联主工作流。",
  databaseBotIdPlaceholder: "botId（可选）"
};

const enUS: CopyTree = {
  title: "Library",
  createResource: "Create Resource",
  createKnowledge: "Create Knowledge Base",
  createPlugin: "Create Plugin",
  createDatabase: "Create Database",
  pluginBasicInfo: "Basic Info",
  pluginSourceAndAuth: "Source & Auth",
  pluginAdvancedConfig: "Advanced JSON Config",
  pluginOpenApiGuide: "OpenAPI Import Guide",
  pluginOpenApiHint: "When OpenApiImport is selected, paste a full OpenAPI document first, then refine tool and auth configs.",
  pluginPrefillOpenApi: "Prefill OpenAPI Sample",
  pluginPreviewSummary: "Parse Summary",
  pluginPreviewOperations: "Operations",
  pluginPreviewSchemas: "Schema Fields",
  pluginPreviewNext: "Parse & Preview",
  pluginPreviewBack: "Back to Edit",
  pluginPreviewEmpty: "No API operations could be parsed from the current OpenAPI content.",
  pluginValidateOpenApi: "OpenAPI JSON is invalid. Fix it before previewing.",
  databaseBasicInfo: "Basic Info",
  databaseSchemaMode: "Schema Editor Mode",
  databaseSchemaStructured: "Structured",
  databaseSchemaRaw: "Raw JSON",
  databaseAddColumn: "Add Column",
  databaseDeleteColumn: "Delete",
  databaseFieldName: "Field Name",
  databaseFieldLabel: "Display Label",
  databaseFieldType: "Field Type",
  databaseFieldRequired: "Required",
  databaseFieldOptional: "Optional",
  databaseFieldDefaultValue: "Default Value",
  databaseFieldDescription: "Description",
  databaseFieldUnique: "Unique",
  databaseFieldIndexed: "Indexed",
  databaseFieldLength: "Length",
  databaseFieldMin: "Min",
  databaseFieldMax: "Max",
  databaseMoveUp: "Move Up",
  databaseMoveDown: "Move Down",
  databaseSchemaPreview: "Schema Preview",
  databaseValidationFailed: "Please fix field rules before creating the database.",
  createKnowledgeHint: "Manage text, table, and image knowledge in a Coze-style flow.",
  searchPlaceholder: "Search resources",
  resourceType: "Type",
  resourceStatus: "Status",
  allTypes: "All Types",
  allStatus: "All Status",
  listEmpty: "No resources match the current filters.",
  noDescription: "No description",
  knowledgeBase: "Knowledge Base",
  documents: "Documents",
  chunks: "Chunks",
  updatedAt: "Updated",
  actions: "Actions",
  open: "Open",
  openCanvas: "Open Canvas",
  openWorkflow: "Open Workflow",
  openPublish: "Open Publish",
  publish: "Publish Plugin",
  downloadTemplate: "Download Template",
  delete: "Delete",
  resegment: "Resegment",
  upload: "Upload",
  create: "Create",
  save: "Save",
  cancel: "Cancel",
  edit: "Edit",
  backToLibrary: "Back to Library",
  retrievalTest: "Retrieval Test",
  retrievalTestHint: "Run the real knowledge retrieval path and inspect hits.",
  retrievalQueryPlaceholder: "Type a test query, e.g. What security capabilities does the platform provide?",
  runTest: "Run Test",
  noTestResult: "No retrieval result yet.",
  hitOffsetLabel: "Match Range",
  hitTagsLabel: "Tags",
  rowIndexLabel: "Row #",
  columnHeadersLabel: "Headers",
  imagePreviewLabel: "Image Preview",
  tablePreviewLabel: "Table Preview",
  uploadTitle: "Import Knowledge Files",
  uploadSubtitle: "Maintain knowledge ingestion with a four-step Coze-like flow.",
  uploadSelectFile: "Choose Files",
  uploadSubmit: "Start Import",
  uploadEmpty: "Please choose at least one file first.",
  uploadProgress: "Processing",
  uploadDone: "Done",
  uploadFailed: "Failed",
  stepType: "1. Select Knowledge Type",
  stepFile: "2. Upload or Import Files",
  stepProcessing: "3. Parse and Chunk",
  stepComplete: "4. Finish and Return",
  documentList: "Documents",
  chunkList: "Chunks",
  selectDocumentHint: "Choose a document to inspect chunks and processing state.",
  addChunk: "Add Chunk",
  editChunk: "Edit Chunk",
  chunkContent: "Content",
  chunkStart: "Start Offset",
  chunkEnd: "End Offset",
  summary: "Summary",
  detailEmpty: "Knowledge base not found.",
  processing: "Processing",
  disabled: "Disabled",
  ready: "Ready",
  failed: "Failed",
  uploadProcessingHint: "After upload, processing status is polled automatically until completion or failure.",
  uploadTagsLabel: "Document tags (optional)",
  uploadTagsPlaceholder: 'JSON array, e.g. ["product","faq"]',
  uploadImageMetaLabel: "Image metadata (optional)",
  uploadImageMetaPlaceholder: 'JSON object, e.g. {"caption":"diagram","ocr":""}',
  uploadTagsInvalid: "Tags must be a valid JSON array.",
  createTextKbHint: "Best for manuals and long text with chunking + vector search.",
  createTableKbHint: "Upload CSV/TSV-like files; each row becomes a chunk with headers.",
  createImageKbHint: "Only image/* files; optional JSON annotation metadata.",
  typeLabels: {
    0: "Text Knowledge",
    1: "Table Knowledge",
    2: "Image Knowledge"
  },
  workflowModeLabels: {
    workflow: "Workflow",
    chatflow: "Chatflow"
  },
  resourceLabels: {
    "agent": "Agent",
    "knowledge-base": "Knowledge Base",
    "workflow": "Workflow",
    "plugin": "Plugin",
    "database": "Database",
    "app": "App",
    "prompt": "Prompt"
  },
  statusLabels: {
    all: "All Status",
    ready: "Ready",
    processing: "Processing",
    disabled: "Disabled",
    failed: "Failed"
  },
  docStatusLabels: {
    0: "Pending",
    1: "Processing",
    2: "Completed",
    3: "Failed"
  },
  detailTabOverview: "Overview",
  detailTabDocuments: "Documents",
  detailTabSlices: "Slices",
  detailTabRetrieval: "Retrieval",
  detailTabBindings: "Bindings",
  detailTabJobs: "Jobs",
  detailTabPermissions: "Permissions",
  detailTabVersions: "Versions",
  detailTabAlertsCount: "alerts",
  wizardChooseKind: "Choose knowledge type",
  wizardKindText: "Text",
  wizardKindTable: "Table",
  wizardKindImage: "Image",
  wizardKindTextDesc: "Manuals, FAQs, long-form text. Chunked + vectorized.",
  wizardKindTableDesc: "CSV/Excel ingestion. Row-as-chunk with header preserved.",
  wizardKindImageDesc: "OCR + caption + tags. Inspections, signs, drills.",
  wizardBasicInfo: "Basic info",
  wizardConfigureProfile: "Chunking & retrieval",
  wizardSummary: "Confirm",
  wizardName: "Knowledge base name",
  wizardDescription: "Purpose / description",
  wizardTagsLabel: "Tags",
  wizardTagsHint: "Press Enter to add. Tags help filter resources.",
  wizardChunkSize: "Chunk size",
  wizardChunkOverlap: "Chunk overlap",
  wizardChunkSeparators: "Separators",
  wizardTopK: "Default TopK",
  wizardEnableRerank: "Enable rerank",
  wizardEnableHybrid: "Enable hybrid retrieval",
  wizardEnableQueryRewrite: "Enable query rewrite",
  wizardProviderLabel: "Vector backend",
  wizardProviderBuiltin: "Built-in vector index",
  wizardProviderQdrant: "Qdrant",
  wizardProviderExternal: "External vector service",
  wizardNext: "Next",
  wizardBack: "Back",
  wizardFinish: "Create",
  wizardCreateSuccess: "Knowledge base created",
  wizardValidationName: "Please enter a knowledge base name",
  jobStatusQueued: "Queued",
  jobStatusRunning: "Running",
  jobStatusSucceeded: "Succeeded",
  jobStatusFailed: "Failed",
  jobStatusRetrying: "Retrying",
  jobStatusDeadLetter: "Dead Letter",
  jobStatusCanceled: "Canceled",
  jobTypeParse: "Parse",
  jobTypeIndex: "Index",
  jobTypeRebuild: "Rebuild",
  jobTypeGc: "GC",
  jobAttempts: "Attempts",
  jobEnqueuedAt: "Enqueued",
  jobFinishedAt: "Finished",
  jobLogsTitle: "Logs",
  jobActionRetry: "Retry",
  jobActionCancel: "Cancel",
  jobActionRebuildIndex: "Rebuild index",
  jobsEmpty: "No jobs",
  jobsTitle: "Jobs",
  jobsSubtitle: "State machine view of parse / index / rebuild / GC jobs.",
  bindingsTitle: "Bindings",
  bindingsSubtitle: "Agents / Apps / Workflows / Chatflows that reference this KB. Must unbind before deletion.",
  bindingsAddTitle: "Add binding",
  bindingsCallerType: "Caller type",
  bindingsCallerId: "Caller ID",
  bindingsCallerName: "Caller name",
  bindingsActionRemove: "Unbind",
  bindingsCallerAgent: "Agent",
  bindingsCallerApp: "App",
  bindingsCallerWorkflow: "Workflow",
  bindingsCallerChatflow: "Chatflow",
  bindingsEmpty: "No bindings",
  bindingsBlockingDelete: "Cannot delete: the following bindings still reference this KB:",
  permissionsTitle: "Permissions",
  permissionsSubtitle: "Four-tier permissions: space / project / KB / document. Inherits from space, finer scopes override.",
  permissionsAddTitle: "Grant",
  permissionsScope: "Scope",
  permissionsSubject: "Subject",
  permissionsActions: "Actions",
  permissionsScopeSpace: "Space",
  permissionsScopeProject: "Project",
  permissionsScopeKb: "KB",
  permissionsScopeDocument: "Document",
  permissionsActionView: "View",
  permissionsActionEdit: "Edit",
  permissionsActionDelete: "Delete",
  permissionsActionPublish: "Publish",
  permissionsActionManage: "Manage",
  permissionsActionRetrieve: "Retrieve",
  permissionsRevoke: "Revoke",
  permissionsEmpty: "No permissions granted",
  versionsTitle: "Versions",
  versionsSubtitle: "Snapshot, release, rollback. Rollback restores schema metadata; uploaded files remain untouched.",
  versionsCreateTitle: "Create snapshot",
  versionsLabelPlaceholder: "Version label e.g. v1.2.0",
  versionsNotePlaceholder: "Optional change note",
  versionsRelease: "Release",
  versionsRollback: "Rollback",
  versionsDiff: "Diff",
  versionsStatusDraft: "Draft",
  versionsStatusReleased: "Released",
  versionsStatusArchived: "Archived",
  versionsEmpty: "No version snapshots yet",
  versionsDiffTitle: "Version diff",
  versionsCreateSuccess: "Snapshot created",
  versionsRolledBack: "Rolled back to selected version",
  retrievalDebugTitle: "Retrieval debug",
  retrievalDebugHint: "Run the real retrieval pipeline. Inspect rewritten query, hits, scores and final injected context.",
  retrievalCallerType: "Caller scenario",
  retrievalCallerStudio: "Studio debug",
  retrievalCallerAgent: "Agent chat",
  retrievalCallerWorkflow: "Workflow run",
  retrievalCallerApp: "App page",
  retrievalCallerChatflow: "Chatflow",
  retrievalEnableDebug: "Return full RetrievalLog (debug=true)",
  retrievalRawQuery: "Raw query",
  retrievalRewrittenQuery: "Rewritten query",
  retrievalCandidates: "Candidates",
  retrievalReranked: "Reranked hits",
  retrievalFinalContext: "Final injected context",
  retrievalLatency: "Latency (ms)",
  retrievalLogsTitle: "Retrieval logs",
  retrievalLogsEmpty: "No retrieval logs recorded yet",
  retrievalLogTraceId: "TraceId",
  retrievalLogCreatedAt: "Time",
  retrievalProfileTitle: "Default retrieval profile",
  retrievalProfileTopK: "TopK",
  retrievalProfileMinScore: "Min score",
  retrievalWeightLabel: "Hybrid weights",
  retrievalSourceLabel: "Source",
  retrievalSourceVector: "Vector",
  retrievalSourceBm25: "BM25",
  retrievalSourceTable: "Table",
  retrievalSourceImage: "Image",
  slicesTabTextHeader: "Text slices",
  slicesTabTableHeader: "Table rows",
  slicesTabImageHeader: "Image items",
  slicesEmpty: "No slices yet — wait for parse and index jobs",
  chunkingProfileTitle: "Chunking profile",
  chunkingProfileSave: "Save chunking profile",
  chunkingProfileMode: "Mode",
  chunkingProfileModeFixed: "Fixed window",
  chunkingProfileModeSemantic: "Semantic",
  chunkingProfileModeTableRow: "Table row",
  chunkingProfileModeImageItem: "Image item",
  chunkingProfileSize: "Chunk size (chars)",
  chunkingProfileOverlap: "Overlap (chars)",
  chunkingProfileSeparators: "Separators (comma)",
  chunkingProfileIndexColumns: "Index columns (table only)",
  retrievalProfileSave: "Save retrieval profile",
  rebuildIndex: "Rebuild index",
  rebuildIndexHint: "Rebuild deletes vectors and re-embeds. Run after switching embedding model or chunking strategy.",
  rebuildIndexConfirm: "Confirm rebuild? Retrieval quality may fluctuate during the run.",
  imageItemAnnotationCaption: "Caption",
  imageItemAnnotationOcr: "OCR",
  imageItemAnnotationTag: "Tag",
  imageItemAnnotationVlm: "VLM",
  jobsCenterTitle: "Jobs center",
  jobsCenterSubtitle: "All KBs together — parse / index / rebuild / GC. Supports dead-letter retry & trace lookup.",
  providerCenterTitle: "Provider configs",
  providerCenterSubtitle: "Upload / storage / vector / embedding / generation providers (read-only in mock phase).",
  providerRoleUpload: "Upload",
  providerRoleStorage: "Storage",
  providerRoleVector: "Vector",
  providerRoleEmbedding: "Embedding",
  providerRoleGeneration: "Generation",
  providerStatusActive: "Active",
  providerStatusDegraded: "Degraded",
  providerStatusInactive: "Inactive",
  resourcePickerTitle: "Pick knowledge base",
  resourcePickerEmpty: "No knowledge bases available",
  resourcePickerSelect: "Confirm",
  parsingFormParsingType: "Parsing mode",
  parsingFormParsingTypeQuick: "Quick (text only)",
  parsingFormParsingTypePrecise: "Precise (text/image/table)",
  parsingFormExtractImage: "Extract images",
  parsingFormExtractTable: "Extract tables",
  parsingFormImageOcr: "OCR images",
  parsingFormFilterPages: "Page filter (e.g. 1-5,8,12-)",
  parsingFormSheetId: "Sheet ID",
  parsingFormHeaderLine: "Header line",
  parsingFormDataStartLine: "Data start line",
  parsingFormRowsCount: "Max rows (0 = unlimited)",
  parsingFormCaptionType: "Caption source",
  parsingFormCaptionAuto: "Auto VLM",
  parsingFormCaptionManual: "Manual",
  parsingFormCaptionFilename: "Filename",
  parsingCompareTitle: "Parsing strategy compare",
  parsingCompareLeft: "Strategy A",
  parsingCompareRight: "Strategy B",
  parsingCompareRun: "Run side-by-side parse",
  close: "Close",
  bindingsRetrievalProfileOverrideHint: "This RetrievalProfile is written as retrievalProfileOverride on new bindings; the backend prefers the binding override on retrieval.",
  permissionsSelectDocumentRequired: "Please select a documentId",
  permissionsSelectDocumentPlaceholder: "Select target document",
  retrievalProfileCustomRerankerPlaceholder: "Custom reranker model id",
  commonRemove: "Remove",
  commonAddFilter: "+ Add filter",
  commonClear: "Clear",
  tablePreviewFilterByColumnPlaceholder: "Filter by column",
  tablePreviewKeywordsPlaceholder: "Keywords",
  databaseValidationFieldRequired: "{field} is required",
  databaseValidationFieldDuplicate: "{field} duplicate: {name}",
  databaseValidationMustBePositive: "{name}: {field} must be positive",
  databaseValidationMustBeNumber: "{name}: {field} must be a number",
  databaseValidationMinGreaterThanMax: "{name}: {min} cannot be greater than {max}",
  databaseValidationNotApplicable: "{name}: {field} is not applicable for {type}",
  imageAnnotationFilterAll: "All annotations",
  imageAnnotationKeywordPlaceholder: "Annotation keyword",
  appNoMainWorkflowWarning: "This app has no main workflow associated.",
  databaseBotIdPlaceholder: "botId (optional)"
};

export function getLibraryCopy(locale: SupportedLocale): CopyTree {
  return locale === "en-US" ? enUS : zhCN;
}
