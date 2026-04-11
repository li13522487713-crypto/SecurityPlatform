import type { WorkflowNodeTypeKey } from "../types";

export type CoverageBatch = "B1" | "B2" | "B3" | "B4" | "B5" | "B6" | "B7";

export interface NodeCoverageItem {
  type: WorkflowNodeTypeKey;
  batch: CoverageBatch;
  verification: string[];
}

export const NODE_COVERAGE_MATRIX: NodeCoverageItem[] = [
  { type: "Entry", batch: "B1", verification: ["标题可编辑", "变量必填校验", "保存后回填"] },
  { type: "Exit", batch: "B1", verification: ["终止策略必填", "模板保存回填"] },
  { type: "Selector", batch: "B1", verification: ["条件表达式必填", "默认分支可选"] },
  { type: "Loop", batch: "B1", verification: ["输入表达式必填", "循环次数范围校验"] },
  { type: "Batch", batch: "B1", verification: ["并发/分批范围校验", "输入表达式必填"] },
  { type: "Break", batch: "B1", verification: ["触发条件必填"] },
  { type: "Continue", batch: "B1", verification: ["触发条件必填"] },
  { type: "Llm", batch: "B2", verification: ["模型必填", "用户提示词必填", "参数保存"] },
  { type: "IntentDetector", batch: "B2", verification: ["提示词必填", "输出字段必填"] },
  { type: "QuestionAnswer", batch: "B2", verification: ["提示词必填", "输出字段必填"] },
  { type: "CodeRunner", batch: "B2", verification: ["语言必填", "代码必填"] },
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
  { type: "Agent", batch: "B2", verification: ["智能体配置保存"] },
  { type: "Comment", batch: "B7", verification: ["注释内容保存"] }
];

