import type { MicroflowActivityType, MicroflowEdgeType, MicroflowNodeCategory, MicroflowNodeType } from "../schema/types";

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
  microflowType: MicroflowNodeType;
  activityType?: MicroflowActivityType;
  mendixConcept: MendixMicroflowConcept;
  category: MicroflowNodeCategory;
  importExportKey: string;
}

export interface MendixFlowMapping {
  edgeType: MicroflowEdgeType;
  importExportKey: "SequenceFlow" | "DecisionConditionFlow" | "ObjectTypeConditionFlow" | "ErrorFlow" | "AnnotationFlow";
  visualStyle: "solid" | "error" | "dashed";
}

export const mendixNodeCategoryMappings: MendixNodeCategoryMapping[] = [
  { microflowType: "startEvent", mendixConcept: "Event", category: "events", importExportKey: "StartEvent" },
  { microflowType: "endEvent", mendixConcept: "Event", category: "events", importExportKey: "EndEvent" },
  { microflowType: "errorEvent", mendixConcept: "Event", category: "events", importExportKey: "ErrorEvent" },
  { microflowType: "breakEvent", mendixConcept: "Event", category: "events", importExportKey: "BreakEvent" },
  { microflowType: "continueEvent", mendixConcept: "Event", category: "events", importExportKey: "ContinueEvent" },
  { microflowType: "decision", mendixConcept: "Decision", category: "decisions", importExportKey: "ExclusiveSplit" },
  { microflowType: "objectTypeDecision", mendixConcept: "Decision", category: "decisions", importExportKey: "InheritanceSplit" },
  { microflowType: "merge", mendixConcept: "Merge", category: "decisions", importExportKey: "Merge" },
  { microflowType: "loop", mendixConcept: "Loop", category: "loop", importExportKey: "LoopedActivity" },
  { microflowType: "parameter", mendixConcept: "Parameter", category: "parameters", importExportKey: "Parameter" },
  { microflowType: "annotation", mendixConcept: "Annotation", category: "annotations", importExportKey: "Annotation" },
  { microflowType: "activity", activityType: "objectCreate", mendixConcept: "ObjectActivity", category: "activities", importExportKey: "CreateObjectAction" },
  { microflowType: "activity", activityType: "objectChange", mendixConcept: "ObjectActivity", category: "activities", importExportKey: "ChangeObjectAction" },
  { microflowType: "activity", activityType: "objectCommit", mendixConcept: "ObjectActivity", category: "activities", importExportKey: "CommitAction" },
  { microflowType: "activity", activityType: "objectDelete", mendixConcept: "ObjectActivity", category: "activities", importExportKey: "DeleteAction" },
  { microflowType: "activity", activityType: "objectRetrieve", mendixConcept: "ObjectActivity", category: "activities", importExportKey: "RetrieveAction" },
  { microflowType: "activity", activityType: "objectRollback", mendixConcept: "ObjectActivity", category: "activities", importExportKey: "RollbackAction" },
  { microflowType: "activity", activityType: "variableCreate", mendixConcept: "VariableActivity", category: "activities", importExportKey: "CreateVariableAction" },
  { microflowType: "activity", activityType: "variableChange", mendixConcept: "VariableActivity", category: "activities", importExportKey: "ChangeVariableAction" },
  { microflowType: "activity", activityType: "callMicroflow", mendixConcept: "CallActivity", category: "activities", importExportKey: "MicroflowCallAction" },
  { microflowType: "activity", activityType: "callRest", mendixConcept: "IntegrationActivity", category: "activities", importExportKey: "RestCallAction" },
  { microflowType: "activity", activityType: "logMessage", mendixConcept: "LoggingActivity", category: "activities", importExportKey: "LogMessageAction" },
  { microflowType: "activity", activityType: "showPage", mendixConcept: "ClientActivity", category: "activities", importExportKey: "ShowPageAction" },
  { microflowType: "activity", activityType: "closePage", mendixConcept: "ClientActivity", category: "activities", importExportKey: "ClosePageAction" }
];

export const mendixFlowMappings: MendixFlowMapping[] = [
  { edgeType: "sequence", importExportKey: "SequenceFlow", visualStyle: "solid" },
  { edgeType: "decisionCondition", importExportKey: "DecisionConditionFlow", visualStyle: "solid" },
  { edgeType: "objectTypeCondition", importExportKey: "ObjectTypeConditionFlow", visualStyle: "solid" },
  { edgeType: "errorHandler", importExportKey: "ErrorFlow", visualStyle: "error" },
  { edgeType: "annotation", importExportKey: "AnnotationFlow", visualStyle: "dashed" }
];
