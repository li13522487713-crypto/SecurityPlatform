import type { WorkflowNodeTypeKey } from "../types";

export type CoverageBatch = "B1" | "B2" | "B3" | "B4" | "B5" | "B6" | "B7";

export interface NodeCoverageItem {
  type: WorkflowNodeTypeKey;
  batch: CoverageBatch;
  verification: string[];
}

export const NODE_COVERAGE_MATRIX: NodeCoverageItem[] = [
  { type: "Entry", batch: "B1", verification: ["entryVariable 必填", "标题可编辑", "保存后回填"] },
  { type: "Exit", batch: "B1", verification: ["exitTerminateMode 必填", "模板保存回填"] },
  { type: "Selector", batch: "B1", verification: ["condition / conditions 二选一有效", "logic 可回填"] },
  { type: "Loop", batch: "B1", verification: ["mode + maxIterations 必填", "循环字段保存回填"] },
  { type: "Batch", batch: "B1", verification: ["concurrentSize/batchSize 校验", "inputArrayPath 必填"] },
  { type: "Break", batch: "B1", verification: ["reason 必填"] },
  { type: "Continue", batch: "B1", verification: ["可保存备注"] },
  { type: "Llm", batch: "B2", verification: ["provider/model/prompt 必填", "temperature/maxTokens 保存"] },
  { type: "IntentDetector", batch: "B2", verification: ["input/model 必填", "intents 标签可保存"] },
  { type: "QuestionAnswer", batch: "B2", verification: ["question/answerPath 必填", "fixedChoices 可保存"] },
  { type: "CodeRunner", batch: "B2", verification: ["code 必填", "language/code/outputKey 可回填"] },
  { type: "TextProcessor", batch: "B3", verification: ["输入输出字段保存"] },
  { type: "JsonSerialization", batch: "B3", verification: ["输入输出字段保存"] },
  { type: "JsonDeserialization", batch: "B3", verification: ["输入输出字段保存"] },
  { type: "VariableAggregator", batch: "B3", verification: ["目标变量必填"] },
  { type: "AssignVariable", batch: "B3", verification: ["目标变量必填"] },
  { type: "VariableAssignerWithinLoop", batch: "B3", verification: ["目标变量必填"] },
  { type: "Plugin", batch: "B4", verification: ["资源ID必填", "输入映射保存"] },
  { type: "HttpRequester", batch: "B4", verification: ["Method/URL必填", "Headers保存"] },
  { type: "SubWorkflow", batch: "B4", verification: ["子流程ID必填", "批处理开关保存"] },
  { type: "InputReceiver", batch: "B4", verification: ["变量名必填"] },
  { type: "OutputEmitter", batch: "B4", verification: ["变量名必填"] },
  { type: "KnowledgeRetriever", batch: "B5", verification: ["知识库ID必填"] },
  { type: "KnowledgeIndexer", batch: "B5", verification: ["知识库ID必填"] },
  { type: "KnowledgeDeleter", batch: "B5", verification: ["知识库ID必填"] },
  { type: "Ltm", batch: "B5", verification: ["知识库配置保存"] },
  { type: "DatabaseQuery", batch: "B6", verification: ["数据源ID必填", "查询条件保存"] },
  { type: "DatabaseInsert", batch: "B6", verification: ["数据源ID必填", "写入字段保存"] },
  { type: "DatabaseUpdate", batch: "B6", verification: ["数据源ID必填", "更新字段保存"] },
  { type: "DatabaseDelete", batch: "B6", verification: ["数据源ID必填", "删除条件保存"] },
  { type: "DatabaseCustomSql", batch: "B6", verification: ["SQL必填"] },
  { type: "CreateConversation", batch: "B7", verification: ["会话参数保存"] },
  { type: "ConversationList", batch: "B7", verification: ["会话查询保存"] },
  { type: "ConversationUpdate", batch: "B7", verification: ["会话更新保存"] },
  { type: "ConversationDelete", batch: "B7", verification: ["会话删除保存"] },
  { type: "ConversationHistory", batch: "B7", verification: ["历史查询保存"] },
  { type: "ClearConversationHistory", batch: "B7", verification: ["清理参数保存"] },
  { type: "MessageList", batch: "B7", verification: ["消息查询保存"] },
  { type: "CreateMessage", batch: "B7", verification: ["消息创建保存"] },
  { type: "EditMessage", batch: "B7", verification: ["消息编辑保存"] },
  { type: "DeleteMessage", batch: "B7", verification: ["消息删除保存"] },
  { type: "Agent", batch: "B2", verification: ["agentId/message 必填", "enableRag 与会话参数可保存"] },
  { type: "Comment", batch: "B7", verification: ["注释内容保存"] }
];

