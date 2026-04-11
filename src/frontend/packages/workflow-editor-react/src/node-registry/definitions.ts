import type { WorkflowNodeTypeKey } from "../types";
import type { FormFieldSchema, FormSectionSchema, NodeDefinition } from "./types";

const FALLBACK_TEXT_FIELD: FormFieldSchema = {
  key: "rawConfig",
  label: "配置 JSON",
  kind: "json",
  path: "rawConfig",
  rows: 8
};

const FLOW_CONTROL_TYPES: WorkflowNodeTypeKey[] = ["Entry", "Exit", "Selector", "Loop", "Batch", "Break", "Continue"];
const AI_TYPES: WorkflowNodeTypeKey[] = ["Llm", "Agent", "IntentDetector", "QuestionAnswer"];
const DATA_TYPES: WorkflowNodeTypeKey[] = [
  "CodeRunner",
  "TextProcessor",
  "JsonSerialization",
  "JsonDeserialization",
  "VariableAggregator",
  "AssignVariable",
  "VariableAssignerWithinLoop"
];
const EXTERNAL_TYPES: WorkflowNodeTypeKey[] = ["Plugin", "HttpRequester", "SubWorkflow", "InputReceiver", "OutputEmitter"];
const KNOWLEDGE_TYPES: WorkflowNodeTypeKey[] = ["KnowledgeRetriever", "KnowledgeIndexer", "KnowledgeDeleter", "Ltm"];
const DATABASE_TYPES: WorkflowNodeTypeKey[] = ["DatabaseQuery", "DatabaseInsert", "DatabaseUpdate", "DatabaseDelete", "DatabaseCustomSql"];

function createStandardSections(type: WorkflowNodeTypeKey): FormSectionSchema[] {
  switch (type) {
    case "Entry":
      return [
        {
          key: "basic",
          title: "开始配置",
          fields: [
            { key: "entryVariable", label: "会话变量", kind: "text", path: "entryVariable", required: true, placeholder: "USER_INPUT" },
            { key: "entryDescription", label: "描述", kind: "textarea", path: "entryDescription", rows: 3 },
            { key: "entryAutoSave", label: "自动保存历史", kind: "switch", path: "entryAutoSaveHistory" }
          ]
        }
      ];
    case "Exit":
      return [
        {
          key: "basic",
          title: "结束配置",
          fields: [
            {
              key: "exitTerminateMode",
              label: "终止策略",
              kind: "select",
              path: "exitTerminateMode",
              required: true,
              options: [
                { value: "return", label: "返回输出" },
                { value: "interrupt", label: "中断执行" }
              ]
            },
            { key: "exitTemplate", label: "输出模板", kind: "variableRefPicker", path: "exitTemplate", rows: 4 },
            { key: "exitStreaming", label: "流式输出", kind: "switch", path: "exitStreaming" }
          ]
        }
      ];
    case "Selector":
      return [
        {
          key: "basic",
          title: "条件判断",
          fields: [
            { key: "selectorCondition", label: "表达式条件", kind: "textarea", path: "condition", rows: 3 },
            {
              key: "selectorLogic",
              label: "条件关系",
              kind: "radioGroup",
              path: "logic",
              options: [
                { value: "and", label: "AND" },
                { value: "or", label: "OR" }
              ]
            },
            { key: "selectorConditions", label: "结构化条件", kind: "conditionBuilder", path: "conditions" }
          ]
        }
      ];
    case "Loop":
      return [
        {
          key: "basic",
          title: "循环配置",
          fields: [
            {
              key: "loopMode",
              label: "循环模式",
              kind: "select",
              path: "mode",
              required: true,
              options: [
                { value: "forEach", label: "数组遍历" },
                { value: "while", label: "条件循环" },
                { value: "count", label: "计数循环" }
              ]
            },
            { key: "loopMaxIterations", label: "最大循环次数", kind: "number", path: "maxIterations", min: 1, max: 10000, required: true },
            { key: "loopCollectionPath", label: "数组变量路径", kind: "text", path: "collectionPath", placeholder: "{{items}}" },
            { key: "loopCondition", label: "循环条件", kind: "text", path: "condition" },
            { key: "loopItemVariable", label: "迭代项变量名", kind: "text", path: "itemVariable", placeholder: "loop_item" },
            { key: "loopIndexVariable", label: "索引变量名", kind: "text", path: "itemIndexVariable", placeholder: "loop_index" },
            { key: "loopBodyNodeKeys", label: "循环体节点键", kind: "textarea", path: "bodyNodeKeys", rows: 2, placeholder: "nodeA,nodeB,nodeC" }
          ]
        }
      ];
    case "Batch":
      return [
        {
          key: "basic",
          title: "批处理配置",
          fields: [
            { key: "batchConcurrentSize", label: "并发数", kind: "number", path: "concurrentSize", min: 1, max: 64, required: true },
            { key: "batchSize", label: "批大小", kind: "number", path: "batchSize", min: 1, max: 10000, required: true },
            { key: "batchInputArrayPath", label: "输入数组路径", kind: "text", path: "inputArrayPath", required: true, placeholder: "{{items}}" },
            { key: "batchItemVariable", label: "迭代项变量名", kind: "text", path: "itemVariable", placeholder: "batch_item" },
            { key: "batchItemIndexVariable", label: "索引变量名", kind: "text", path: "itemIndexVariable", placeholder: "batch_item_index" },
            { key: "batchOutputKey", label: "输出变量", kind: "text", path: "outputKey", required: true, placeholder: "batch_results" }
          ]
        }
      ];
    case "Break":
      return [
        {
          key: "basic",
          title: "中断配置",
          fields: [{ key: "breakReason", label: "中断原因", kind: "text", path: "reason", required: true }]
        }
      ];
    case "Continue":
      return [
        {
          key: "basic",
          title: "继续配置",
          fields: [{ key: "continueRemark", label: "说明", kind: "textarea", path: "remark", rows: 2 }]
        }
      ];
    case "Llm":
      return [
        {
          key: "basic",
          title: "模型配置",
          fields: [
            { key: "llmProvider", label: "模型提供商", kind: "text", path: "provider", required: true, placeholder: "openai" },
            { key: "llmModel", label: "模型", kind: "text", path: "model", required: true, placeholder: "gpt-5.4-medium" },
            { key: "llmPrompt", label: "提示词", kind: "variableRefPicker", path: "prompt", rows: 6, required: true, placeholder: "{{input.message}}" },
            { key: "llmOutputKey", label: "输出字段", kind: "text", path: "outputKey", required: true, placeholder: "llm_output" }
          ]
        },
        {
          key: "advanced",
          title: "高级参数",
          advanced: true,
          fields: [
            { key: "llmSystemPrompt", label: "系统提示词", kind: "variableRefPicker", path: "systemPrompt", rows: 3 },
            { key: "llmTemperature", label: "Temperature", kind: "slider", path: "temperature", min: 0, max: 2, step: 0.1 },
            { key: "llmMaxTokens", label: "最大 Token", kind: "number", path: "maxTokens", min: 1, max: 32000 },
            { key: "llmStream", label: "流式输出", kind: "switch", path: "stream" }
          ]
        }
      ];
    case "Agent":
      return [
        {
          key: "basic",
          title: "智能体配置",
          fields: [
            { key: "agentId", label: "Agent ID", kind: "text", path: "agentId", required: true },
            { key: "agentMessage", label: "消息模板", kind: "variableRefPicker", path: "message", rows: 4, required: true },
            { key: "agentConversationId", label: "会话 ID", kind: "text", path: "conversationId" },
            { key: "agentUserId", label: "用户 ID", kind: "text", path: "userId" },
            { key: "agentEnableRag", label: "启用 RAG", kind: "switch", path: "enableRag" },
            { key: "agentOutputKey", label: "输出字段", kind: "text", path: "outputKey", placeholder: "agent_output" }
          ]
        }
      ];
    case "IntentDetector":
      return [
        {
          key: "basic",
          title: "意图识别配置",
          fields: [
            { key: "intentInput", label: "输入表达式", kind: "text", path: "input", required: true, placeholder: "{{text}}" },
            { key: "intentProvider", label: "模型提供商", kind: "text", path: "provider", required: true },
            { key: "intentModel", label: "模型", kind: "text", path: "model", required: true },
            { key: "intentSystemPrompt", label: "系统提示词", kind: "variableRefPicker", path: "systemPrompt", rows: 3 },
            { key: "intentTemperature", label: "Temperature", kind: "slider", path: "temperature", min: 0, max: 2, step: 0.1 },
            { key: "intentIntents", label: "意图列表", kind: "tagInput", path: "intents" }
          ]
        }
      ];
    case "QuestionAnswer":
      return [
        {
          key: "basic",
          title: "问答配置",
          fields: [
            { key: "qaQuestion", label: "问题模板", kind: "variableRefPicker", path: "question", rows: 3, required: true },
            {
              key: "qaAnswerType",
              label: "回答类型",
              kind: "select",
              path: "answerType",
              options: [
                { value: "free_text", label: "自由文本" },
                { value: "single_choice", label: "单选" }
              ]
            },
            { key: "qaFixedChoices", label: "固定选项", kind: "arrayEditor", path: "fixedChoices" },
            { key: "qaMaxAnswerCount", label: "最大回答次数", kind: "number", path: "maxAnswerCount", min: 1, max: 20 },
            { key: "qaAnswerPath", label: "答案输出路径", kind: "text", path: "answerPath", required: true, placeholder: "answer" }
          ]
        }
      ];
    case "CodeRunner":
      return [
        {
          key: "basic",
          title: "代码执行",
          fields: [
            {
              key: "codeLanguage",
              label: "运行语言",
              kind: "select",
              path: "language",
              required: true,
              options: [
                { value: "javascript", label: "JavaScript" },
                { value: "python", label: "Python" }
              ]
            },
            { key: "codeBody", label: "代码", kind: "codeEditor", path: "code", rows: 12, required: true, languagePath: "language" },
            { key: "codeOutputKey", label: "输出变量", kind: "text", path: "outputKey", required: true, placeholder: "code_output" }
          ]
        }
      ];
    case "TextProcessor":
      return [
        {
          key: "basic",
          title: "文本处理",
          fields: [
            { key: "textTemplate", label: "模板", kind: "variableRefPicker", path: "template", rows: 4, required: true },
            { key: "textOutputKey", label: "输出变量", kind: "text", path: "outputKey", required: true, placeholder: "text_output" }
          ]
        }
      ];
    case "JsonSerialization":
      return [
        {
          key: "basic",
          title: "JSON 序列化",
          fields: [
            { key: "jsonSerializeVariableKeys", label: "变量键列表", kind: "tagInput", path: "variableKeys", required: true },
            { key: "jsonSerializeOutputKey", label: "输出变量", kind: "text", path: "outputKey", required: true, placeholder: "json_output" }
          ]
        }
      ];
    case "JsonDeserialization":
      return [
        {
          key: "basic",
          title: "JSON 反序列化",
          fields: [{ key: "jsonDeserializeInput", label: "输入变量名", kind: "text", path: "inputVariable", required: true }]
        }
      ];
    case "VariableAggregator":
      return [
        {
          key: "basic",
          title: "变量聚合",
          fields: [
            { key: "varAggKeys", label: "变量键列表", kind: "tagInput", path: "variableKeys", required: true },
            { key: "varAggOutput", label: "输出变量", kind: "text", path: "outputKey", required: true, placeholder: "aggregated" }
          ]
        }
      ];
    case "AssignVariable":
    case "VariableAssignerWithinLoop":
      return [
        {
          key: "basic",
          title: "变量赋值",
          fields: [{ key: "assignmentsRaw", label: "赋值表达式", kind: "text", path: "assignments", required: true, placeholder: "a=1;b={{x}}" }]
        }
      ];
    case "Plugin":
      return [
        {
          key: "basic",
          title: "插件配置",
          fields: [
            { key: "pluginId", label: "插件 ID", kind: "text", path: "pluginId", required: true },
            { key: "pluginApiId", label: "API ID", kind: "text", path: "apiId", required: true },
            { key: "pluginInputJson", label: "输入 JSON", kind: "json", path: "inputJson", rows: 6 },
            { key: "pluginOutputKey", label: "输出变量", kind: "text", path: "outputKey", placeholder: "plugin_output" }
          ]
        }
      ];
    case "HttpRequester":
      return [
        {
          key: "basic",
          title: "HTTP 请求",
          fields: [
            {
              key: "httpMethod",
              label: "Method",
              kind: "select",
              path: "method",
              required: true,
              options: [
                { value: "GET", label: "GET" },
                { value: "POST", label: "POST" },
                { value: "PUT", label: "PUT" },
                { value: "PATCH", label: "PATCH" },
                { value: "DELETE", label: "DELETE" }
              ]
            },
            { key: "httpUrl", label: "URL", kind: "text", path: "url", required: true, placeholder: "https://api.example.com" },
            { key: "httpHeaders", label: "Headers", kind: "keyValue", path: "headers" },
            { key: "httpBody", label: "Body", kind: "json", path: "body", rows: 6 },
            { key: "httpTimeout", label: "超时(秒)", kind: "number", path: "timeout", min: 1, max: 600 },
            { key: "httpRetryTimes", label: "重试次数", kind: "number", path: "retryTimes", min: 0, max: 10 }
          ]
        }
      ];
    case "SubWorkflow":
      return [
        {
          key: "basic",
          title: "子工作流",
          fields: [
            { key: "subWorkflowId", label: "工作流 ID", kind: "text", path: "workflowId", required: true },
            { key: "subWorkflowMaxDepth", label: "最大深度", kind: "number", path: "maxDepth", min: 1, max: 20 },
            { key: "subWorkflowInheritVariables", label: "继承变量", kind: "switch", path: "inheritVariables" },
            { key: "subWorkflowMergeOutputs", label: "合并输出", kind: "switch", path: "mergeOutputs" },
            { key: "subWorkflowInputsVariable", label: "输入变量路径", kind: "text", path: "inputsVariable" },
            { key: "subWorkflowOutputKey", label: "输出变量", kind: "text", path: "outputKey", placeholder: "sub_workflow_output" }
          ]
        }
      ];
    case "InputReceiver":
      return [
        {
          key: "basic",
          title: "输入接收",
          fields: [
            { key: "inputPath", label: "输入路径", kind: "text", path: "inputPath", required: true, placeholder: "input.message" },
            { key: "inputOutputSchema", label: "预期输入 Schema", kind: "json", path: "outputSchema", rows: 6 }
          ]
        }
      ];
    case "OutputEmitter":
      return [
        {
          key: "basic",
          title: "中间输出",
          fields: [
            { key: "outputEmitterKey", label: "输出变量", kind: "text", path: "outputKey", required: true, placeholder: "output" },
            { key: "outputEmitterTemplate", label: "输出模板", kind: "variableRefPicker", path: "template", rows: 3 }
          ]
        }
      ];
    case "KnowledgeRetriever":
      return [
        {
          key: "basic",
          title: "知识检索",
          fields: [
            { key: "knowledgeRetrieverIds", label: "知识库 ID 列表", kind: "tagInput", path: "knowledgeIds", required: true },
            { key: "knowledgeRetrieverQuery", label: "查询语句", kind: "variableRefPicker", path: "query", rows: 3, required: true },
            { key: "knowledgeRetrieverTopK", label: "TopK", kind: "number", path: "topK", min: 1, max: 50 },
            { key: "knowledgeRetrieverMinScore", label: "最低分", kind: "slider", path: "minScore", min: 0, max: 1, step: 0.01 }
          ]
        }
      ];
    case "KnowledgeIndexer":
      return [
        {
          key: "basic",
          title: "知识写入",
          fields: [
            { key: "knowledgeIndexerKnowledgeId", label: "知识库 ID", kind: "number", path: "knowledgeId", required: true, min: 1 },
            { key: "knowledgeIndexerFileId", label: "文件 ID", kind: "number", path: "fileId", required: true, min: 1 },
            { key: "knowledgeIndexerFileName", label: "文件名", kind: "text", path: "fileName" },
            { key: "knowledgeIndexerContentType", label: "Content-Type", kind: "text", path: "contentType" },
            { key: "knowledgeIndexerFileSize", label: "文件大小(字节)", kind: "number", path: "fileSizeBytes", min: 0 },
            { key: "knowledgeIndexerChunkSize", label: "切片大小", kind: "number", path: "chunkSize", min: 100, max: 5000 },
            { key: "knowledgeIndexerOverlap", label: "重叠大小", kind: "number", path: "overlap", min: 0, max: 1000 }
          ]
        }
      ];
    case "KnowledgeDeleter":
      return [
        {
          key: "basic",
          title: "知识删除",
          fields: [
            { key: "knowledgeDeleterKnowledgeId", label: "知识库 ID", kind: "number", path: "knowledgeId", required: true, min: 1 },
            { key: "knowledgeDeleterDocumentId", label: "文档 ID", kind: "number", path: "documentId", min: 1 }
          ]
        }
      ];
    case "Ltm":
      return [
        {
          key: "basic",
          title: "长期记忆",
          fields: [
            {
              key: "ltmAction",
              label: "动作",
              kind: "select",
              path: "action",
              required: true,
              options: [
                { value: "read", label: "读取" },
                { value: "write", label: "写入" },
                { value: "delete", label: "删除" }
              ]
            },
            { key: "ltmUserId", label: "用户 ID", kind: "number", path: "userId", min: 1, required: true },
            { key: "ltmAgentId", label: "Agent ID", kind: "number", path: "agentId", min: 1 },
            { key: "ltmConversationId", label: "会话 ID", kind: "number", path: "conversationId", min: 1 },
            { key: "ltmMemoryKey", label: "记忆键", kind: "text", path: "memoryKey" },
            { key: "ltmContent", label: "内容", kind: "variableRefPicker", path: "content", rows: 3 },
            { key: "ltmSource", label: "来源", kind: "text", path: "source" },
            { key: "ltmLimit", label: "读取条数", kind: "number", path: "limit", min: 1, max: 100 },
            { key: "ltmMemoryId", label: "记忆 ID", kind: "number", path: "memoryId", min: 1 }
          ]
        }
      ];
    case "DatabaseQuery":
      return [
        {
          key: "basic",
          title: "数据库查询",
          fields: [
            { key: "dbQueryDatabaseInfoId", label: "数据源 ID", kind: "number", path: "databaseInfoId", required: true, min: 1 },
            { key: "dbQueryFields", label: "查询字段", kind: "tagInput", path: "queryFields" },
            { key: "dbQueryClauses", label: "过滤条件", kind: "arrayEditor", path: "clauseGroup", itemFields: [{ key: "field", label: "字段" }, { key: "op", label: "操作符" }, { key: "value", label: "值" }, { key: "logic", label: "逻辑(AND/OR)" }] },
            { key: "dbQueryLimit", label: "返回条数", kind: "number", path: "limit", min: 1, max: 1000 },
            { key: "dbQueryOutputKey", label: "输出变量", kind: "text", path: "outputKey", placeholder: "rows" }
          ]
        }
      ];
    case "DatabaseInsert":
      return [
        {
          key: "basic",
          title: "数据库新增",
          fields: [
            { key: "dbInsertDatabaseInfoId", label: "数据源 ID", kind: "number", path: "databaseInfoId", required: true, min: 1 },
            { key: "dbInsertRows", label: "插入行(JSON)", kind: "json", path: "rows", rows: 8 }
          ]
        }
      ];
    case "DatabaseUpdate":
      return [
        {
          key: "basic",
          title: "数据库更新",
          fields: [
            { key: "dbUpdateDatabaseInfoId", label: "数据源 ID", kind: "number", path: "databaseInfoId", required: true, min: 1 },
            { key: "dbUpdateFields", label: "更新字段", kind: "objectEditor", path: "updateFields" },
            { key: "dbUpdateClauses", label: "过滤条件", kind: "arrayEditor", path: "clauseGroup", itemFields: [{ key: "field", label: "字段" }, { key: "op", label: "操作符" }, { key: "value", label: "值" }, { key: "logic", label: "逻辑(AND/OR)" }] }
          ]
        }
      ];
    case "DatabaseDelete":
      return [
        {
          key: "basic",
          title: "数据库删除",
          fields: [
            { key: "dbDeleteDatabaseInfoId", label: "数据源 ID", kind: "number", path: "databaseInfoId", required: true, min: 1 },
            { key: "dbDeleteClauses", label: "过滤条件", kind: "arrayEditor", path: "clauseGroup", itemFields: [{ key: "field", label: "字段" }, { key: "op", label: "操作符" }, { key: "value", label: "值" }, { key: "logic", label: "逻辑(AND/OR)" }] }
          ]
        }
      ];
    case "DatabaseCustomSql":
      return [
        {
          key: "basic",
          title: "自定义 SQL",
          fields: [
            { key: "dbCustomSqlDatabaseInfoId", label: "数据源 ID", kind: "number", path: "databaseInfoId", required: true, min: 1 },
            { key: "dbCustomSqlTemplate", label: "SQL 模板", kind: "codeEditor", path: "sqlTemplate", editorLanguage: "sql", rows: 10, required: true }
          ]
        }
      ];
    case "CreateConversation":
      return [
        {
          key: "basic",
          title: "创建会话",
          fields: [
            { key: "createConversationTitle", label: "标题", kind: "text", path: "title" },
            { key: "createConversationUserId", label: "用户 ID", kind: "number", path: "userId", min: 1, required: true },
            { key: "createConversationAgentId", label: "Agent ID", kind: "number", path: "agentId", min: 1, required: true }
          ]
        }
      ];
    case "ConversationList":
      return [
        {
          key: "basic",
          title: "会话列表",
          fields: [
            { key: "conversationListUserId", label: "用户 ID", kind: "number", path: "userId", min: 1, required: true },
            { key: "conversationListAgentId", label: "Agent ID", kind: "number", path: "agentId", min: 1, required: true },
            { key: "conversationListPageIndex", label: "页码", kind: "number", path: "pageIndex", min: 1 },
            { key: "conversationListPageSize", label: "每页数量", kind: "number", path: "pageSize", min: 1, max: 100 }
          ]
        }
      ];
    case "ConversationUpdate":
      return [
        {
          key: "basic",
          title: "更新会话",
          fields: [
            { key: "conversationUpdateUserId", label: "用户 ID", kind: "number", path: "userId", min: 1, required: true },
            { key: "conversationUpdateConversationId", label: "会话 ID", kind: "number", path: "conversationId", min: 1, required: true },
            { key: "conversationUpdateTitle", label: "标题", kind: "text", path: "title", required: true }
          ]
        }
      ];
    case "ConversationDelete":
      return [
        {
          key: "basic",
          title: "删除会话",
          fields: [
            { key: "conversationDeleteUserId", label: "用户 ID", kind: "number", path: "userId", min: 1, required: true },
            { key: "conversationDeleteConversationId", label: "会话 ID", kind: "number", path: "conversationId", min: 1, required: true }
          ]
        }
      ];
    case "ConversationHistory":
      return [
        {
          key: "basic",
          title: "会话历史",
          fields: [
            { key: "conversationHistoryUserId", label: "用户 ID", kind: "number", path: "userId", min: 1, required: true },
            { key: "conversationHistoryConversationId", label: "会话 ID", kind: "number", path: "conversationId", min: 1, required: true },
            { key: "conversationHistoryLimit", label: "返回条数", kind: "number", path: "limit", min: 1, max: 200 },
            { key: "conversationHistoryIncludeContextMarkers", label: "包含上下文标记", kind: "switch", path: "includeContextMarkers" }
          ]
        }
      ];
    case "ClearConversationHistory":
      return [
        {
          key: "basic",
          title: "清空会话历史",
          fields: [
            { key: "clearConversationHistoryUserId", label: "用户 ID", kind: "number", path: "userId", min: 1, required: true },
            { key: "clearConversationHistoryConversationId", label: "会话 ID", kind: "number", path: "conversationId", min: 1, required: true }
          ]
        }
      ];
    case "MessageList":
      return [
        {
          key: "basic",
          title: "消息列表",
          fields: [
            { key: "messageListUserId", label: "用户 ID", kind: "number", path: "userId", min: 1, required: true },
            { key: "messageListConversationId", label: "会话 ID", kind: "number", path: "conversationId", min: 1, required: true },
            { key: "messageListPageIndex", label: "页码", kind: "number", path: "pageIndex", min: 1 },
            { key: "messageListPageSize", label: "每页数量", kind: "number", path: "pageSize", min: 1, max: 100 }
          ]
        }
      ];
    case "CreateMessage":
      return [
        {
          key: "basic",
          title: "创建消息",
          fields: [
            { key: "createMessageConversationId", label: "会话 ID", kind: "number", path: "conversationId", min: 1, required: true },
            { key: "createMessageRole", label: "角色", kind: "text", path: "role", required: true, placeholder: "user" },
            { key: "createMessageContent", label: "内容", kind: "variableRefPicker", path: "content", rows: 4, required: true },
            { key: "createMessageMetadata", label: "元数据", kind: "json", path: "metadata", rows: 4 }
          ]
        }
      ];
    case "EditMessage":
      return [
        {
          key: "basic",
          title: "编辑消息",
          fields: [
            { key: "editMessageConversationId", label: "会话 ID", kind: "number", path: "conversationId", min: 1, required: true },
            { key: "editMessageMessageId", label: "消息 ID", kind: "number", path: "messageId", min: 1, required: true },
            { key: "editMessageContent", label: "内容", kind: "variableRefPicker", path: "content", rows: 4, required: true },
            { key: "editMessageMetadata", label: "元数据", kind: "json", path: "metadata", rows: 4 }
          ]
        }
      ];
    case "DeleteMessage":
      return [
        {
          key: "basic",
          title: "删除消息",
          fields: [
            { key: "deleteMessageUserId", label: "用户 ID", kind: "number", path: "userId", min: 1, required: true },
            { key: "deleteMessageConversationId", label: "会话 ID", kind: "number", path: "conversationId", min: 1, required: true },
            { key: "deleteMessageMessageId", label: "消息 ID", kind: "number", path: "messageId", min: 1, required: true }
          ]
        }
      ];
    case "Comment":
      return [
        {
          key: "basic",
          title: "注释配置",
          fields: [{ key: "commentContent", label: "内容", kind: "textarea", path: "content", rows: 5, required: true }]
        }
      ];
    default:
      return [
        {
          key: "fallback",
          title: "基础配置",
          fields: [FALLBACK_TEXT_FIELD]
        }
      ];
  }
}

function createFallbackDefaults(type: WorkflowNodeTypeKey): Record<string, unknown> {
  if (FLOW_CONTROL_TYPES.includes(type)) {
    return { enabled: true };
  }
  if (AI_TYPES.includes(type)) {
    return { enabled: true, timeoutMs: 120000 };
  }
  if (DATA_TYPES.includes(type)) {
    return { enabled: true };
  }
  if (EXTERNAL_TYPES.includes(type)) {
    return { enabled: true, timeoutMs: 30000, retryTimes: 1 };
  }
  if (KNOWLEDGE_TYPES.includes(type)) {
    return { enabled: true, topK: 5 };
  }
  if (DATABASE_TYPES.includes(type)) {
    return { enabled: true, limit: 100 };
  }
  return { enabled: true };
}

function nonEmpty(value: unknown): boolean {
  return typeof value === "string" && value.trim().length > 0;
}

function toNumber(value: unknown): number | null {
  if (typeof value === "number" && !Number.isNaN(value)) {
    return value;
  }
  if (typeof value === "string" && value.trim().length > 0) {
    const parsed = Number(value);
    return Number.isNaN(parsed) ? null : parsed;
  }
  return null;
}

function positive(value: unknown): boolean {
  const num = toNumber(value);
  return num !== null && num > 0;
}

function hasArray(value: unknown): boolean {
  return Array.isArray(value) && value.length > 0;
}

function createValidator(type: WorkflowNodeTypeKey) {
  return (config: Record<string, unknown>): string[] => {
    const errors: string[] = [];
    switch (type) {
      case "Entry":
        if (!nonEmpty(config.entryVariable)) {
          errors.push("开始节点: 会话变量不能为空");
        }
        break;
      case "Exit":
        if (!nonEmpty(config.exitTerminateMode)) {
          errors.push("结束节点: 终止策略不能为空");
        }
        break;
      case "Selector":
        if (!nonEmpty(config.condition) && !hasArray(config.conditions)) {
          errors.push("条件节点: 需配置表达式或结构化条件");
        }
        break;
      case "Loop":
        if (!nonEmpty(config.mode)) {
          errors.push("循环节点: mode 必填");
        }
        if (!positive(config.maxIterations)) {
          errors.push("循环节点: maxIterations 必须大于 0");
        }
        break;
      case "Batch":
        if (!positive(config.concurrentSize)) {
          errors.push("批处理节点: concurrentSize 必须大于 0");
        }
        if (!positive(config.batchSize)) {
          errors.push("批处理节点: batchSize 必须大于 0");
        }
        if (!nonEmpty(config.inputArrayPath)) {
          errors.push("批处理节点: inputArrayPath 必填");
        }
        break;
      case "Break":
        if (!nonEmpty(config.reason)) {
          errors.push("中断节点: reason 必填");
        }
        break;
      case "Llm":
        if (!nonEmpty(config.model)) {
          errors.push("LLM 节点: model 必填");
        }
        if (!nonEmpty(config.prompt)) {
          errors.push("LLM 节点: prompt 必填");
        }
        break;
      case "Agent":
        if (!nonEmpty(config.agentId) || !nonEmpty(config.message)) {
          errors.push("Agent 节点: agentId/message 必填");
        }
        break;
      case "IntentDetector":
        if (!nonEmpty(config.input) || !nonEmpty(config.model)) {
          errors.push("意图识别节点: input/model 必填");
        }
        break;
      case "QuestionAnswer":
        if (!nonEmpty(config.question) || !nonEmpty(config.answerPath)) {
          errors.push("问答节点: question/answerPath 必填");
        }
        break;
      case "CodeRunner":
        if (!nonEmpty(config.code)) {
          errors.push("代码节点: code 必填");
        }
        break;
      case "TextProcessor":
        if (!nonEmpty(config.template) || !nonEmpty(config.outputKey)) {
          errors.push("文本节点: template/outputKey 必填");
        }
        break;
      case "JsonSerialization":
      case "VariableAggregator":
        if (!hasArray(config.variableKeys) || !nonEmpty(config.outputKey)) {
          errors.push("变量节点: variableKeys/outputKey 必填");
        }
        break;
      case "JsonDeserialization":
        if (!nonEmpty(config.inputVariable)) {
          errors.push("JSON 节点: inputVariable 必填");
        }
        break;
      case "AssignVariable":
      case "VariableAssignerWithinLoop":
        if (!nonEmpty(config.assignments)) {
          errors.push("赋值节点: assignments 必填");
        }
        break;
      case "Plugin":
        if (!nonEmpty(config.pluginId) || !nonEmpty(config.apiId)) {
          errors.push("插件节点: pluginId/apiId 必填");
        }
        break;
      case "HttpRequester":
        if (!nonEmpty(config.url) || !nonEmpty(config.method)) {
          errors.push("HTTP 节点: url/method 必填");
        }
        break;
      case "SubWorkflow":
        if (!nonEmpty(config.workflowId)) {
          errors.push("子工作流节点: workflowId 必填");
        }
        break;
      case "InputReceiver":
        if (!nonEmpty(config.inputPath)) {
          errors.push("输入节点: inputPath 必填");
        }
        break;
      case "OutputEmitter":
        if (!nonEmpty(config.outputKey)) {
          errors.push("输出节点: outputKey 必填");
        }
        break;
      case "KnowledgeRetriever":
        if (!hasArray(config.knowledgeIds) || !nonEmpty(config.query)) {
          errors.push("知识检索节点: knowledgeIds/query 必填");
        }
        break;
      case "KnowledgeIndexer":
        if (!positive(config.knowledgeId) || !positive(config.fileId)) {
          errors.push("知识写入节点: knowledgeId/fileId 必填");
        }
        break;
      case "KnowledgeDeleter":
        if (!positive(config.knowledgeId)) {
          errors.push("知识删除节点: knowledgeId 必填");
        }
        break;
      case "Ltm":
        if (!nonEmpty(config.action) || !positive(config.userId)) {
          errors.push("LTM 节点: action/userId 必填");
        }
        break;
      case "DatabaseQuery":
      case "DatabaseInsert":
      case "DatabaseUpdate":
      case "DatabaseDelete":
      case "DatabaseCustomSql":
        if (!positive(config.databaseInfoId) && !positive(config.databaseId)) {
          errors.push("数据库节点: databaseInfoId/databaseId 必填");
        }
        if (type === "DatabaseCustomSql" && !nonEmpty(config.sqlTemplate)) {
          errors.push("数据库节点: sqlTemplate 必填");
        }
        break;
      case "CreateConversation":
        if (!positive(config.userId) || !positive(config.agentId)) {
          errors.push("创建会话节点: userId/agentId 必填");
        }
        break;
      case "ConversationList":
        if (!positive(config.userId) || !positive(config.agentId)) {
          errors.push("会话列表节点: userId/agentId 必填");
        }
        break;
      case "ConversationUpdate":
        if (!positive(config.userId) || !positive(config.conversationId) || !nonEmpty(config.title)) {
          errors.push("更新会话节点: userId/conversationId/title 必填");
        }
        break;
      case "ConversationDelete":
      case "ConversationHistory":
      case "ClearConversationHistory":
      case "MessageList":
        if (!positive(config.userId) || !positive(config.conversationId)) {
          errors.push("会话节点: userId/conversationId 必填");
        }
        break;
      case "CreateMessage":
        if (!positive(config.conversationId) || !nonEmpty(config.content)) {
          errors.push("创建消息节点: conversationId/content 必填");
        }
        break;
      case "EditMessage":
        if (!positive(config.conversationId) || !positive(config.messageId) || !nonEmpty(config.content)) {
          errors.push("编辑消息节点: conversationId/messageId/content 必填");
        }
        break;
      case "DeleteMessage":
        if (!positive(config.userId) || !positive(config.conversationId) || !positive(config.messageId)) {
          errors.push("删除消息节点: userId/conversationId/messageId 必填");
        }
        break;
      case "Comment":
        if (!nonEmpty(config.content)) {
          errors.push("注释节点: content 必填");
        }
        break;
      default:
        break;
    }
    return errors;
  };
}

export function createNodeDefinition(type: WorkflowNodeTypeKey): NodeDefinition {
  return {
    type,
    sections: createStandardSections(type),
    getFallbackDefaults: () => createFallbackDefaults(type),
    validate: (ctx) => createValidator(type)(ctx.config)
  };
}

