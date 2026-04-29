export type MendixStudioLocale = "zh-CN" | "en-US";

export interface MendixStudioCopy {
  readonly common: {
    readonly refreshPage: string;
    readonly backToLibrary: string;
  };
  readonly app: {
    readonly initFailedTitle: string;
    readonly initFailedDescription: string;
    readonly workspaceIdLabel: string;
    readonly appIdLabel: string;
    readonly microflowMissingTitle: string;
    readonly microflowTabMissingIdTitle: string;
    readonly microflowMissingDescription: string;
  };
  readonly index: {
    readonly workspaceLabel: string;
    readonly sampleAppTitle: string;
    readonly sampleAppDescription: string;
    readonly createAppInProgress: string;
    readonly createAppButton: string;
    readonly footer: string;
    readonly noAppTitle: string;
    readonly noAppDescription: string;
    readonly openAppPlaceholder: string;
    readonly openAppButton: string;
    readonly openAppEmptyError: string;
    readonly devSampleHint: string;
  };
  readonly editorPage: {
    readonly notFoundTitle: string;
    readonly forbiddenTitle: string;
    readonly conflictTitle: string;
    readonly serviceErrorTitle: string;
    readonly serviceNotConfigured: string;
    readonly emptyTitle: string;
    readonly emptyDescription: string;
    readonly httpOnlyTitle: string;
    readonly httpOnlyDescription: string;
    readonly adapterNotConfiguredTitle: string;
    readonly adapterNotConfiguredDescription: string;
    readonly saveSuccess: string;
  };
  readonly createModal: {
    readonly networkUnavailable: string;
    readonly unauthorized: string;
    readonly forbidden: string;
    readonly duplicated: string;
    readonly validationFailed: string;
    readonly serverError: string;
    readonly fallbackError: string;
    readonly invalidNameRequired: string;
    readonly invalidNamePattern: string;
    readonly invalidNameDuplicated: string;
    readonly missingModuleId: string;
    readonly missingModuleContext: string;
    readonly invalidParameterName: string;
    readonly duplicatedParameterName: string;
    readonly invalidUrlPath: string;
    readonly formInvalid: string;
    readonly submitSuccess: string;
    readonly retryHint: string;
    readonly retryableHint: string;
    readonly title: string;
    readonly createButton: string;
  };
}

const zhCN: MendixStudioCopy = {
  common: {
    refreshPage: "刷新页面",
    backToLibrary: "返回资源库",
  },
  app: {
    initFailedTitle: "微流服务初始化失败",
    initFailedDescription:
      "当前页面无法创建 microflow adapter bundle。请检查 apiBaseUrl、adapter mode、workspaceId 和 tenantId 配置后刷新页面。",
    workspaceIdLabel: "workspaceId",
    appIdLabel: "appId",
    microflowMissingTitle: "微流资源不存在或已被删除",
    microflowTabMissingIdTitle: "微流 Workbench tab 缺少 microflowId",
    microflowMissingDescription:
      "本轮不会创建 fake resource，也不会加载真实 schema。请刷新资源列表或关闭该 tab 后从 App Explorer 重新打开真实微流。",
  },
  index: {
    workspaceLabel: "工作区",
    sampleAppTitle: "Procurement Approval（示例）",
    sampleAppDescription: "采购审批工作流 · Domain Model · Microflow · Workflow",
    createAppInProgress: "新建应用功能开发中",
    createAppButton: "+ 新建应用",
    footer: "Mendix Studio Core · Atlas Security Platform · v0.0.0",
    noAppTitle: "选择要打开的应用",
    noAppDescription: "请输入工作区下已有的 appId 打开 Mendix Studio。新建应用功能开发中。",
    openAppPlaceholder: "请输入 appId",
    openAppButton: "打开应用",
    openAppEmptyError: "请输入 appId。",
    devSampleHint: "开发示例应用（仅在本地 dev 构建可见）",
  },
  editorPage: {
    notFoundTitle: "微流不存在",
    forbiddenTitle: "无权限访问该微流",
    conflictTitle: "微流版本冲突",
    serviceErrorTitle: "微流服务异常",
    serviceNotConfigured: "微流服务未配置。",
    emptyTitle: "微流不存在",
    emptyDescription: "资源可能已被删除或当前工作区不可见。",
    httpOnlyTitle: "微流编辑器仅支持 HTTP 模式",
    httpOnlyDescription: "发布路径不允许使用 local 或 mock adapter。请切换到 HTTP adapter 后重试。",
    adapterNotConfiguredTitle: "微流服务未配置",
    adapterNotConfiguredDescription: "请配置 HTTP adapter 的 apiBaseUrl 后重试。",
    saveSuccess: "微流已保存",
  },
  createModal: {
    networkUnavailable: "微流服务不可用，请检查网络或后端服务。",
    unauthorized: "登录已失效，请重新登录。",
    forbidden: "当前账号无权限创建微流。",
    duplicated: "同名微流已存在。",
    validationFailed: "微流校验失败，请检查输入字段。",
    serverError: "微流服务异常，请联系管理员。",
    fallbackError: "微流服务异常。",
    invalidNameRequired: "name 不能为空。",
    invalidNamePattern: "name 必须以字母开头，且只能包含字母、数字和下划线。",
    invalidNameDuplicated: "同名微流已存在。",
    missingModuleId: "moduleId 不能为空。",
    missingModuleContext: "缺少模块上下文，无法创建微流。",
    invalidParameterName: "参数名格式不合法",
    duplicatedParameterName: "参数名不能重复",
    invalidUrlPath: "URL 路径必须以 / 开头",
    formInvalid: "请先修正表单错误。",
    submitSuccess: "微流创建成功",
    retryHint: "该错误可重试，请稍后再试。",
    retryableHint: "该错误可重试，请稍后再试。",
    title: "新建微流",
    createButton: "创建",
  },
};

const enUS: MendixStudioCopy = {
  common: {
    refreshPage: "Refresh",
    backToLibrary: "Back to library",
  },
  app: {
    initFailedTitle: "Microflow service initialization failed",
    initFailedDescription:
      "Unable to create the microflow adapter bundle. Check apiBaseUrl, adapter mode, workspaceId, and tenantId, then refresh.",
    workspaceIdLabel: "workspaceId",
    appIdLabel: "appId",
    microflowMissingTitle: "Microflow resource not found or deleted",
    microflowTabMissingIdTitle: "Microflow workbench tab is missing microflowId",
    microflowMissingDescription:
      "No fake resource will be created. Refresh resources or reopen a real microflow from App Explorer.",
  },
  index: {
    workspaceLabel: "Workspace",
    sampleAppTitle: "Procurement Approval (Sample)",
    sampleAppDescription: "Procurement approval workflow · Domain Model · Microflow · Workflow",
    createAppInProgress: "Create app is under development",
    createAppButton: "+ Create App",
    footer: "Mendix Studio Core · Atlas Security Platform · v0.0.0",
    noAppTitle: "Select an app to open",
    noAppDescription: "Enter an existing appId in this workspace to open Mendix Studio. Create-app is under development.",
    openAppPlaceholder: "Enter appId",
    openAppButton: "Open app",
    openAppEmptyError: "Please enter an appId.",
    devSampleHint: "Developer sample app (visible in local dev builds only)",
  },
  editorPage: {
    notFoundTitle: "Microflow not found",
    forbiddenTitle: "No permission to access this microflow",
    conflictTitle: "Microflow version conflict",
    serviceErrorTitle: "Microflow service error",
    serviceNotConfigured: "Microflow service is not configured.",
    emptyTitle: "Microflow not found",
    emptyDescription: "The resource may have been deleted or is not visible in this workspace.",
    httpOnlyTitle: "Microflow editor only supports HTTP mode",
    httpOnlyDescription: "Production path does not allow local or mock adapters. Switch to HTTP adapter and retry.",
    adapterNotConfiguredTitle: "Microflow service is not configured",
    adapterNotConfiguredDescription: "Configure the HTTP adapter apiBaseUrl and retry.",
    saveSuccess: "Microflow saved",
  },
  createModal: {
    networkUnavailable: "Microflow service is unavailable. Check network or backend service.",
    unauthorized: "Session expired. Please sign in again.",
    forbidden: "Current account is not allowed to create microflows.",
    duplicated: "A microflow with the same name already exists.",
    validationFailed: "Microflow validation failed. Please check inputs.",
    serverError: "Microflow service error. Contact administrator.",
    fallbackError: "Microflow service error.",
    invalidNameRequired: "Name is required.",
    invalidNamePattern: "Name must start with a letter and contain only letters, numbers, and underscores.",
    invalidNameDuplicated: "A microflow with the same name already exists.",
    missingModuleId: "moduleId is required.",
    missingModuleContext: "Missing module context. Unable to create microflow.",
    invalidParameterName: "Invalid parameter name format",
    duplicatedParameterName: "Parameter names must be unique",
    invalidUrlPath: "URL path must start with /",
    formInvalid: "Please fix form errors first.",
    submitSuccess: "Microflow created",
    retryHint: "This error is retryable. Please try again later.",
    retryableHint: "This error is retryable. Please try again later.",
    title: "Create microflow",
    createButton: "Create",
  },
};

function readLocaleFromStorage(): MendixStudioLocale {
  if (typeof window === "undefined") {
    return "zh-CN";
  }
  const value = window.localStorage.getItem("atlas_locale");
  return value === "en-US" ? "en-US" : "zh-CN";
}

export function resolveMendixStudioLocale(locale?: string): MendixStudioLocale {
  if (locale === "en-US" || locale === "zh-CN") {
    return locale;
  }
  return readLocaleFromStorage();
}

export function getMendixStudioCopy(locale?: string): MendixStudioCopy {
  return resolveMendixStudioLocale(locale) === "en-US" ? enUS : zhCN;
}
