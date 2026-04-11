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
const CONVERSATION_TYPES: WorkflowNodeTypeKey[] = [
  "CreateConversation",
  "ConversationList",
  "ConversationUpdate",
  "ConversationDelete",
  "ConversationHistory",
  "ClearConversationHistory",
  "MessageList",
  "CreateMessage",
  "EditMessage",
  "DeleteMessage",
  "Comment"
];

function createStandardSections(type: WorkflowNodeTypeKey): FormSectionSchema[] {
  if (type === "Entry") {
    return [
      {
        key: "basic",
        title: "基础配置",
        fields: [
          { key: "entryVariable", label: "会话变量", kind: "text", path: "entry.variable", required: true, placeholder: "USER_INPUT" },
          { key: "entryDescription", label: "描述", kind: "textarea", path: "entry.description", rows: 3 },
          { key: "entryAutoSave", label: "自动保存历史", kind: "switch", path: "entry.autoSaveHistory" }
        ]
      },
      {
        key: "advanced",
        title: "高级",
        advanced: true,
        fields: [{ key: "entryInputMappings", label: "输入映射", kind: "keyValue", path: "inputMappings" }]
      }
    ];
  }

  if (type === "Exit") {
    return [
      {
        key: "basic",
        title: "基础配置",
        fields: [
          {
            key: "exitTerminateMode",
            label: "终止策略",
            kind: "select",
            path: "exit.terminateMode",
            required: true,
            options: [
              { value: "return", label: "返回输出" },
              { value: "interrupt", label: "中断执行" }
            ]
          },
          { key: "exitTemplate", label: "输出模板", kind: "textarea", path: "exit.template", rows: 4 },
          { key: "exitStreaming", label: "流式输出", kind: "switch", path: "exit.streaming" }
        ]
      }
    ];
  }

  if (type === "Selector") {
    return [
      {
        key: "basic",
        title: "条件判断",
        fields: [
          { key: "selectorExpression", label: "条件表达式", kind: "textarea", path: "selector.expression", required: true, rows: 4 },
          {
            key: "selectorDefaultBranch",
            label: "默认分支",
            kind: "select",
            path: "selector.defaultBranch",
            options: [
              { value: "true", label: "true" },
              { value: "false", label: "false" }
            ]
          }
        ]
      }
    ];
  }

  if (type === "Loop") {
    return [
      {
        key: "basic",
        title: "循环配置",
        fields: [
          {
            key: "loopType",
            label: "循环类型",
            kind: "select",
            path: "loop.type",
            options: [
              { value: "array", label: "数组循环" },
              { value: "count", label: "计数循环" },
              { value: "infinite", label: "无限循环" }
            ]
          },
          { key: "loopCount", label: "最大次数", kind: "number", path: "loop.maxIterations", min: 1, max: 10000 },
          { key: "loopInput", label: "输入列表表达式", kind: "text", path: "loop.inputExpression", required: true }
        ]
      }
    ];
  }

  if (type === "Batch") {
    return [
      {
        key: "basic",
        title: "批处理配置",
        fields: [
          { key: "batchConcurrent", label: "并发数", kind: "number", path: "batch.concurrentSize", min: 1, max: 128 },
          { key: "batchSize", label: "分批大小", kind: "number", path: "batch.batchSize", min: 1, max: 1000 },
          { key: "batchInput", label: "输入列表表达式", kind: "text", path: "batch.inputExpression", required: true }
        ]
      }
    ];
  }

  if (type === "Break" || type === "Continue") {
    return [
      {
        key: "basic",
        title: "控制配置",
        fields: [{ key: "controlCondition", label: "触发条件", kind: "text", path: "control.condition", required: true }]
      }
    ];
  }

  if (type === "Llm") {
    return [
      {
        key: "basic",
        title: "模型配置",
        fields: [
          { key: "llmProvider", label: "模型提供商", kind: "text", path: "llm.provider", required: true, placeholder: "qwen" },
          { key: "llmModelId", label: "模型", kind: "text", path: "llm.model", required: true, placeholder: "qwen-max" },
          { key: "llmSystemPrompt", label: "系统提示词", kind: "textarea", path: "llm.systemPrompt", rows: 5 },
          { key: "llmUserPrompt", label: "用户提示词", kind: "textarea", path: "llm.userPrompt", rows: 5, required: true }
        ]
      },
      {
        key: "params",
        title: "推理参数",
        fields: [
          { key: "llmTemperature", label: "Temperature", kind: "number", path: "llm.temperature", min: 0, max: 2 },
          { key: "llmTopP", label: "TopP", kind: "number", path: "llm.topP", min: 0, max: 1 },
          { key: "llmMaxTokens", label: "最大Token", kind: "number", path: "llm.maxTokens", min: 1, max: 32000 },
          { key: "llmStream", label: "流式输出", kind: "switch", path: "llm.stream" }
        ]
      },
      {
        key: "advanced",
        title: "高级",
        advanced: true,
        fields: [{ key: "llmInputMappings", label: "输入映射", kind: "keyValue", path: "inputMappings" }]
      }
    ];
  }

  if (type === "IntentDetector" || type === "QuestionAnswer") {
    return [
      {
        key: "basic",
        title: "AI 配置",
        fields: [
          { key: "aiPrompt", label: "提示词", kind: "textarea", path: "ai.prompt", rows: 4, required: true },
          { key: "aiOutputKey", label: "输出字段", kind: "text", path: "ai.outputKey", required: true }
        ]
      }
    ];
  }

  if (type === "CodeRunner") {
    return [
      {
        key: "basic",
        title: "代码执行",
        fields: [
          {
            key: "codeLanguage",
            label: "运行时",
            kind: "select",
            path: "code.language",
            required: true,
            options: [
              { value: "javascript", label: "JavaScript" },
              { value: "python", label: "Python" }
            ]
          },
          { key: "codeBody", label: "代码", kind: "json", path: "code.source", rows: 12, required: true }
        ]
      }
    ];
  }

  if (type === "TextProcessor" || type === "JsonSerialization" || type === "JsonDeserialization") {
    return [
      {
        key: "basic",
        title: "处理配置",
        fields: [
          { key: "processorInput", label: "输入表达式", kind: "text", path: "processor.input", required: true },
          { key: "processorOutput", label: "输出字段", kind: "text", path: "processor.outputKey", required: true }
        ]
      }
    ];
  }

  if (type === "VariableAggregator" || type === "AssignVariable" || type === "VariableAssignerWithinLoop") {
    return [
      {
        key: "basic",
        title: "变量配置",
        fields: [
          { key: "varTarget", label: "目标变量", kind: "text", path: "variables.target", required: true },
          { key: "varExpression", label: "变量表达式", kind: "textarea", path: "variables.expression", rows: 4, required: true }
        ]
      }
    ];
  }

  if (type === "Plugin" || type === "SubWorkflow") {
    return [
      {
        key: "basic",
        title: "资源配置",
        fields: [
          { key: "resourceId", label: type === "Plugin" ? "插件 ID" : "子工作流 ID", kind: "text", path: "resource.id", required: true },
          { key: "resourceVersion", label: "版本", kind: "text", path: "resource.version" },
          { key: "resourceBatch", label: "批处理模式", kind: "switch", path: "resource.batchMode" }
        ]
      },
      {
        key: "advanced",
        title: "高级",
        advanced: true,
        fields: [{ key: "resourceInputMappings", label: "输入映射", kind: "keyValue", path: "inputMappings" }]
      }
    ];
  }

  if (type === "HttpRequester") {
    return [
      {
        key: "basic",
        title: "HTTP 配置",
        fields: [
          {
            key: "httpMethod",
            label: "Method",
            kind: "select",
            path: "http.method",
            required: true,
            options: [
              { value: "GET", label: "GET" },
              { value: "POST", label: "POST" },
              { value: "PUT", label: "PUT" },
              { value: "PATCH", label: "PATCH" },
              { value: "DELETE", label: "DELETE" }
            ]
          },
          { key: "httpUrl", label: "URL", kind: "text", path: "http.url", required: true, placeholder: "https://api.example.com" },
          { key: "httpHeaders", label: "Headers", kind: "keyValue", path: "http.headers" },
          { key: "httpBody", label: "Body JSON", kind: "json", path: "http.body", rows: 8 }
        ]
      }
    ];
  }

  if (type === "InputReceiver" || type === "OutputEmitter") {
    return [
      {
        key: "basic",
        title: "输入输出配置",
        fields: [
          { key: "ioKey", label: "变量名", kind: "text", path: "io.key", required: true },
          { key: "ioDescription", label: "说明", kind: "textarea", path: "io.description", rows: 3 }
        ]
      }
    ];
  }

  if (type === "KnowledgeRetriever" || type === "KnowledgeIndexer" || type === "KnowledgeDeleter" || type === "Ltm") {
    return [
      {
        key: "basic",
        title: "知识库配置",
        fields: [
          { key: "knowledgeDatasetId", label: "知识库 ID", kind: "text", path: "knowledge.datasetId", required: true },
          { key: "knowledgeQuery", label: "查询表达式", kind: "text", path: "knowledge.query", required: type !== "KnowledgeIndexer" },
          { key: "knowledgeTopK", label: "TopK", kind: "number", path: "knowledge.topK", min: 1, max: 50 }
        ]
      }
    ];
  }

  if (type === "DatabaseQuery" || type === "DatabaseInsert" || type === "DatabaseUpdate" || type === "DatabaseDelete" || type === "DatabaseCustomSql") {
    return [
      {
        key: "basic",
        title: "数据库配置",
        fields: [
          { key: "dbId", label: "数据源 ID", kind: "text", path: "database.id", required: true },
          { key: "dbTable", label: "表名", kind: "text", path: "database.table", required: type !== "DatabaseCustomSql" },
          { key: "dbWhere", label: "过滤条件(JSON)", kind: "json", path: "database.where", rows: 6 },
          { key: "dbSql", label: "SQL", kind: "json", path: "database.sql", rows: 8, required: type === "DatabaseCustomSql" }
        ]
      }
    ];
  }

  if (type === "Comment") {
    return [
      {
        key: "basic",
        title: "注释配置",
        fields: [{ key: "commentContent", label: "内容", kind: "textarea", path: "comment.content", rows: 5, required: true }]
      }
    ];
  }

  if (CONVERSATION_TYPES.includes(type)) {
    return [
      {
        key: "basic",
        title: "会话配置",
        fields: [
          { key: "conversationId", label: "会话ID表达式", kind: "text", path: "conversation.id", required: type !== "CreateConversation" },
          { key: "conversationUser", label: "用户ID表达式", kind: "text", path: "conversation.userId" },
          { key: "conversationPayload", label: "参数(JSON)", kind: "json", path: "conversation.payload", rows: 6 }
        ]
      }
    ];
  }

  return [
    {
      key: "fallback",
      title: "基础配置",
      fields: [FALLBACK_TEXT_FIELD]
    }
  ];
}

function createFallbackDefaults(type: WorkflowNodeTypeKey): Record<string, unknown> {
  if (FLOW_CONTROL_TYPES.includes(type)) {
    return { enabled: true, retry: 0 };
  }
  if (AI_TYPES.includes(type)) {
    return { enabled: true, timeoutMs: 120000, inputMappings: {} };
  }
  if (DATA_TYPES.includes(type)) {
    return { enabled: true, strict: false };
  }
  if (EXTERNAL_TYPES.includes(type)) {
    return { enabled: true, timeoutMs: 10000, retries: 1 };
  }
  if (KNOWLEDGE_TYPES.includes(type)) {
    return { enabled: true, knowledge: { topK: 5 } };
  }
  if (DATABASE_TYPES.includes(type)) {
    return { enabled: true, database: { limit: 100 } };
  }
  return { enabled: true };
}

function createP0Validator(type: WorkflowNodeTypeKey) {
  if (type === "Entry") {
    return (config: Record<string, unknown>) => {
      const entry = (config.entry as Record<string, unknown> | undefined) ?? {};
      return typeof entry.variable === "string" && entry.variable.trim().length > 0 ? [] : ["开始节点: 会话变量不能为空"];
    };
  }
  if (type === "Exit") {
    return (config: Record<string, unknown>) => {
      const exit = (config.exit as Record<string, unknown> | undefined) ?? {};
      return typeof exit.terminateMode === "string" && exit.terminateMode.trim().length > 0 ? [] : ["结束节点: 终止策略不能为空"];
    };
  }
  if (type === "Selector") {
    return (config: Record<string, unknown>) => {
      const selector = (config.selector as Record<string, unknown> | undefined) ?? {};
      return typeof selector.expression === "string" && selector.expression.trim().length > 0 ? [] : ["条件节点: 条件表达式不能为空"];
    };
  }
  if (type === "Loop") {
    return (config: Record<string, unknown>) => {
      const loop = (config.loop as Record<string, unknown> | undefined) ?? {};
      const expressionValid = typeof loop.inputExpression === "string" && loop.inputExpression.trim().length > 0;
      const count = loop.maxIterations;
      const countValid = typeof count === "number" && count >= 1 && count <= 10000;
      const errors: string[] = [];
      if (!expressionValid) {
        errors.push("循环节点: 输入列表表达式不能为空");
      }
      if (!countValid) {
        errors.push("循环节点: 最大次数需在 1~10000");
      }
      return errors;
    };
  }
  if (type === "Batch") {
    return (config: Record<string, unknown>) => {
      const batch = (config.batch as Record<string, unknown> | undefined) ?? {};
      const concurrent = batch.concurrentSize;
      const size = batch.batchSize;
      const expression = batch.inputExpression;
      const errors: string[] = [];
      if (typeof concurrent !== "number" || concurrent < 1 || concurrent > 128) {
        errors.push("批处理节点: 并发数需在 1~128");
      }
      if (typeof size !== "number" || size < 1 || size > 1000) {
        errors.push("批处理节点: 分批大小需在 1~1000");
      }
      if (typeof expression !== "string" || expression.trim().length === 0) {
        errors.push("批处理节点: 输入列表表达式不能为空");
      }
      return errors;
    };
  }
  if (type === "Break" || type === "Continue") {
    return (config: Record<string, unknown>) => {
      const control = (config.control as Record<string, unknown> | undefined) ?? {};
      return typeof control.condition === "string" && control.condition.trim().length > 0
        ? []
        : [`${type === "Break" ? "中断循环" : "继续循环"}节点: 触发条件不能为空`];
    };
  }
  if (type === "Llm") {
    return (config: Record<string, unknown>) => {
      const llm = (config.llm as Record<string, unknown> | undefined) ?? {};
      const errors: string[] = [];
      if (typeof llm.model !== "string" || llm.model.trim().length === 0) {
        errors.push("LLM 节点: 模型不能为空");
      }
      if (typeof llm.userPrompt !== "string" || llm.userPrompt.trim().length === 0) {
        errors.push("LLM 节点: 用户提示词不能为空");
      }
      return errors;
    };
  }
  if (type === "IntentDetector" || type === "QuestionAnswer") {
    return (config: Record<string, unknown>) => {
      const ai = (config.ai as Record<string, unknown> | undefined) ?? {};
      const errors: string[] = [];
      if (typeof ai.prompt !== "string" || ai.prompt.trim().length === 0) {
        errors.push("AI 节点: 提示词不能为空");
      }
      if (typeof ai.outputKey !== "string" || ai.outputKey.trim().length === 0) {
        errors.push("AI 节点: 输出字段不能为空");
      }
      return errors;
    };
  }
  if (type === "CodeRunner") {
    return (config: Record<string, unknown>) => {
      const code = (config.code as Record<string, unknown> | undefined) ?? {};
      const errors: string[] = [];
      if (typeof code.language !== "string" || code.language.trim().length === 0) {
        errors.push("代码节点: 运行时不能为空");
      }
      if (typeof code.source !== "string" || code.source.trim().length === 0) {
        errors.push("代码节点: 代码不能为空");
      }
      return errors;
    };
  }
  return undefined;
}

export function createNodeDefinition(type: WorkflowNodeTypeKey): NodeDefinition {
  return {
    type,
    sections: createStandardSections(type),
    getFallbackDefaults: () => createFallbackDefaults(type),
    validate: createP0Validator(type)
      ? (ctx) => {
          const validator = createP0Validator(type);
          return validator ? validator(ctx.config) : [];
        }
      : undefined
  };
}

