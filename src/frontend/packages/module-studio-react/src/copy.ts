import type { StudioLocale } from "./types";

/**
 * 包级 i18n 字典：与 library-module-react/copy.ts 同模式。
 * 所有用户可见文案集中维护，组件通过 getStudioCopy(locale) 拿到对应语言版本。
 *
 * 子节点按"功能域"分组，避免单个超大 flat object，便于按里程碑（M4a/M4b/M4c）增量补充。
 */
export interface StudioCopy {
  readonly common: {
    readonly close: string;
    readonly cancel: string;
    readonly retry: string;
    readonly loadDataFailed: string;
    readonly unknownError: string;
    readonly emptyData: string;
  };
  readonly status: {
    readonly unknown: string;
    readonly published: string;
    readonly draft: string;
    readonly outdated: string;
  };
  readonly appBuilder: {
    readonly duplicateVariableKey: string;
    readonly missingVariableKey: string;
    readonly workflowBindHeader: string;
    readonly workflowBindHint: string;
    readonly workflowBindPlaceholder: string;
    readonly workflowBindOpenInEditor: string;
    readonly workflowBindNoDescription: string;
  };
  readonly assistant: {
    readonly configNavTitle: string;
    readonly configNavSubtitle: string;
    readonly configNavLoadingResources: string;
    readonly versionLoading: string;
    readonly versionEmpty: string;
    readonly versionActive: string;
    readonly versionHistory: string;
    readonly publishModalTitle: string;
    readonly publishModalConfirm: string;
    readonly publishModalNoteHint: string;
    readonly publishModalNotePlaceholder: string;
  };
  readonly publishConfirm: {
    readonly titlePrefix: string;
    readonly okText: string;
    readonly resourceAgent: string;
    readonly resourceApp: string;
    readonly resourceWorkflow: string;
    readonly bodyTemplate: string;
    readonly pendingChanges: string;
    readonly noteOptional: string;
    readonly notePlaceholder: string;
  };
  readonly channelDetail: {
    readonly loadActiveReleaseFailed: string;
    readonly noActiveRelease: string;
    readonly activeReleaseInfoTemplate: string;
    readonly channelUiNotAvailable: string;
    readonly channelUiNotAvailableHintTemplate: string;
  };
  /** Coze 风格数据库详情（资源库 → 数据库） */
  readonly databaseDetail: {
    readonly subtitle: string;
    readonly backToLibrary: string;
    readonly channelReadWrite: string;
    readonly channelIsolation: string;
    readonly userPermissionMode: string;
    readonly editStructure: string;
    readonly tabStructure: string;
    readonly tabDraft: string;
    readonly tabOnline: string;
    readonly colFieldName: string;
    readonly colDescription: string;
    readonly colIndexed: string;
    readonly colType: string;
    readonly colRequired: string;
    readonly yes: string;
    readonly no: string;
    readonly draftCount: string;
    readonly onlineCount: string;
    readonly structureHintTemplate: string;
    readonly addRecord: string;
    readonly downloadTemplate: string;
    readonly importData: string;
    readonly colChannel: string;
    readonly colUser: string;
    readonly colCreatedAt: string;
    readonly actions: string;
    readonly edit: string;
    readonly delete: string;
    readonly loadingRecords: string;
    readonly notFound: string;
    readonly modalEditStructure: string;
    readonly modalRenameTitle: string;
    readonly fieldNamePh: string;
    readonly fieldDescPh: string;
    readonly requiredOn: string;
    readonly requiredOff: string;
    readonly removeField: string;
    readonly addField: string;
    readonly modalRecordCreate: string;
    readonly modalRecordEditTemplate: string;
    readonly writeTargetHintTemplate: string;
    readonly modalChannelRw: string;
    readonly modalChannelRwEmpty: string;
    readonly colChannelName: string;
    readonly colChannelType: string;
    readonly linkDraftData: string;
    readonly linkOnlineData: string;
    readonly popoverIsolationTitle: string;
    readonly isolationFullShared: string;
    readonly isolationFullSharedDesc: string;
    readonly isolationChannel: string;
    readonly isolationChannelDesc: string;
    readonly isolationInternal: string;
    readonly isolationInternalDesc: string;
    readonly popoverUserModeTitle: string;
    readonly userModeSingle: string;
    readonly userModeSingleDesc: string;
    readonly userModeMulti: string;
    readonly userModeMultiDesc: string;
    readonly toastLoadDetailFailed: string;
    readonly toastLoadRecordsFailed: string;
    readonly toastSchemaNotEditable: string;
    readonly toastMinOneField: string;
    readonly toastStructureSaved: string;
    readonly toastStructureSaveFailed: string;
    readonly toastJsonInvalid: string;
    readonly toastRecordSaved: string;
    readonly toastRecordUpdated: string;
    readonly toastRecordSaveFailed: string;
    readonly toastRecordDeleted: string;
    readonly toastRecordDeleteFailed: string;
    readonly toastChannelRwNotAvailable: string;
    readonly toastChannelRwSaved: string;
    readonly toastChannelRwSaveFailed: string;
    readonly toastModeNotAvailable: string;
    readonly toastModeSaved: string;
    readonly toastModeSaveFailed: string;
    readonly toastImportSubmitted: string;
    readonly toastImportFailed: string;
    readonly typeString: string;
    readonly typeNumber: string;
    readonly typeInteger: string;
    readonly typeBoolean: string;
    readonly typeDate: string;
    readonly typeJson: string;
    readonly typeArray: string;
    readonly typeUnknown: string;
  };
  readonly modelGuard: {
    readonly noModelTitle: string;
    readonly noEnabledModelTitle: string;
    readonly noModelDescription: string;
    readonly noEnabledModelDescription: string;
    readonly goToModelSettings: string;
  };
  readonly quickStart: {
    readonly cardTitle: string;
    readonly buildAgentTitle: string;
    readonly buildAgentDescription: string;
    readonly buildAppTitle: string;
    readonly buildAppDescription: string;
    readonly composeWorkflowTitle: string;
    readonly composeWorkflowDescription: string;
  };
  readonly chatSdk: {
    readonly title: string;
    readonly hint: string;
    readonly activeSnippet: string;
    readonly snippetMissing: string;
    readonly endpointLabel: string;
    readonly secretLabel: string;
    readonly originsLabel: string;
    readonly originsNoRestriction: string;
  };
  /* M4b */
  readonly apiAccess: {
    readonly title: string;
    readonly hint: string;
    readonly activeEndpoint: string;
    readonly tokenLabel: string;
    readonly rateLimitLabel: string;
    readonly availableEndpoints: string;
    readonly curlSection: string;
    readonly headersSection: string;
    readonly headersHint: string;
  };
  readonly tokenManagement: {
    readonly title: string;
    readonly hint: string;
    readonly columnResource: string;
    readonly columnEmbedToken: string;
    readonly columnActions: string;
    readonly notIssued: string;
    readonly regenerate: string;
    readonly emptyHint: string;
    readonly onlyAgentSupportRegenerate: string;
    readonly tokenRegenerated: string;
    readonly regenerateFailed: string;
  };
  readonly channelsList: {
    readonly columnName: string;
    readonly columnType: string;
    readonly columnStatus: string;
    readonly columnAuth: string;
    readonly columnLastSync: string;
    readonly emptyTitle: string;
    readonly emptyHint: string;
    readonly loadFailed: string;
    readonly typeFeishu: string;
    readonly typeWechatMp: string;
    readonly typeWechatMiniapp: string;
    readonly typeWechatCs: string;
    readonly typeWechat: string;
    readonly typeCustom: string;
  };
  readonly feishuTab: {
    readonly title: string;
    readonly hint: string;
    readonly credentialSaved: string;
    readonly credentialCleared: string;
    readonly loading: string;
    readonly appIdLabel: string;
    readonly verificationTokenLabel: string;
    readonly encryptKeyLabel: string;
    readonly encryptKeyConfigured: string;
    readonly encryptKeyNotSet: string;
    readonly refreshCountLabel: string;
    readonly tokenExpiresAtLabel: string;
    readonly webhookHint: string;
    readonly formAppSecretLabel: string;
    readonly formEncryptKeyOptionalLabel: string;
    readonly saveCredential: string;
    readonly clear: string;
  };
  readonly wechatMpTab: {
    readonly title: string;
    readonly hint: string;
    readonly credentialSaved: string;
    readonly credentialCleared: string;
    readonly loading: string;
    readonly appIdLabel: string;
    readonly serverTokenLabel: string;
    readonly encodingAesKeyLabel: string;
    readonly encodingAesKeyConfigured: string;
    readonly encodingAesKeyNotSet: string;
    readonly accessTokenRefreshCountLabel: string;
    readonly accessTokenExpiresAtLabel: string;
    readonly webhookHint: string;
    readonly formAppSecretLabel: string;
    readonly formEncodingAesKeyOptionalLabel: string;
    readonly saveCredential: string;
    readonly clear: string;
  };
  readonly wechatMiniappTab: {
    readonly title: string;
    readonly hint: string;
    readonly credentialSaved: string;
    readonly credentialCleared: string;
    readonly loading: string;
    readonly appIdLabel: string;
    readonly originalIdLabel: string;
    readonly messageTokenLabel: string;
    readonly encodingAesKeyLabel: string;
    readonly encodingAesKeyConfigured: string;
    readonly encodingAesKeyNotSet: string;
    readonly accessTokenRefreshCountLabel: string;
    readonly accessTokenExpiresAtLabel: string;
    readonly webhookHint: string;
    readonly formAppSecretLabel: string;
    readonly formEncodingAesKeyOptionalLabel: string;
    readonly saveCredential: string;
    readonly clear: string;
  };
  readonly wechatCsTab: {
    readonly title: string;
    readonly hint: string;
    readonly credentialSaved: string;
    readonly credentialCleared: string;
    readonly loading: string;
    readonly corpIdLabel: string;
    readonly openKfIdLabel: string;
    readonly serverTokenLabel: string;
    readonly encodingAesKeyLabel: string;
    readonly encodingAesKeyConfigured: string;
    readonly encodingAesKeyNotSet: string;
    readonly accessTokenRefreshCountLabel: string;
    readonly accessTokenExpiresAtLabel: string;
    readonly webhookHint: string;
    readonly formSecretLabel: string;
    readonly formEncodingAesKeyOptionalLabel: string;
    readonly saveCredential: string;
    readonly clear: string;
  };
  readonly addChannelModal: {
    readonly title: string;
    readonly stepChooseType: string;
    readonly stepBasicInfo: string;
    readonly loadFailed: string;
    readonly emptyTitle: string;
    readonly emptyHint: string;
    readonly chooseTypeHint: string;
    readonly selectedTypeLabel: string;
    readonly credentialKindLabel: string;
    readonly publishTypeLabel: string;
    readonly channelNameLabel: string;
    readonly channelNamePlaceholder: string;
    readonly channelDescription: string;
    readonly next: string;
    readonly back: string;
    readonly create: string;
    readonly creating: string;
    readonly created: string;
    readonly targetAgent: string;
    readonly targetApp: string;
    readonly targetWorkflow: string;
  };
  readonly resourceReference: {
    readonly defaultHeading: string;
    readonly columnReferrerType: string;
    readonly columnName: string;
    readonly columnBinding: string;
    readonly bodyHint: string;
    readonly emptyTitle: string;
    readonly emptyDescription: string;
    readonly loadFailed: string;
    readonly referrerAgent: string;
    readonly referrerApp: string;
    readonly referrerWorkflow: string;
  };
  /* M4b 续 */
  readonly inputComponent: {
    readonly cardTitle: string;
    readonly itemSuffix: string;
    readonly bodyHint: string;
    readonly emptyHint: string;
    readonly fieldLabel: string;
    readonly fieldVariableKey: string;
    readonly fieldType: string;
    readonly fieldRequired: string;
    readonly fieldOptions: string;
    readonly fieldDefault: string;
    readonly addRow: string;
    readonly removeRow: string;
    readonly addOption: string;
    readonly placeholderLabel: string;
    readonly placeholderVariableKey: string;
    readonly placeholderOptionLabel: string;
    readonly placeholderOptionValue: string;
    readonly placeholderDefault: string;
    readonly defaultOptionLabel: string;
    readonly typeText: string;
    readonly typeTextarea: string;
    readonly typeNumber: string;
    readonly typeDate: string;
    readonly typeSelect: string;
    readonly typeFile: string;
  };
  readonly outputComponent: {
    readonly cardTitle: string;
    readonly itemSuffix: string;
    readonly bodyHint: string;
    readonly emptyHint: string;
    readonly fieldLabel: string;
    readonly fieldType: string;
    readonly fieldSourceExpression: string;
    readonly addRow: string;
    readonly removeRow: string;
    readonly placeholderLabel: string;
    readonly placeholderSourceExpression: string;
    readonly typeText: string;
    readonly typeTable: string;
    readonly typeChart: string;
  };
  readonly appPreview: {
    readonly noData: string;
    readonly fillVariableKeyFirst: string;
    readonly selectedFilePrefix: string;
    readonly kicker: string;
    readonly title: string;
    readonly layoutForm: string;
    readonly layoutChat: string;
    readonly layoutHybrid: string;
    readonly bannerTitle: string;
    readonly bannerDescription: string;
    readonly formPreviewSection: string;
    readonly noInputsHint: string;
    readonly unnamed: string;
    readonly required: string;
    readonly runPreview: string;
    readonly outputResultSection: string;
    readonly noOutputMappingHint: string;
    readonly outputFallbackTitle: string;
    readonly traceTitle: string;
    readonly notRunHint: string;
  };
}

const zhCN: StudioCopy = {
  common: {
    close: "关闭",
    cancel: "取消",
    retry: "重试",
    loadDataFailed: "加载数据失败",
    unknownError: "发生未知错误",
    emptyData: "暂无数据"
  },
  status: {
    unknown: "未知",
    published: "已发布",
    draft: "草稿",
    outdated: "有更新"
  },
  appBuilder: {
    duplicateVariableKey: "存在重复的变量键，请修改后再保存。",
    missingVariableKey: "存在未填写变量键的输入项，请补全或删除该项。",
    workflowBindHeader: "工作流绑定",
    workflowBindHint: "请选择已发布的工作流作为应用运行入口。",
    workflowBindPlaceholder: "选择已发布工作流",
    workflowBindOpenInEditor: "在工作流编辑器中打开",
    workflowBindNoDescription: "无描述"
  },
  assistant: {
    configNavTitle: "配置导航",
    configNavSubtitle: "按模块拆分人设、能力与记忆，避免单页表单过长。",
    configNavLoadingResources: "资源加载中",
    versionLoading: "正在加载发布记录…",
    versionEmpty: "当前还没有发布记录。",
    versionActive: "当前激活",
    versionHistory: "历史版本",
    publishModalTitle: "发布智能体",
    publishModalConfirm: "确认发布",
    publishModalNoteHint: "发布说明将随版本记录保存，便于审计与回滚对照（可选）。",
    publishModalNotePlaceholder: "例如：修复知识库检索阈值、更新插件工具超时策略。"
  },
  publishConfirm: {
    titlePrefix: "发布",
    okText: "确认发布",
    resourceAgent: "智能体",
    resourceApp: "应用",
    resourceWorkflow: "工作流",
    bodyTemplate: "您即将发布{type}。发布后，新版本将替换当前运行版本，外部接入的客户端将立即生效。",
    pendingChanges: "未发布变更内容：",
    noteOptional: "发布说明 (可选)",
    notePlaceholder: "简要说明本次版本更新的内容，这有助于后续的版本回溯..."
  },
  channelDetail: {
    loadActiveReleaseFailed: "加载当前发布失败。",
    noActiveRelease: "暂无生效发布。",
    activeReleaseInfoTemplate: "当前生效 v{releaseNo}（{status}）。",
    channelUiNotAvailable: "该渠道类型尚未提供 UI",
    channelUiNotAvailableHintTemplate: "检测到渠道类型 \"{type}\"，请改走 HTTP / API Tab 或联系平台管理员。"
  },
  databaseDetail: {
    subtitle: "表结构、测试数据与线上数据统一管理。",
    backToLibrary: "资源库",
    channelReadWrite: "渠道读写配置",
    channelIsolation: "渠道隔离",
    userPermissionMode: "单用户模式",
    editStructure: "编辑表结构",
    tabStructure: "表结构",
    tabDraft: "测试数据",
    tabOnline: "线上数据",
    colFieldName: "存储字段名称",
    colDescription: "描述",
    colIndexed: "设为索引",
    colType: "类型",
    colRequired: "是否必要",
    yes: "是",
    no: "否",
    draftCount: "测试",
    onlineCount: "线上",
    structureHintTemplate: "系统字段与业务字段共 {count} 项",
    addRecord: "新增记录",
    downloadTemplate: "下载模板",
    importData: "导入数据",
    colChannel: "渠道",
    colUser: "用户",
    colCreatedAt: "创建时间",
    actions: "操作",
    edit: "编辑",
    delete: "删除",
    loadingRecords: "正在加载记录…",
    notFound: "未找到数据库",
    modalEditStructure: "编辑表结构",
    modalRenameTitle: "修改表名称",
    fieldNamePh: "字段名",
    fieldDescPh: "描述",
    requiredOn: "必填",
    requiredOff: "可选",
    removeField: "删除",
    addField: "新增字段",
    modalRecordCreate: "新增记录",
    modalRecordEditTemplate: "编辑记录 #{id}",
    writeTargetHintTemplate: "当前写入：{target}",
    modalChannelRw: "渠道读写配置",
    modalChannelRwEmpty: "尚未初始化渠道配置。",
    colChannelName: "渠道",
    colChannelType: "类型",
    linkDraftData: "测试数据",
    linkOnlineData: "线上数据",
    popoverIsolationTitle: "选择模式",
    isolationFullShared: "渠道共享",
    isolationFullSharedDesc: "所有渠道共享同一份数据。",
    isolationChannel: "渠道隔离",
    isolationChannelDesc: "各渠道仅访问本渠道数据。",
    isolationInternal: "站内共享",
    isolationInternalDesc: "站内渠道共享数据，外部渠道彼此隔离。",
    popoverUserModeTitle: "选择权限模式",
    userModeSingle: "单用户模式",
    userModeSingleDesc: "开发者和用户只能对自己创建的数据进行操作。",
    userModeMulti: "多用户模式",
    userModeMultiDesc: "开发者和用户能对所有人创建的数据进行操作（工作流节点场景）。",
    toastLoadDetailFailed: "加载数据库详情失败。",
    toastLoadRecordsFailed: "加载记录失败。",
    toastSchemaNotEditable: "当前环境未启用表结构编辑。",
    toastMinOneField: "至少保留一个业务字段。",
    toastStructureSaved: "表结构已更新。",
    toastStructureSaveFailed: "保存表结构失败。",
    toastJsonInvalid: "记录 JSON 格式不合法。",
    toastRecordSaved: "记录已创建。",
    toastRecordUpdated: "记录已更新。",
    toastRecordSaveFailed: "保存记录失败。",
    toastRecordDeleted: "记录已删除。",
    toastRecordDeleteFailed: "删除记录失败。",
    toastChannelRwNotAvailable: "渠道配置接口不可用。",
    toastChannelRwSaved: "渠道读写配置已保存。",
    toastChannelRwSaveFailed: "保存渠道配置失败。",
    toastModeNotAvailable: "模式配置接口不可用。",
    toastModeSaved: "模式已更新。",
    toastModeSaveFailed: "保存模式失败。",
    toastImportSubmitted: "导入任务已提交。",
    toastImportFailed: "导入提交失败。",
    typeString: "String",
    typeNumber: "Number",
    typeInteger: "Integer",
    typeBoolean: "Boolean",
    typeDate: "Time",
    typeJson: "Object",
    typeArray: "Array",
    typeUnknown: "String"
  },
  modelGuard: {
    noModelTitle: "系统尚未配置 AI 模型",
    noEnabledModelTitle: "当前没有已启用的 AI 模型",
    noModelDescription: "AI Agent、工作流和应用依赖底层大语言模型才能运行。请先在模型管理中配置并启用至少一个模型提供商（如 OpenAI、千问等）。",
    noEnabledModelDescription: "您的模型列表中没有任何处于启用状态的模型。在启用至少一个模型之前，所有 AI 相关功能（如调试、运行）将无法正常工作。",
    goToModelSettings: "前往配置模型"
  },
  quickStart: {
    cardTitle: "快速开始",
    buildAgentTitle: "构建智能体",
    buildAgentDescription: "配置提示词、工具和知识库，创建专属 AI 助手",
    buildAppTitle: "搭建应用",
    buildAppDescription: "将工作流或智能体封装为带界面的交互式应用",
    composeWorkflowTitle: "编排工作流",
    composeWorkflowDescription: "可视化连接多个节点，编排复杂 AI 业务逻辑"
  },
  chatSdk: {
    title: "Web / React 示例",
    hint: "使用发布中心展示的已发布智能体 ID 与 API 根路径；生产环境建议通过服务端代理保护令牌。",
    activeSnippet: "当前发布 snippet",
    snippetMissing: "metadata 未提供 snippet。",
    endpointLabel: "Endpoint：",
    secretLabel: "密钥（脱敏）：",
    originsLabel: "允许来源：",
    originsNoRestriction: "未限制"
  },
  apiAccess: {
    title: "HTTP / API 接入",
    hint: "将占位符替换为租户 ID、访问令牌，以及各已发布资源返回的实际 API 路径。",
    activeEndpoint: "当前发布端点",
    tokenLabel: "令牌（脱敏）：",
    rateLimitLabel: "速率限制：",
    availableEndpoints: "可用端点",
    curlSection: "cURL 示例",
    headersSection: "请求头说明",
    headersHint: "Authorization：登录后下发的 JWT。X-Tenant-Id：必须与令牌中的租户一致。"
  },
  tokenManagement: {
    title: "嵌入令牌",
    hint: "复制嵌入令牌用于前端托管组件。轮换令牌会使旧令牌失效（按智能体维度）。",
    columnResource: "资源",
    columnEmbedToken: "嵌入令牌",
    columnActions: "操作",
    notIssued: "未下发",
    regenerate: "重新生成",
    emptyHint: "发布清单中暂无可用的嵌入令牌。",
    onlyAgentSupportRegenerate: "当前仅支持对智能体轮换嵌入令牌。",
    tokenRegenerated: "已重新生成令牌。",
    regenerateFailed: "轮换失败。"
  },
  channelsList: {
    columnName: "渠道名",
    columnType: "类型",
    columnStatus: "状态",
    columnAuth: "认证",
    columnLastSync: "上次同步",
    emptyTitle: "暂无发布渠道",
    emptyHint: "请在发布流程中先创建渠道。",
    loadFailed: "加载渠道失败。",
    typeFeishu: "飞书",
    typeWechatMp: "微信公众号",
    typeWechatMiniapp: "微信小程序",
    typeWechatCs: "微信客服",
    typeWechat: "企业微信",
    typeCustom: "自定义"
  },
  feishuTab: {
    title: "飞书渠道",
    hint: "在飞书开放平台获取应用凭据后填写；AppSecret / EncryptKey 在落库前会用平台密钥加密。",
    credentialSaved: "飞书凭据已保存。",
    credentialCleared: "飞书凭据已清除。",
    loading: "加载中…",
    appIdLabel: "App Id",
    verificationTokenLabel: "校验 Token",
    encryptKeyLabel: "Encrypt Key",
    encryptKeyConfigured: "已配置",
    encryptKeyNotSet: "未设置",
    refreshCountLabel: "Token 刷新次数",
    tokenExpiresAtLabel: "Token 过期时间",
    webhookHint: "请在飞书事件订阅中填写：",
    formAppSecretLabel: "App Secret（落库前自动加密）",
    formEncryptKeyOptionalLabel: "Encrypt Key（可选）",
    saveCredential: "保存凭据",
    clear: "清除"
  },
  wechatMpTab: {
    title: "微信公众号渠道",
    hint: "在微信公众平台「基本配置」中获取 AppId、AppSecret、Token 与 EncodingAesKey 后填写。",
    credentialSaved: "微信公众号凭据已保存。",
    credentialCleared: "微信公众号凭据已清除。",
    loading: "加载中…",
    appIdLabel: "App Id",
    serverTokenLabel: "服务器 Token",
    encodingAesKeyLabel: "EncodingAesKey",
    encodingAesKeyConfigured: "已配置",
    encodingAesKeyNotSet: "未设置",
    accessTokenRefreshCountLabel: "AccessToken 刷新次数",
    accessTokenExpiresAtLabel: "AccessToken 过期时间",
    webhookHint: "请在微信公众平台「服务器地址 (URL)」中填写：",
    formAppSecretLabel: "App Secret（落库前自动加密）",
    formEncodingAesKeyOptionalLabel: "EncodingAesKey（可选）",
    saveCredential: "保存凭据",
    clear: "清除"
  },
  wechatMiniappTab: {
    title: "微信小程序渠道",
    hint: "在微信小程序管理后台填写 AppId、AppSecret，以及可选的原始 ID、消息 Token 与 EncodingAesKey。",
    credentialSaved: "微信小程序凭据已保存。",
    credentialCleared: "微信小程序凭据已清除。",
    loading: "加载中…",
    appIdLabel: "App Id",
    originalIdLabel: "原始 ID",
    messageTokenLabel: "消息 Token",
    encodingAesKeyLabel: "EncodingAesKey",
    encodingAesKeyConfigured: "已配置",
    encodingAesKeyNotSet: "未设置",
    accessTokenRefreshCountLabel: "AccessToken 刷新次数",
    accessTokenExpiresAtLabel: "AccessToken 过期时间",
    webhookHint: "如需配置回调地址，请在小程序后台填写：",
    formAppSecretLabel: "App Secret（落库前自动加密）",
    formEncodingAesKeyOptionalLabel: "EncodingAesKey（可选）",
    saveCredential: "保存凭据",
    clear: "清除"
  },
  wechatCsTab: {
    title: "微信客服渠道",
    hint: "填写微信客服所需的企业 CorpId、Secret、OpenKfId，以及可选的服务器 Token / EncodingAesKey。",
    credentialSaved: "微信客服凭据已保存。",
    credentialCleared: "微信客服凭据已清除。",
    loading: "加载中…",
    corpIdLabel: "Corp Id",
    openKfIdLabel: "OpenKf Id",
    serverTokenLabel: "服务器 Token",
    encodingAesKeyLabel: "EncodingAesKey",
    encodingAesKeyConfigured: "已配置",
    encodingAesKeyNotSet: "未设置",
    accessTokenRefreshCountLabel: "AccessToken 刷新次数",
    accessTokenExpiresAtLabel: "AccessToken 过期时间",
    webhookHint: "如需配置事件回调地址，请填写：",
    formSecretLabel: "Secret（落库前自动加密）",
    formEncodingAesKeyOptionalLabel: "EncodingAesKey（可选）",
    saveCredential: "保存凭据",
    clear: "清除"
  },
  addChannelModal: {
    title: "新增渠道",
    stepChooseType: "选择渠道类型",
    stepBasicInfo: "填写基础信息",
    loadFailed: "加载渠道目录失败。",
    emptyTitle: "暂无可用渠道",
    emptyHint: "当前目录中没有可创建的发布渠道。",
    chooseTypeHint: "先选择渠道类型，再填写渠道名称并创建空壳渠道。",
    selectedTypeLabel: "已选类型",
    credentialKindLabel: "凭据类型",
    publishTypeLabel: "发布类型",
    channelNameLabel: "渠道名称",
    channelNamePlaceholder: "请输入渠道名称",
    channelDescription: "默认会创建支持智能体、应用、工作流的渠道条目，凭据可在详情面板继续配置。",
    next: "下一步",
    back: "上一步",
    create: "创建渠道",
    creating: "创建中…",
    created: "渠道已创建。",
    targetAgent: "智能体",
    targetApp: "应用",
    targetWorkflow: "工作流"
  },
  resourceReference: {
    defaultHeading: "引用本资源的实体",
    columnReferrerType: "引用方类型",
    columnName: "名称",
    columnBinding: "绑定字段",
    bodyHint: "依赖此资源的智能体、应用或工作流（用于影响分析与变更评估）。",
    emptyTitle: "暂无引用",
    emptyDescription: "当前没有其他实体引用该资源。",
    loadFailed: "加载引用关系失败。",
    referrerAgent: "智能体",
    referrerApp: "应用",
    referrerWorkflow: "工作流"
  },
  inputComponent: {
    cardTitle: "输入组件",
    itemSuffix: "项",
    bodyHint: "定义表单字段与变量键，预览与运行将按变量键组装入参。",
    emptyHint: "暂无输入项，请点击下方添加。",
    fieldLabel: "标签",
    fieldVariableKey: "变量键",
    fieldType: "类型",
    fieldRequired: "必填",
    fieldOptions: "选项（标签 / 值）",
    fieldDefault: "默认值（可选）",
    addRow: "添加输入项",
    removeRow: "删除",
    addOption: "添加选项",
    placeholderLabel: "显示名称",
    placeholderVariableKey: "如 userQuery",
    placeholderOptionLabel: "标签",
    placeholderOptionValue: "值",
    placeholderDefault: "未填写时的默认值",
    defaultOptionLabel: "选项 A",
    typeText: "单行文本",
    typeTextarea: "多行文本",
    typeNumber: "数字",
    typeDate: "日期",
    typeSelect: "下拉",
    typeFile: "文件"
  },
  outputComponent: {
    cardTitle: "输出组件",
    itemSuffix: "项",
    bodyHint: "使用源表达式从运行结果中取值（支持顶层键或点路径，如 result.items）。",
    emptyHint: "暂无输出项。",
    fieldLabel: "标签",
    fieldType: "展示类型",
    fieldSourceExpression: "源表达式",
    addRow: "添加输出项",
    removeRow: "删除",
    placeholderLabel: "展示标题",
    placeholderSourceExpression: "如 answer 或 data.summary",
    typeText: "纯文本",
    typeTable: "表格",
    typeChart: "图表"
  },
  appPreview: {
    noData: "（无数据）",
    fillVariableKeyFirst: "请先填写变量键",
    selectedFilePrefix: "已选：",
    kicker: "实时预览",
    title: "预览与运行",
    layoutForm: "表单",
    layoutChat: "对话",
    layoutHybrid: "混合",
    bannerTitle: "说明",
    bannerDescription: "左侧配置保存后，可在此填写预览值并运行，查看输出映射结果与执行轨迹摘要。",
    formPreviewSection: "表单预览",
    noInputsHint: "尚未配置输入组件。",
    unnamed: "未命名",
    required: "必填",
    runPreview: "运行预览",
    outputResultSection: "输出结果",
    noOutputMappingHint: "未配置输出映射，以下为完整返回对象：",
    outputFallbackTitle: "输出",
    traceTitle: "执行轨迹",
    notRunHint: "尚未运行。点击「运行预览」加载结果。"
  }
};

const enUS: StudioCopy = {
  common: {
    close: "Close",
    cancel: "Cancel",
    retry: "Retry",
    loadDataFailed: "Failed to load data",
    unknownError: "Unknown error",
    emptyData: "No data"
  },
  status: {
    unknown: "Unknown",
    published: "Published",
    draft: "Draft",
    outdated: "Outdated"
  },
  appBuilder: {
    duplicateVariableKey: "Duplicate variable key detected. Please fix it before saving.",
    missingVariableKey: "Some input items have an empty variable key. Please fill in or remove them.",
    workflowBindHeader: "Workflow binding",
    workflowBindHint: "Select a published workflow as the application entry.",
    workflowBindPlaceholder: "Select a published workflow",
    workflowBindOpenInEditor: "Open in workflow editor",
    workflowBindNoDescription: "No description"
  },
  assistant: {
    configNavTitle: "Configuration",
    configNavSubtitle: "Split persona, capability, and memory into modules to avoid an overlong form.",
    configNavLoadingResources: "Loading resources",
    versionLoading: "Loading release history…",
    versionEmpty: "No release history yet.",
    versionActive: "Active",
    versionHistory: "Archived version",
    publishModalTitle: "Publish agent",
    publishModalConfirm: "Confirm publish",
    publishModalNoteHint: "Release notes are saved with the version record for audit and rollback (optional).",
    publishModalNotePlaceholder: "e.g. fixed knowledge retrieval threshold, updated plugin tool timeout policy."
  },
  publishConfirm: {
    titlePrefix: "Publish",
    okText: "Confirm publish",
    resourceAgent: "agent",
    resourceApp: "application",
    resourceWorkflow: "workflow",
    bodyTemplate: "You are about to publish the {type}. Once published, the new version will replace the current runtime and all external clients will take effect immediately.",
    pendingChanges: "Unpublished changes:",
    noteOptional: "Release note (optional)",
    notePlaceholder: "Briefly describe what changed in this version to help later traceability..."
  },
  channelDetail: {
    loadActiveReleaseFailed: "Failed to load active release.",
    noActiveRelease: "No active release.",
    activeReleaseInfoTemplate: "Active release v{releaseNo} ({status}).",
    channelUiNotAvailable: "Channel UI not available yet",
    channelUiNotAvailableHintTemplate: "Type \"{type}\" detected. Use the HTTP / API tab or contact platform admin."
  },
  databaseDetail: {
    subtitle: "Schema, draft data, and online data in one place.",
    backToLibrary: "Library",
    channelReadWrite: "Channel read/write",
    channelIsolation: "Channel isolation",
    userPermissionMode: "Permission mode",
    editStructure: "Edit schema",
    tabStructure: "Schema",
    tabDraft: "Test data",
    tabOnline: "Online data",
    colFieldName: "Field name",
    colDescription: "Description",
    colIndexed: "Indexed",
    colType: "Type",
    colRequired: "Required",
    yes: "Yes",
    no: "No",
    draftCount: "Draft",
    onlineCount: "Online",
    structureHintTemplate: "{count} fields (system + custom)",
    addRecord: "Add row",
    downloadTemplate: "Template",
    importData: "Import",
    colChannel: "Channel",
    colUser: "User",
    colCreatedAt: "Created",
    actions: "Actions",
    edit: "Edit",
    delete: "Delete",
    loadingRecords: "Loading rows…",
    notFound: "Database not found",
    modalEditStructure: "Edit schema",
    modalRenameTitle: "Rename table",
    fieldNamePh: "Field name",
    fieldDescPh: "Description",
    requiredOn: "Required",
    requiredOff: "Optional",
    removeField: "Remove",
    addField: "Add field",
    modalRecordCreate: "New row",
    modalRecordEditTemplate: "Edit row #{id}",
    writeTargetHintTemplate: "Target: {target}",
    modalChannelRw: "Channel read/write",
    modalChannelRwEmpty: "Channel config not initialized.",
    colChannelName: "Channel",
    colChannelType: "Type",
    linkDraftData: "Test data",
    linkOnlineData: "Online data",
    popoverIsolationTitle: "Isolation mode",
    isolationFullShared: "Shared",
    isolationFullSharedDesc: "All channels share the same dataset.",
    isolationChannel: "Isolated",
    isolationChannelDesc: "Each channel only sees its own rows.",
    isolationInternal: "Internal shared",
    isolationInternalDesc: "Internal channels share data; external channels stay isolated.",
    popoverUserModeTitle: "Permission mode",
    userModeSingle: "Single-user",
    userModeSingleDesc: "Users only access rows they created.",
    userModeMulti: "Multi-user",
    userModeMultiDesc: "Users may access rows from everyone (workflow nodes).",
    toastLoadDetailFailed: "Failed to load database.",
    toastLoadRecordsFailed: "Failed to load rows.",
    toastSchemaNotEditable: "Schema editing is not available.",
    toastMinOneField: "Keep at least one custom field.",
    toastStructureSaved: "Schema updated.",
    toastStructureSaveFailed: "Failed to save schema.",
    toastJsonInvalid: "Invalid JSON for row.",
    toastRecordSaved: "Row created.",
    toastRecordUpdated: "Row updated.",
    toastRecordSaveFailed: "Failed to save row.",
    toastRecordDeleted: "Row deleted.",
    toastRecordDeleteFailed: "Failed to delete row.",
    toastChannelRwNotAvailable: "Channel API not available.",
    toastChannelRwSaved: "Channel settings saved.",
    toastChannelRwSaveFailed: "Failed to save channel settings.",
    toastModeNotAvailable: "Mode API not available.",
    toastModeSaved: "Mode updated.",
    toastModeSaveFailed: "Failed to save mode.",
    toastImportSubmitted: "Import job submitted.",
    toastImportFailed: "Import failed.",
    typeString: "String",
    typeNumber: "Number",
    typeInteger: "Integer",
    typeBoolean: "Boolean",
    typeDate: "Time",
    typeJson: "Object",
    typeArray: "Array",
    typeUnknown: "String"
  },
  modelGuard: {
    noModelTitle: "No AI model configured yet",
    noEnabledModelTitle: "No enabled AI model is available",
    noModelDescription: "AI agents, workflows, and apps rely on a base large language model to run. Configure and enable at least one model provider in Model Settings first, such as OpenAI or Qianwen.",
    noEnabledModelDescription: "None of the models in your list are currently enabled. Before enabling at least one model, AI-related features such as testing and execution will not work properly.",
    goToModelSettings: "Go to model settings"
  },
  quickStart: {
    cardTitle: "Quick start",
    buildAgentTitle: "Build an agent",
    buildAgentDescription: "Configure prompts, tools, and knowledge bases to create a dedicated AI assistant.",
    buildAppTitle: "Build an app",
    buildAppDescription: "Package workflows or agents into an interactive app with a UI.",
    composeWorkflowTitle: "Compose a workflow",
    composeWorkflowDescription: "Visually connect multiple nodes to orchestrate complex AI business logic."
  },
  chatSdk: {
    title: "Web / React snippets",
    hint: "Use the published agent id and the API base from the release center. Prefer server-side proxy for tokens in production.",
    activeSnippet: "Active embed snippet",
    snippetMissing: "Snippet not provided in metadata.",
    endpointLabel: "Endpoint: ",
    secretLabel: "Secret (masked): ",
    originsLabel: "Allowed origins: ",
    originsNoRestriction: "No restriction"
  },
  apiAccess: {
    title: "HTTP / API access",
    hint: "Replace placeholders with your tenant, token, and the endpoint returned for each published resource.",
    activeEndpoint: "Active endpoint",
    tokenLabel: "Token (masked): ",
    rateLimitLabel: "Rate limit: ",
    availableEndpoints: "Available endpoints",
    curlSection: "cURL",
    headersSection: "Headers",
    headersHint: "Authorization: JWT from sign-in. X-Tenant-Id: must match the tenant bound to the token."
  },
  tokenManagement: {
    title: "Embed tokens",
    hint: "Copy embed tokens for hosted widgets. Regenerate invalidates the previous token for that agent.",
    columnResource: "Resource",
    columnEmbedToken: "Embed token",
    columnActions: "Actions",
    notIssued: "Not issued",
    regenerate: "Regenerate",
    emptyHint: "No embed tokens in the publish list yet.",
    onlyAgentSupportRegenerate: "Only agents support regenerate here.",
    tokenRegenerated: "Token regenerated.",
    regenerateFailed: "Regenerate failed."
  },
  channelsList: {
    columnName: "Name",
    columnType: "Type",
    columnStatus: "Status",
    columnAuth: "Auth",
    columnLastSync: "Last sync",
    emptyTitle: "No publish channels",
    emptyHint: "Create a channel from publish workflow first.",
    loadFailed: "Failed to load channels.",
    typeFeishu: "Feishu (Lark)",
    typeWechatMp: "WeChat MP",
    typeWechatMiniapp: "WeChat Miniapp",
    typeWechatCs: "WeChat Customer Service",
    typeWechat: "WeChat",
    typeCustom: "Custom"
  },
  feishuTab: {
    title: "Feishu (Lark) channel",
    hint: "Provide app id, app secret and event verification token from the Feishu Open Platform. Secrets are encrypted at rest.",
    credentialSaved: "Feishu credential saved.",
    credentialCleared: "Feishu credential cleared.",
    loading: "Loading…",
    appIdLabel: "App Id",
    verificationTokenLabel: "Verification Token",
    encryptKeyLabel: "Encrypt Key",
    encryptKeyConfigured: "configured",
    encryptKeyNotSet: "not set",
    refreshCountLabel: "Last token refresh count",
    tokenExpiresAtLabel: "Token expires at",
    webhookHint: "Configure Feishu event subscription URL: ",
    formAppSecretLabel: "App Secret (will be encrypted)",
    formEncryptKeyOptionalLabel: "Encrypt Key (optional)",
    saveCredential: "Save credential",
    clear: "Clear"
  },
  wechatMpTab: {
    title: "WeChat Official Account",
    hint: "Provide AppId / AppSecret / Token / EncodingAesKey from the WeChat Official Account admin console.",
    credentialSaved: "WeChat MP credential saved.",
    credentialCleared: "WeChat MP credential cleared.",
    loading: "Loading…",
    appIdLabel: "App Id",
    serverTokenLabel: "Server Token",
    encodingAesKeyLabel: "EncodingAesKey",
    encodingAesKeyConfigured: "configured",
    encodingAesKeyNotSet: "not set",
    accessTokenRefreshCountLabel: "Access token refresh count",
    accessTokenExpiresAtLabel: "Access token expires at",
    webhookHint: "Configure WeChat MP server URL: ",
    formAppSecretLabel: "App Secret",
    formEncodingAesKeyOptionalLabel: "EncodingAesKey (optional)",
    saveCredential: "Save credential",
    clear: "Clear"
  },
  wechatMiniappTab: {
    title: "WeChat Miniapp",
    hint: "Provide AppId, AppSecret, and optional original id / message token / EncodingAesKey from the miniapp console.",
    credentialSaved: "WeChat Miniapp credential saved.",
    credentialCleared: "WeChat Miniapp credential cleared.",
    loading: "Loading…",
    appIdLabel: "App Id",
    originalIdLabel: "Original Id",
    messageTokenLabel: "Message Token",
    encodingAesKeyLabel: "EncodingAesKey",
    encodingAesKeyConfigured: "configured",
    encodingAesKeyNotSet: "not set",
    accessTokenRefreshCountLabel: "Access token refresh count",
    accessTokenExpiresAtLabel: "Access token expires at",
    webhookHint: "Configure the miniapp callback URL: ",
    formAppSecretLabel: "App Secret",
    formEncodingAesKeyOptionalLabel: "EncodingAesKey (optional)",
    saveCredential: "Save credential",
    clear: "Clear"
  },
  wechatCsTab: {
    title: "WeChat Customer Service",
    hint: "Provide CorpId, secret, OpenKfId, and optional server token / EncodingAesKey for the customer service channel.",
    credentialSaved: "WeChat customer service credential saved.",
    credentialCleared: "WeChat customer service credential cleared.",
    loading: "Loading…",
    corpIdLabel: "Corp Id",
    openKfIdLabel: "OpenKf Id",
    serverTokenLabel: "Server Token",
    encodingAesKeyLabel: "EncodingAesKey",
    encodingAesKeyConfigured: "configured",
    encodingAesKeyNotSet: "not set",
    accessTokenRefreshCountLabel: "Access token refresh count",
    accessTokenExpiresAtLabel: "Access token expires at",
    webhookHint: "Configure the customer service callback URL: ",
    formSecretLabel: "Secret",
    formEncodingAesKeyOptionalLabel: "EncodingAesKey (optional)",
    saveCredential: "Save credential",
    clear: "Clear"
  },
  addChannelModal: {
    title: "Add channel",
    stepChooseType: "Choose channel type",
    stepBasicInfo: "Basic info",
    loadFailed: "Failed to load channel catalog.",
    emptyTitle: "No available channels",
    emptyHint: "There are no channel definitions available to create.",
    chooseTypeHint: "Choose a channel type first, then provide a channel name to create the shell.",
    selectedTypeLabel: "Selected type",
    credentialKindLabel: "Credential kind",
    publishTypeLabel: "Publish type",
    channelNameLabel: "Channel name",
    channelNamePlaceholder: "Enter a channel name",
    channelDescription: "The channel will be created with agent / app / workflow targets enabled. Configure credentials from the detail pane next.",
    next: "Next",
    back: "Back",
    create: "Create channel",
    creating: "Creating…",
    created: "Channel created.",
    targetAgent: "Agent",
    targetApp: "App",
    targetWorkflow: "Workflow"
  },
  resourceReference: {
    defaultHeading: "Inbound references",
    columnReferrerType: "Referrer type",
    columnName: "Name",
    columnBinding: "Binding",
    bodyHint: "Entities that depend on this resource (agents, apps, or workflows).",
    emptyTitle: "No references",
    emptyDescription: "No other entities reference this resource yet.",
    loadFailed: "Failed to load references.",
    referrerAgent: "Agent",
    referrerApp: "App",
    referrerWorkflow: "Workflow"
  },
  inputComponent: {
    cardTitle: "Input components",
    itemSuffix: "items",
    bodyHint: "Define form fields and variable keys; preview and runtime use the keys to assemble inputs.",
    emptyHint: "No input items. Click below to add.",
    fieldLabel: "Label",
    fieldVariableKey: "Variable key",
    fieldType: "Type",
    fieldRequired: "Required",
    fieldOptions: "Options (label / value)",
    fieldDefault: "Default value (optional)",
    addRow: "Add input item",
    removeRow: "Remove",
    addOption: "Add option",
    placeholderLabel: "Display name",
    placeholderVariableKey: "e.g. userQuery",
    placeholderOptionLabel: "Label",
    placeholderOptionValue: "Value",
    placeholderDefault: "Default value when not provided",
    defaultOptionLabel: "Option A",
    typeText: "Single-line text",
    typeTextarea: "Multi-line text",
    typeNumber: "Number",
    typeDate: "Date",
    typeSelect: "Select",
    typeFile: "File"
  },
  outputComponent: {
    cardTitle: "Output components",
    itemSuffix: "items",
    bodyHint: "Use source expression to extract values from the run result (top-level key or dot path, e.g. result.items).",
    emptyHint: "No output items.",
    fieldLabel: "Label",
    fieldType: "Display type",
    fieldSourceExpression: "Source expression",
    addRow: "Add output item",
    removeRow: "Remove",
    placeholderLabel: "Display title",
    placeholderSourceExpression: "e.g. answer or data.summary",
    typeText: "Plain text",
    typeTable: "Table",
    typeChart: "Chart"
  },
  appPreview: {
    noData: "(no data)",
    fillVariableKeyFirst: "Please fill in the variable key first",
    selectedFilePrefix: "Selected: ",
    kicker: "Live preview",
    title: "Preview & run",
    layoutForm: "Form",
    layoutChat: "Chat",
    layoutHybrid: "Hybrid",
    bannerTitle: "Note",
    bannerDescription: "After saving the left-side config, fill in preview values and run to see output mapping results and execution trace summary.",
    formPreviewSection: "Form preview",
    noInputsHint: "No input components configured yet.",
    unnamed: "Unnamed",
    required: "Required",
    runPreview: "Run preview",
    outputResultSection: "Output result",
    noOutputMappingHint: "No output mapping configured. Showing the full response object below:",
    outputFallbackTitle: "Output",
    traceTitle: "Execution trace",
    notRunHint: "Not run yet. Click \"Run preview\" to load the result."
  }
};

export function getStudioCopy(locale: StudioLocale): StudioCopy {
  return locale === "en-US" ? enUS : zhCN;
}

/**
 * 模板字符串占位符替换：把 "{key}" 替换为给定 params 中的对应值。
 */
export function formatStudioTemplate(template: string, params: Record<string, string | number>): string {
  return Object.entries(params).reduce(
    (acc, [key, value]) => acc.replace(new RegExp(`\\{${key}\\}`, "g"), String(value)),
    template
  );
}
