export type WorkflowCategoryKey =
  | "featured"
  | "logic"
  | "io"
  | "memory"
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
  "featured",
  "logic",
  "io",
  "database",
  "knowledge",
  "memory",
  "conversation"
];

export const WORKFLOW_NODE_CATALOG: WorkflowNodeCatalogItem[] = [
  { type: "Llm", titleKey: "wfUi.nodeTypes.Llm", category: "featured", color: "#111827", iconText: "AI" },
  { type: "Plugin", titleKey: "wfUi.nodeTypes.Plugin", category: "featured", color: "#C026D3", iconText: "PL" },
  { type: "SubWorkflow", titleKey: "wfUi.nodeTypes.SubWorkflow", category: "featured", color: "#22C55E", iconText: "WF" },
  { type: "Comment", titleKey: "wfUi.nodeTypes.Comment", category: "featured", color: "#9CA3AF", iconText: "CM" },
  { type: "Selector", titleKey: "wfUi.nodeTypes.Selector", category: "logic", color: "#14B8A6", iconText: "IF" },
  { type: "Loop", titleKey: "wfUi.nodeTypes.Loop", category: "logic", color: "#14B8A6", iconText: "LP" },
  { type: "CodeRunner", titleKey: "wfUi.nodeTypes.CodeRunner", category: "logic", color: "#14B8A6", iconText: "</>" },
  { type: "VariableAggregator", titleKey: "wfUi.nodeTypes.VariableAggregator", category: "logic", color: "#14B8A6", iconText: "VA" },
  { type: "IntentDetector", titleKey: "wfUi.nodeTypes.IntentDetector", category: "logic", color: "#14B8A6", iconText: "IR" },
  { type: "Batch", titleKey: "wfUi.nodeTypes.Batch", category: "logic", color: "#14B8A6", iconText: "BT" },
  { type: "Break", titleKey: "wfUi.nodeTypes.Break", category: "logic", color: "#14B8A6", iconText: "BK" },
  { type: "Continue", titleKey: "wfUi.nodeTypes.Continue", category: "logic", color: "#14B8A6", iconText: "CT" },
  { type: "TextProcessor", titleKey: "wfUi.nodeTypes.TextProcessor", category: "logic", color: "#14B8A6", iconText: "TP" },
  { type: "JsonSerialization", titleKey: "wfUi.nodeTypes.JsonSerialization", category: "logic", color: "#14B8A6", iconText: "JS" },
  { type: "JsonDeserialization", titleKey: "wfUi.nodeTypes.JsonDeserialization", category: "logic", color: "#14B8A6", iconText: "JD" },
  { type: "InputReceiver", titleKey: "wfUi.nodeTypes.InputReceiver", category: "io", color: "#6366F1", iconText: "IN" },
  { type: "OutputEmitter", titleKey: "wfUi.nodeTypes.OutputEmitter", category: "io", color: "#6366F1", iconText: "OT" },
  { type: "DatabaseCustomSql", titleKey: "wfUi.nodeTypes.DatabaseCustomSql", category: "database", color: "#F97316", iconText: "SQL" },
  { type: "DatabaseQuery", titleKey: "wfUi.nodeTypes.DatabaseQuery", category: "database", color: "#F97316", iconText: "Q" },
  { type: "DatabaseInsert", titleKey: "wfUi.nodeTypes.DatabaseInsert", category: "database", color: "#F97316", iconText: "+" },
  { type: "DatabaseUpdate", titleKey: "wfUi.nodeTypes.DatabaseUpdate", category: "database", color: "#F97316", iconText: "UP" },
  { type: "DatabaseDelete", titleKey: "wfUi.nodeTypes.DatabaseDelete", category: "database", color: "#F97316", iconText: "DL" },
  { type: "AssignVariable", titleKey: "wfUi.nodeTypes.AssignVariable", category: "knowledge", color: "#F59E0B", iconText: "AS" },
  { type: "KnowledgeRetriever", titleKey: "wfUi.nodeTypes.KnowledgeRetriever", category: "knowledge", color: "#F59E0B", iconText: "KR" },
  { type: "KnowledgeIndexer", titleKey: "wfUi.nodeTypes.KnowledgeIndexer", category: "knowledge", color: "#F59E0B", iconText: "KW" },
  { type: "KnowledgeDeleter", titleKey: "wfUi.nodeTypes.KnowledgeDeleter", category: "knowledge", color: "#F59E0B", iconText: "KD" },
  { type: "Agent", titleKey: "wfUi.nodeTypes.Agent", category: "memory", color: "#0EA5E9", iconText: "AG" },
  { type: "QuestionAnswer", titleKey: "wfUi.nodeTypes.QuestionAnswer", category: "memory", color: "#0EA5E9", iconText: "QA" },
  {
    type: "VariableAssignerWithinLoop",
    titleKey: "wfUi.nodeTypes.VariableAssignerWithinLoop",
    category: "memory",
    color: "#06B6D4",
    iconText: "AL"
  },
  { type: "Ltm", titleKey: "wfUi.nodeTypes.Ltm", category: "memory", color: "#0EA5E9", iconText: "LT" },
  { type: "HttpRequester", titleKey: "wfUi.nodeTypes.HttpRequester", category: "conversation", color: "#EC4899", iconText: "HT" },
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

