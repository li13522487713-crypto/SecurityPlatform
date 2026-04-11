export type WorkflowCategoryKey =
  | "flowControl"
  | "ai"
  | "dataProcess"
  | "external"
  | "knowledge"
  | "database"
  | "conversation";

export interface WorkflowNodeCatalogItem {
  type: string;
  titleKey: string;
  category: WorkflowCategoryKey;
  color: string;
  iconText: string;
}

export const WORKFLOW_CATEGORY_ORDER: WorkflowCategoryKey[] = [
  "flowControl",
  "ai",
  "dataProcess",
  "external",
  "knowledge",
  "database",
  "conversation"
];

export const WORKFLOW_NODE_CATALOG: WorkflowNodeCatalogItem[] = [
  { type: "Entry", titleKey: "wfUi.nodeTypes.Entry", category: "flowControl", color: "#6366F1", iconText: "ST" },
  { type: "Exit", titleKey: "wfUi.nodeTypes.Exit", category: "flowControl", color: "#6366F1", iconText: "ED" },
  { type: "Selector", titleKey: "wfUi.nodeTypes.Selector", category: "flowControl", color: "#6366F1", iconText: "IF" },
  { type: "Loop", titleKey: "wfUi.nodeTypes.Loop", category: "flowControl", color: "#6366F1", iconText: "LP" },
  { type: "Batch", titleKey: "wfUi.nodeTypes.Batch", category: "flowControl", color: "#6366F1", iconText: "BT" },
  { type: "Break", titleKey: "wfUi.nodeTypes.Break", category: "flowControl", color: "#6366F1", iconText: "BK" },
  { type: "Continue", titleKey: "wfUi.nodeTypes.Continue", category: "flowControl", color: "#6366F1", iconText: "CT" },
  { type: "Llm", titleKey: "wfUi.nodeTypes.Llm", category: "ai", color: "#8B5CF6", iconText: "AI" },
  { type: "IntentDetector", titleKey: "wfUi.nodeTypes.IntentDetector", category: "ai", color: "#8B5CF6", iconText: "ID" },
  { type: "QuestionAnswer", titleKey: "wfUi.nodeTypes.QuestionAnswer", category: "ai", color: "#8B5CF6", iconText: "QA" },
  { type: "CodeRunner", titleKey: "wfUi.nodeTypes.CodeRunner", category: "dataProcess", color: "#06B6D4", iconText: "CD" },
  { type: "TextProcessor", titleKey: "wfUi.nodeTypes.TextProcessor", category: "dataProcess", color: "#06B6D4", iconText: "TP" },
  { type: "JsonSerialization", titleKey: "wfUi.nodeTypes.JsonSerialization", category: "dataProcess", color: "#06B6D4", iconText: "JS" },
  { type: "JsonDeserialization", titleKey: "wfUi.nodeTypes.JsonDeserialization", category: "dataProcess", color: "#06B6D4", iconText: "JD" },
  { type: "VariableAggregator", titleKey: "wfUi.nodeTypes.VariableAggregator", category: "dataProcess", color: "#06B6D4", iconText: "VA" },
  { type: "AssignVariable", titleKey: "wfUi.nodeTypes.AssignVariable", category: "dataProcess", color: "#06B6D4", iconText: "AS" },
  { type: "Plugin", titleKey: "wfUi.nodeTypes.Plugin", category: "external", color: "#F59E0B", iconText: "PL" },
  { type: "HttpRequester", titleKey: "wfUi.nodeTypes.HttpRequester", category: "external", color: "#F59E0B", iconText: "HT" },
  { type: "SubWorkflow", titleKey: "wfUi.nodeTypes.SubWorkflow", category: "external", color: "#F59E0B", iconText: "SW" },
  { type: "InputReceiver", titleKey: "wfUi.nodeTypes.InputReceiver", category: "external", color: "#F59E0B", iconText: "IN" },
  { type: "OutputEmitter", titleKey: "wfUi.nodeTypes.OutputEmitter", category: "external", color: "#F59E0B", iconText: "OT" },
  { type: "KnowledgeRetriever", titleKey: "wfUi.nodeTypes.KnowledgeRetriever", category: "knowledge", color: "#10B981", iconText: "KR" },
  { type: "KnowledgeIndexer", titleKey: "wfUi.nodeTypes.KnowledgeIndexer", category: "knowledge", color: "#10B981", iconText: "KW" },
  { type: "Ltm", titleKey: "wfUi.nodeTypes.Ltm", category: "knowledge", color: "#10B981", iconText: "LT" },
  { type: "DatabaseQuery", titleKey: "wfUi.nodeTypes.DatabaseQuery", category: "database", color: "#3B82F6", iconText: "DQ" },
  { type: "DatabaseInsert", titleKey: "wfUi.nodeTypes.DatabaseInsert", category: "database", color: "#3B82F6", iconText: "DI" },
  { type: "DatabaseUpdate", titleKey: "wfUi.nodeTypes.DatabaseUpdate", category: "database", color: "#3B82F6", iconText: "DU" },
  { type: "DatabaseDelete", titleKey: "wfUi.nodeTypes.DatabaseDelete", category: "database", color: "#3B82F6", iconText: "DD" },
  { type: "DatabaseCustomSql", titleKey: "wfUi.nodeTypes.DatabaseCustomSql", category: "database", color: "#3B82F6", iconText: "SQL" },
  { type: "CreateConversation", titleKey: "wfUi.nodeTypes.CreateConversation", category: "conversation", color: "#EC4899", iconText: "CC" },
  { type: "ConversationList", titleKey: "wfUi.nodeTypes.ConversationList", category: "conversation", color: "#EC4899", iconText: "CL" },
  { type: "ConversationUpdate", titleKey: "wfUi.nodeTypes.ConversationUpdate", category: "conversation", color: "#EC4899", iconText: "CU" },
  { type: "ConversationDelete", titleKey: "wfUi.nodeTypes.ConversationDelete", category: "conversation", color: "#EC4899", iconText: "CD" },
  { type: "ConversationHistory", titleKey: "wfUi.nodeTypes.ConversationHistory", category: "conversation", color: "#EC4899", iconText: "CH" },
  { type: "ClearConversationHistory", titleKey: "wfUi.nodeTypes.ClearConversationHistory", category: "conversation", color: "#EC4899", iconText: "CR" },
  { type: "MessageList", titleKey: "wfUi.nodeTypes.MessageList", category: "conversation", color: "#EC4899", iconText: "ML" },
  { type: "CreateMessage", titleKey: "wfUi.nodeTypes.CreateMessage", category: "conversation", color: "#EC4899", iconText: "CM" },
  { type: "EditMessage", titleKey: "wfUi.nodeTypes.EditMessage", category: "conversation", color: "#EC4899", iconText: "EM" },
  { type: "DeleteMessage", titleKey: "wfUi.nodeTypes.DeleteMessage", category: "conversation", color: "#EC4899", iconText: "DM" }
];

