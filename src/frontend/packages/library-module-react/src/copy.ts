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
  }
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
  }
};

export function getLibraryCopy(locale: SupportedLocale): CopyTree {
  return locale === "en-US" ? enUS : zhCN;
}
