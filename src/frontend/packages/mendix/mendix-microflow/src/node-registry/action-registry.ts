import type {
  MicroflowActionCategory,
  MicroflowActionKind,
  MicroflowActivityType,
  MicroflowNodeAvailability
} from "../schema/types";

export interface MicroflowActionRegistryEntry {
  kind: MicroflowActionKind;
  legacyActivityType: MicroflowActivityType;
  officialType: string;
  title: string;
  titleZh: string;
  category: MicroflowActionCategory;
  iconKey: string;
  availability: MicroflowNodeAvailability;
  createsActionActivity: true;
}

function action(
  kind: MicroflowActionKind,
  legacyActivityType: MicroflowActivityType,
  officialType: string,
  title: string,
  titleZh: string,
  category: MicroflowActionCategory,
  availability: MicroflowNodeAvailability = "supported"
): MicroflowActionRegistryEntry {
  return {
    kind,
    legacyActivityType,
    officialType,
    title,
    titleZh,
    category,
    iconKey: kind,
    availability,
    createsActionActivity: true
  };
}

export const defaultMicroflowActionRegistry: MicroflowActionRegistryEntry[] = [
  action("retrieve", "objectRetrieve", "Microflows$RetrieveAction", "Retrieve", "检索对象", "object"),
  action("createObject", "objectCreate", "Microflows$CreateObjectAction", "Create Object", "创建对象", "object"),
  action("changeMembers", "objectChange", "Microflows$ChangeMembersAction", "Change Members", "修改成员", "object"),
  action("commit", "objectCommit", "Microflows$CommitAction", "Commit", "提交", "object"),
  action("delete", "objectDelete", "Microflows$DeleteAction", "Delete", "删除", "object"),
  action("rollback", "objectRollback", "Microflows$RollbackAction", "Rollback", "回滚", "object"),
  action("cast", "objectCast", "Microflows$CastAction", "Cast", "转换对象", "object"),
  action("aggregateList", "listAggregate", "Microflows$AggregateListAction", "Aggregate List", "列表聚合", "list"),
  action("createList", "listCreate", "Microflows$CreateListAction", "Create List", "创建列表", "list"),
  action("changeList", "listChange", "Microflows$ChangeListAction", "Change List", "修改列表", "list"),
  action("listOperation", "listOperation", "Microflows$ListOperationAction", "List Operation", "列表操作", "list"),
  action("callMicroflow", "callMicroflow", "Microflows$MicroflowCallAction", "Call Microflow", "调用微流", "call"),
  action("callJavaAction", "callJavaAction", "Microflows$JavaActionCallAction", "Call Java Action", "调用 Java 动作", "call"),
  action("callJavaScriptAction", "callJavaScriptAction", "Microflows$JavaScriptActionCallAction", "Call JavaScript Action", "调用 JavaScript 动作", "call", "nanoflowOnlyDisabled"),
  action("callNanoflow", "callNanoflow", "Microflows$NanoflowCallAction", "Call Nanoflow", "调用纳流", "client", "nanoflowOnlyDisabled"),
  action("createVariable", "variableCreate", "Microflows$CreateVariableAction", "Create Variable", "创建变量", "variable"),
  action("changeVariable", "variableChange", "Microflows$ChangeVariableAction", "Change Variable", "修改变量", "variable"),
  action("closePage", "closePage", "Microflows$ClosePageAction", "Close Page", "关闭页面", "client"),
  action("downloadFile", "downloadFile", "Microflows$DownloadFileAction", "Download File", "下载文件", "client"),
  action("showHomePage", "showHomePage", "Microflows$ShowHomePageAction", "Show Home Page", "显示首页", "client"),
  action("showMessage", "showMessage", "Microflows$ShowMessageAction", "Show Message", "显示消息", "client"),
  action("showPage", "showPage", "Microflows$ShowPageAction", "Show Page", "显示页面", "client"),
  action("validationFeedback", "validationFeedback", "Microflows$ValidationFeedbackAction", "Validation Feedback", "验证反馈", "client"),
  action("synchronize", "synchronize", "Microflows$SynchronizeAction", "Synchronize", "同步", "client", "nanoflowOnlyDisabled"),
  action("restCall", "callRest", "Microflows$RestCallAction", "Call REST", "调用 REST", "integration"),
  action("webServiceCall", "callWebService", "Microflows$WebServiceCallAction", "Call Web Service", "调用 Web Service", "integration"),
  action("importXml", "importWithMapping", "Microflows$ImportXmlAction", "Import XML", "导入 XML", "integration"),
  action("exportXml", "exportWithMapping", "Microflows$ExportXmlAction", "Export XML", "导出 XML", "integration"),
  action("callExternalAction", "callExternalAction", "Microflows$CallExternalAction", "Call External Action", "调用外部动作", "integration"),
  action("restOperationCall", "sendRestRequestBeta", "Microflows$RestOperationCallAction", "REST Operation Call", "REST 操作调用", "integration", "beta"),
  action("logMessage", "logMessage", "Microflows$LogMessageAction", "Log Message", "记录日志", "logging"),
  action("generateDocument", "generateDocument", "Microflows$GenerateDocumentAction", "Generate Document", "生成文档", "documentGeneration", "deprecated"),
  action("metric", "counter", "Microflows$MetricAction", "Metric", "指标", "metrics"),
  action("mlModelCall", "callMlModel", "Microflows$MLModelCallAction", "Call ML Model", "调用 ML 模型", "mlKit"),
  action("workflowAction", "callWorkflow", "Microflows$WorkflowAction", "Workflow Action", "工作流动作", "workflow"),
  action("externalObjectAction", "sendExternalObject", "Microflows$ExternalObjectAction", "External Object Action", "外部对象动作", "externalObject")
];

export const microflowActionRegistryByKind = new Map(defaultMicroflowActionRegistry.map(entry => [entry.kind, entry]));
export const microflowActionRegistryByActivityType = new Map(defaultMicroflowActionRegistry.map(entry => [entry.legacyActivityType, entry]));
