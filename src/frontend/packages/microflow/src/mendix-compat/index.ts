import type { MicroflowActivityType, MicroflowEdgeType, MicroflowNodeCategory, MicroflowNodeKind } from "../schema/types";

export type MendixMicroflowConcept =
  | "Event"
  | "Decision"
  | "Merge"
  | "Loop"
  | "Parameter"
  | "Annotation"
  | "ObjectActivity"
  | "ListActivity"
  | "VariableActivity"
  | "CallActivity"
  | "ClientActivity"
  | "IntegrationActivity"
  | "LoggingActivity";

export interface MendixNodeCategoryMapping {
  microflowType: MicroflowNodeKind;
  activityType?: MicroflowActivityType;
  mendixConcept: MendixMicroflowConcept;
  category: MicroflowNodeCategory;
  importExportKey: string;
}

export interface MendixFlowMapping {
  edgeType: MicroflowEdgeType;
  importExportKey: "SequenceFlow" | "ErrorFlow" | "AnnotationFlow";
  visualStyle: "solid" | "error" | "dashed";
}

export const mendixNodeCategoryMappings: MendixNodeCategoryMapping[] = [
  { microflowType: "startEvent", mendixConcept: "Event", category: "event", importExportKey: "StartEvent" },
  { microflowType: "endEvent", mendixConcept: "Event", category: "event", importExportKey: "EndEvent" },
  { microflowType: "errorEvent", mendixConcept: "Event", category: "event", importExportKey: "ErrorEvent" },
  { microflowType: "breakEvent", mendixConcept: "Event", category: "event", importExportKey: "BreakEvent" },
  { microflowType: "continueEvent", mendixConcept: "Event", category: "event", importExportKey: "ContinueEvent" },
  { microflowType: "decision", mendixConcept: "Decision", category: "decision", importExportKey: "ExclusiveSplit" },
  { microflowType: "merge", mendixConcept: "Merge", category: "merge", importExportKey: "Merge" },
  { microflowType: "loop", mendixConcept: "Loop", category: "loop", importExportKey: "LoopedActivity" },
  { microflowType: "parameter", mendixConcept: "Parameter", category: "parameter", importExportKey: "Parameter" },
  { microflowType: "annotation", mendixConcept: "Annotation", category: "annotation", importExportKey: "Annotation" },
  { microflowType: "activity", activityType: "objectCreate", mendixConcept: "ObjectActivity", category: "activity", importExportKey: "CreateObjectAction" },
  { microflowType: "activity", activityType: "objectChange", mendixConcept: "ObjectActivity", category: "activity", importExportKey: "ChangeObjectAction" },
  { microflowType: "activity", activityType: "objectCommit", mendixConcept: "ObjectActivity", category: "activity", importExportKey: "CommitAction" },
  { microflowType: "activity", activityType: "objectDelete", mendixConcept: "ObjectActivity", category: "activity", importExportKey: "DeleteAction" },
  { microflowType: "activity", activityType: "objectRetrieve", mendixConcept: "ObjectActivity", category: "activity", importExportKey: "RetrieveAction" },
  { microflowType: "activity", activityType: "objectRollback", mendixConcept: "ObjectActivity", category: "activity", importExportKey: "RollbackAction" },
  { microflowType: "activity", activityType: "variableCreate", mendixConcept: "VariableActivity", category: "activity", importExportKey: "CreateVariableAction" },
  { microflowType: "activity", activityType: "variableChange", mendixConcept: "VariableActivity", category: "activity", importExportKey: "ChangeVariableAction" },
  { microflowType: "activity", activityType: "callMicroflow", mendixConcept: "CallActivity", category: "activity", importExportKey: "MicroflowCallAction" },
  { microflowType: "activity", activityType: "callRest", mendixConcept: "IntegrationActivity", category: "activity", importExportKey: "RestCallAction" },
  { microflowType: "activity", activityType: "logMessage", mendixConcept: "LoggingActivity", category: "activity", importExportKey: "LogMessageAction" },
  { microflowType: "activity", activityType: "showPage", mendixConcept: "ClientActivity", category: "activity", importExportKey: "ShowPageAction" },
  { microflowType: "activity", activityType: "closePage", mendixConcept: "ClientActivity", category: "activity", importExportKey: "ClosePageAction" }
];

export const mendixFlowMappings: MendixFlowMapping[] = [
  { edgeType: "sequence", importExportKey: "SequenceFlow", visualStyle: "solid" },
  { edgeType: "error", importExportKey: "ErrorFlow", visualStyle: "error" },
  { edgeType: "annotation", importExportKey: "AnnotationFlow", visualStyle: "dashed" }
];
