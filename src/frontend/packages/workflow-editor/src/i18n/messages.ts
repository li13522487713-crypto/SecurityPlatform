import type { Composer } from "vue-i18n";

const workflowZhCN = {
  workflowUi: {
    handleTrue: "真",
    handleFalse: "假",
    handleBody: "循环体",
    handleDone: "完成"
  },
  workflow: {
    editorUnsaved: "未保存",
    saveDraft: "保存草稿",
    publish: "发布",
    colLatestVersion: "最新版本",
    testRunToolbar: "测试运行",
    moreActions: "更多操作",
    exportCanvasJson: "导出画布 JSON",
    importCanvasJson: "导入画布 JSON",
    resetCanvas: "重置画布",
    autosaveNotStarted: "尚未保存",
    autosaveAt: "已保存 {time}",
    debugCardTitle: "执行恢复与调试视图",
    publishModalTitle: "发布工作流",
    publishOk: "确认发布",
    labelChangelog: "变更说明",
    phChangelog: "填写本次发布说明",
    defaultName: "未命名工作流",
    draftSaved: "草稿保存成功",
    publishSuccess: "发布成功",
    importJsonSuccess: "导入画布成功",
    importJsonFailed: "导入画布失败，请检查 JSON 格式",
    resetCanvasSuccess: "画布已重置",
    nodeStart: "开始",
    nodeEnd: "结束"
  },
  wfUi: {
    nodePanel: {
      phSearch: "搜索节点",
      catFlowControl: "流程控制",
      catAi: "AI能力",
      catDataProcess: "数据处理",
      catExternal: "外部交互",
      catKnowledge: "知识库",
      catDatabase: "数据库",
      catConversation: "对话管理"
    },
    nodeTypes: {
      Entry: "开始",
      Exit: "结束",
      Selector: "条件判断",
      Loop: "循环",
      Batch: "批处理",
      Break: "中断循环",
      Continue: "继续循环",
      Llm: "大模型",
      IntentDetector: "意图识别",
      QuestionAnswer: "问答",
      CodeRunner: "代码执行",
      TextProcessor: "文本处理",
      JsonSerialization: "JSON序列化",
      JsonDeserialization: "JSON反序列化",
      VariableAggregator: "变量聚合",
      AssignVariable: "变量赋值",
      Plugin: "插件",
      HttpRequester: "HTTP请求",
      SubWorkflow: "子工作流",
      KnowledgeRetriever: "知识检索",
      KnowledgeIndexer: "知识写入",
      Ltm: "长期记忆",
      DatabaseQuery: "数据库查询",
      DatabaseInsert: "数据库新增",
      DatabaseUpdate: "数据库更新",
      DatabaseDelete: "数据库删除",
      DatabaseCustomSql: "自定义SQL",
      CreateConversation: "创建会话",
      ConversationList: "会话列表",
      ConversationUpdate: "更新会话",
      ConversationDelete: "删除会话",
      ConversationHistory: "会话历史",
      ClearConversationHistory: "清空会话历史",
      MessageList: "消息列表",
      CreateMessage: "创建消息",
      EditMessage: "编辑消息",
      DeleteMessage: "删除消息",
      InputReceiver: "等待输入",
      OutputEmitter: "中间输出"
    },
    properties: {
      title: "节点属性",
      basic: "通用设置",
      labelTitle: "标题",
      labelKey: "节点Key",
      labelType: "节点类型",
      nodeConfig: "节点配置",
      configJson: "配置JSON",
      inputMap: "输入映射",
      mapHint: "将节点字段映射到上游变量引用",
      phField: "字段名",
      phRef: "变量引用"
    },
    testRun: {
      tabRun: "运行",
      tabTrace: "追踪",
      tabVersions: "版本",
      inputParams: "输入参数",
      syncRun: "同步运行",
      streamRun: "流式运行",
      cancel: "取消",
      execLog: "执行日志",
      clearLog: "清空",
      logEmpty: "暂无日志",
      finalOutput: "最终输出",
      watchVariables: "变量监视",
      noVariableSnapshot: "暂无变量快照",
      waitUser: "等待用户输入",
      phAnswer: "请输入回答",
      submitAnswer: "提交回答",
      traceHint: "运行后查看节点耗时与状态",
      totalTime: "总耗时",
      status: "状态",
      nodeDetails: "节点详情",
      publishNew: "发布新版本",
      noVersions: "暂无版本",
      noDesc: "无说明",
      evtExecutionStart: "执行开始",
      evtNodeStart: "节点开始",
      evtNodeOutput: "节点输出",
      evtNodeComplete: "节点完成",
      evtNodeFailed: "节点失败",
      evtLlmOutput: "模型输出",
      evtExecutionComplete: "执行完成",
      evtExecutionFailed: "执行失败",
      evtExecutionCancelled: "执行取消",
      evtExecutionInterrupted: "执行中断",
      evtWorkflowError: "流程错误",
      jsonInvalid: "输入JSON格式错误",
      cancelled: "已取消",
      resumed: "已恢复",
      syncStart: "开始同步执行",
      runFailed: "执行失败",
      syncComplete: "同步执行完成，耗时 {ms}ms",
      streamStart: "开始流式执行",
      execCreated: "执行实例已创建：{id}",
      nodeStart: "节点开始：{key}",
      nodeOutput: "节点输出：{key} => {out}",
      nodeComplete: "节点完成：{key}（{ms}ms）",
      nodeFailed: "节点失败：{key}，{msg}",
      streamComplete: "流式执行完成：{id}",
      execFailed: "执行失败",
      execCancelled: "执行已取消：{id}",
      execInterrupted: "执行中断：{type}",
      interruptQuestion: "节点 {node} 中断，类型 {type}",
      traceLoadFailed: "加载执行追踪失败"
    },
    forms: {
      start: {
        defaultVariables: "默认变量",
        variableName: "变量名",
        defaultValue: "默认值",
        addVariable: "+ 添加变量",
        autoSaveHistory: "自动保存历史"
      },
      end: {
        terminationMode: "终止计划",
        returnVariables: "返回变量",
        answerText: "回答内容",
        outputMappings: "输出变量映射",
        sourceVariable: "源变量",
        targetField: "输出字段",
        addMapping: "+ 添加映射",
        templateText: "模板文本",
        streamOutput: "流式输出"
      },
      selector: {
        matchMode: "分支命中策略",
        matchAll: "全部满足 (AND)",
        matchAny: "任一满足 (OR)",
        conditions: "条件构建器",
        fieldPath: "变量路径，例如 input.score",
        compareValue: "比较值",
        addCondition: "+ 添加条件",
        fallbackExpression: "兜底分支表达式（可选）"
      },
      loop: {
        mode: "循环模式",
        modeCount: "按次数",
        modeWhile: "条件循环",
        modeForEach: "遍历集合",
        maxIterations: "最大迭代次数",
        indexVariable: "索引变量名",
        condition: "循环条件表达式",
        conditionPlaceholder: "例如: {{loop_index}} < 10",
        collectionPath: "集合变量路径",
        collectionPathPlaceholder: "例如: input.items",
        itemVariable: "元素变量名",
        itemIndexVariable: "元素索引变量名"
      },
      batch: {
        collectionPath: "批处理输入路径",
        collectionPathPlaceholder: "例如: input.records",
        parallelism: "并发度",
        itemTimeoutMs: "单项超时（毫秒）",
        onError: "失败策略",
        onErrorContinue: "继续处理",
        onErrorFailFast: "立即失败",
        outputKey: "结果变量名"
      },
      assignVariable: {
        variableName: "变量名",
        valueExpression: "赋值表达式",
        scope: "作用域",
        scopeWorkflow: "工作流",
        scopeLoop: "循环作用域",
        scopeSession: "会话作用域",
        overwrite: "覆盖已存在值"
      },
      llm: {
        provider: "模型提供商",
        model: "模型",
        systemPrompt: "系统提示词",
        prompt: "用户提示词模板",
        temperature: "温度",
        maxTokens: "最大输出Token",
        stream: "流式输出",
        outputKey: "输出变量名"
      },
      textProcessor: {
        mode: "处理模式",
        modeTemplate: "模板渲染",
        modeReplace: "替换",
        modeExtract: "提取",
        inputPath: "输入变量路径",
        templateText: "处理模板 / 表达式",
        outputKey: "输出变量名"
      },
      json: {
        direction: "转换方向",
        serialize: "对象 → JSON",
        deserialize: "JSON → 对象",
        inputPath: "输入变量路径",
        pretty: "格式化输出",
        outputKey: "输出变量名"
      },
      http: {
        method: "请求方法",
        url: "请求 URL",
        headersJson: "请求头 JSON",
        body: "请求体",
        timeoutMs: "超时（毫秒）",
        outputKey: "输出变量名"
      },
      questionAnswer: {
        questionTemplate: "问题模板",
        timeoutSeconds: "等待超时（秒）",
        answerKey: "答案变量名",
        allowEmpty: "允许空答案",
        defaultQuestionTemplate: "请补充必要信息。"
      },
      variableAggregator: {
        mode: "聚合模式",
        modeObject: "对象",
        modeArray: "数组",
        modeMerge: "深度合并",
        sourcePaths: "来源变量路径",
        addSourcePath: "+ 添加来源",
        outputKey: "输出变量名"
      },
      subWorkflow: {
        subWorkflowId: "子工作流 ID",
        inheritVariables: "继承父变量",
        inputsVariable: "输入变量路径",
        maxDepth: "最大嵌套深度",
        outputKey: "输出变量名"
      },
      message: {
        nodeType: "节点类型",
        conversationId: "会话 ID",
        messageId: "消息 ID",
        content: "消息内容",
        pageSize: "分页大小",
        outputKey: "输出变量名"
      },
      plugin: {
        pluginKey: "插件标识",
        method: "方法名",
        inputJson: "输入参数 JSON",
        timeoutMs: "超时（毫秒）",
        outputKey: "输出变量名"
      },
      ltm: {
        action: "操作类型",
        actionRead: "读取",
        actionWrite: "写入",
        actionDelete: "删除",
        namespace: "命名空间",
        keyName: "键",
        valuePath: "写入内容变量路径",
        outputKey: "输出变量名"
      },
      loopControl: {
        signal: "控制信号",
        reason: "说明",
        reasonPlaceholder: "可选，记录中断/继续原因",
        outputKey: "输出变量名"
      },
      knowledgeWrite: {
        datasetId: "知识库 ID",
        title: "文档标题",
        titlePlaceholder: "文档标题",
        contentPath: "写入内容变量路径",
        chunkSize: "分片大小",
        outputKey: "输出变量名"
      },
      knowledgeSearch: {
        datasetId: "知识库 ID",
        queryPath: "查询变量路径",
        minScore: "最小分数阈值",
        outputKey: "输出变量名"
      },
      io: {
        nodeType: "节点类型",
        templateText: "输出模板",
        prompt: "等待提示",
        defaultPrompt: "请继续输入。",
        timeoutSeconds: "超时（秒）",
        outputKey: "变量名"
      },
      intentDetector: {
        intents: "候选意图（每行一个）",
        inputPath: "输入变量路径",
        threshold: "低置信度阈值",
        outputKey: "输出变量名"
      },
      generic: {
        advancedConfigTip: "该节点使用高级配置（JSON）进行编辑。"
      },
      database: {
        databaseId: "数据库连接 ID",
        tableName: "表名",
        mode: "操作类型",
        whereJson: "查询条件 JSON",
        payloadJson: "插入数据 JSON",
        updateWhereJson: "更新条件 JSON",
        updatePayloadJson: "更新数据 JSON",
        deleteWhereJson: "删除条件 JSON",
        outputKey: "输出变量名"
      },
      conversation: {
        nodeType: "节点类型",
        conversationId: "会话 ID",
        userId: "用户 ID",
        title: "会话标题",
        agentId: "Agent ID（可选）",
        outputKey: "输出变量名"
      },
      conversationHistory: {
        nodeType: "节点类型",
        conversationId: "会话 ID",
        limit: "查询条数",
        outputKey: "输出变量名"
      },
      code: {
        language: "执行语言",
        source: "代码（Monaco）",
        outputKey: "输出变量名"
      }
    }
  }
};

const workflowEnUS = {
  workflowUi: {
    handleTrue: "True",
    handleFalse: "False",
    handleBody: "Body",
    handleDone: "Done"
  },
  workflow: {
    editorUnsaved: "Unsaved",
    saveDraft: "Save Draft",
    publish: "Publish",
    colLatestVersion: "Latest Version",
    testRunToolbar: "Test Run",
    moreActions: "More Actions",
    exportCanvasJson: "Export Canvas JSON",
    importCanvasJson: "Import Canvas JSON",
    resetCanvas: "Reset Canvas",
    autosaveNotStarted: "Not saved yet",
    autosaveAt: "Saved at {time}",
    debugCardTitle: "Execution Recovery & Debug",
    publishModalTitle: "Publish Workflow",
    publishOk: "Publish",
    labelChangelog: "Change Log",
    phChangelog: "Describe this release",
    defaultName: "Untitled Workflow",
    draftSaved: "Draft saved",
    publishSuccess: "Published",
    importJsonSuccess: "Canvas imported",
    importJsonFailed: "Import failed, invalid JSON",
    resetCanvasSuccess: "Canvas reset",
    nodeStart: "Start",
    nodeEnd: "End"
  },
  wfUi: {
    nodePanel: {
      phSearch: "Search nodes",
      catFlowControl: "Flow Control",
      catAi: "AI",
      catDataProcess: "Data Processing",
      catExternal: "External",
      catKnowledge: "Knowledge",
      catDatabase: "Database",
      catConversation: "Conversation"
    },
    nodeTypes: {
      Entry: "Start",
      Exit: "End",
      Selector: "If",
      Loop: "Loop",
      Batch: "Batch",
      Break: "Break",
      Continue: "Continue",
      Llm: "LLM",
      IntentDetector: "Intent Detector",
      QuestionAnswer: "Question Answer",
      CodeRunner: "Code Runner",
      TextProcessor: "Text Processor",
      JsonSerialization: "JSON Serialize",
      JsonDeserialization: "JSON Deserialize",
      VariableAggregator: "Variable Aggregate",
      AssignVariable: "Set Variable",
      Plugin: "Plugin",
      HttpRequester: "HTTP Request",
      SubWorkflow: "Sub Workflow",
      KnowledgeRetriever: "Dataset Search",
      KnowledgeIndexer: "Dataset Write",
      Ltm: "LTM",
      DatabaseQuery: "DB Query",
      DatabaseInsert: "DB Insert",
      DatabaseUpdate: "DB Update",
      DatabaseDelete: "DB Delete",
      DatabaseCustomSql: "DB Custom SQL",
      CreateConversation: "Create Conversation",
      ConversationList: "Conversation List",
      ConversationUpdate: "Update Conversation",
      ConversationDelete: "Delete Conversation",
      ConversationHistory: "Conversation History",
      ClearConversationHistory: "Clear History",
      MessageList: "Message List",
      CreateMessage: "Create Message",
      EditMessage: "Edit Message",
      DeleteMessage: "Delete Message",
      InputReceiver: "Input Receiver",
      OutputEmitter: "Output Emitter"
    },
    nodePalette: {
      descEntry: "Flow start",
      descExit: "Flow end",
      descSelector: "Branch by condition",
      descLoop: "Loop execution",
      descBatch: "Batch execution",
      descBreak: "Break loop",
      descContinue: "Continue loop",
      descLlm: "Call LLM",
      descIntentDetector: "Intent classification",
      descQuestionAnswer: "Interrupt and ask",
      descCode: "Execute script",
      descText: "Process text template",
      descSer: "Object to JSON",
      descDe: "JSON to object",
      descAgg: "Aggregate vars",
      descAssign: "Set variable",
      descPlugin: "Call plugin",
      descHttp: "HTTP call",
      descSub: "Call subflow",
      descKnowledgeRetriever: "Search knowledge",
      descKnowledgeIndexer: "Write knowledge",
      descLtm: "Long-term memory",
      descDb: "Query database",
      descDatabaseInsert: "Insert database",
      descDatabaseUpdate: "Update database",
      descDatabaseDelete: "Delete database",
      descDatabaseCustomSql: "Run custom SQL",
      descCreateConversation: "Create conversation",
      descConversationList: "List conversations",
      descConversationUpdate: "Update conversation",
      descConversationDelete: "Delete conversation",
      descConversationHistory: "Get history",
      descClearConversationHistory: "Clear history",
      descMessageList: "List messages",
      descCreateMessage: "Create message",
      descEditMessage: "Edit message",
      descDeleteMessage: "Delete message",
      descInputReceiver: "Wait for user input",
      descOutputEmitter: "Emit intermediate output"
    },
    properties: {
      title: "Node Properties",
      basic: "Common",
      labelTitle: "Title",
      labelKey: "Node Key",
      labelType: "Node Type",
      nodeConfig: "Node Config",
      configJson: "Config JSON",
      inputMap: "Input Mapping",
      mapHint: "Map node fields to upstream variables",
      phField: "Field",
      phRef: "Variable reference"
    },
    testRun: {
      tabRun: "Run",
      tabTrace: "Trace",
      tabVersions: "Versions",
      inputParams: "Input Params",
      syncRun: "Sync Run",
      streamRun: "Stream Run",
      cancel: "Cancel",
      execLog: "Execution Log",
      clearLog: "Clear",
      logEmpty: "No logs",
      finalOutput: "Final Output",
      watchVariables: "Variable Watch",
      noVariableSnapshot: "No variable snapshot",
      waitUser: "Waiting user input",
      phAnswer: "Enter answer",
      submitAnswer: "Submit",
      traceHint: "Run once to inspect timeline",
      totalTime: "Total Time",
      status: "Status",
      nodeDetails: "Node Details",
      publishNew: "Publish New Version",
      noVersions: "No versions",
      noDesc: "No description",
      evtExecutionStart: "Execution Started",
      evtNodeStart: "Node Started",
      evtNodeOutput: "Node Output",
      evtNodeComplete: "Node Completed",
      evtNodeFailed: "Node Failed",
      evtLlmOutput: "LLM Output",
      evtExecutionComplete: "Execution Completed",
      evtExecutionFailed: "Execution Failed",
      evtExecutionCancelled: "Execution Cancelled",
      evtExecutionInterrupted: "Execution Interrupted",
      evtWorkflowError: "Workflow Error",
      jsonInvalid: "Invalid input JSON",
      cancelled: "Cancelled",
      resumed: "Resumed",
      syncStart: "Start sync execution",
      runFailed: "Run failed",
      syncComplete: "Sync completed in {ms}ms",
      streamStart: "Start stream execution",
      execCreated: "Execution created: {id}",
      nodeStart: "Node started: {key}",
      nodeOutput: "Node output: {key} => {out}",
      nodeComplete: "Node completed: {key} ({ms}ms)",
      nodeFailed: "Node failed: {key}, {msg}",
      streamComplete: "Stream completed: {id}",
      execFailed: "Execution failed",
      execCancelled: "Execution cancelled: {id}",
      execInterrupted: "Execution interrupted: {type}",
      interruptQuestion: "Node {node} interrupted, type {type}",
      traceLoadFailed: "Failed to load trace"
    },
    forms: {
      start: {
        defaultVariables: "Default Variables",
        variableName: "Variable Name",
        defaultValue: "Default Value",
        addVariable: "+ Add Variable",
        autoSaveHistory: "Auto Save History"
      },
      end: {
        terminationMode: "Termination Mode",
        returnVariables: "Return Variables",
        answerText: "Answer Text",
        outputMappings: "Output Mappings",
        sourceVariable: "Source Variable",
        targetField: "Target Field",
        addMapping: "+ Add Mapping",
        templateText: "Template Text",
        streamOutput: "Stream Output"
      },
      selector: {
        matchMode: "Match Mode",
        matchAll: "Match All (AND)",
        matchAny: "Match Any (OR)",
        conditions: "Condition Builder",
        fieldPath: "Variable path, e.g. input.score",
        compareValue: "Compare Value",
        addCondition: "+ Add Condition",
        fallbackExpression: "Fallback Expression (Optional)"
      },
      loop: {
        mode: "Loop Mode",
        modeCount: "By Count",
        modeWhile: "While Condition",
        modeForEach: "For Each",
        maxIterations: "Max Iterations",
        indexVariable: "Index Variable",
        condition: "Loop Condition Expression",
        conditionPlaceholder: "e.g.: {{loop_index}} < 10",
        collectionPath: "Collection Path",
        collectionPathPlaceholder: "e.g.: input.items",
        itemVariable: "Item Variable",
        itemIndexVariable: "Item Index Variable"
      },
      batch: {
        collectionPath: "Batch Input Path",
        collectionPathPlaceholder: "e.g.: input.records",
        parallelism: "Parallelism",
        itemTimeoutMs: "Item Timeout (ms)",
        onError: "On Error",
        onErrorContinue: "Continue",
        onErrorFailFast: "Fail Fast",
        outputKey: "Output Variable"
      },
      assignVariable: {
        variableName: "Variable Name",
        valueExpression: "Value Expression",
        scope: "Scope",
        scopeWorkflow: "Workflow",
        scopeLoop: "Loop Scope",
        scopeSession: "Session Scope",
        overwrite: "Overwrite Existing Value"
      },
      llm: {
        provider: "Provider",
        model: "Model",
        systemPrompt: "System Prompt",
        prompt: "User Prompt Template",
        temperature: "Temperature",
        maxTokens: "Max Output Tokens",
        stream: "Stream Output",
        outputKey: "Output Variable"
      },
      textProcessor: {
        mode: "Process Mode",
        modeTemplate: "Template Render",
        modeReplace: "Replace",
        modeExtract: "Extract",
        inputPath: "Input Path",
        templateText: "Template / Expression",
        outputKey: "Output Variable"
      },
      json: {
        direction: "Direction",
        serialize: "Object → JSON",
        deserialize: "JSON → Object",
        inputPath: "Input Path",
        pretty: "Pretty Output",
        outputKey: "Output Variable"
      },
      http: {
        method: "Method",
        url: "Request URL",
        headersJson: "Headers JSON",
        body: "Request Body",
        timeoutMs: "Timeout (ms)",
        outputKey: "Output Variable"
      },
      questionAnswer: {
        questionTemplate: "Question Template",
        timeoutSeconds: "Timeout (seconds)",
        answerKey: "Answer Variable",
        allowEmpty: "Allow Empty Answer",
        defaultQuestionTemplate: "Please provide required information."
      },
      variableAggregator: {
        mode: "Aggregation Mode",
        modeObject: "Object",
        modeArray: "Array",
        modeMerge: "Deep Merge",
        sourcePaths: "Source Paths",
        addSourcePath: "+ Add Source",
        outputKey: "Output Variable"
      },
      subWorkflow: {
        subWorkflowId: "Sub Workflow ID",
        inheritVariables: "Inherit Parent Variables",
        inputsVariable: "Input Variable Path",
        maxDepth: "Max Nesting Depth",
        outputKey: "Output Variable"
      },
      message: {
        nodeType: "Node Type",
        conversationId: "Conversation ID",
        messageId: "Message ID",
        content: "Message Content",
        pageSize: "Page Size",
        outputKey: "Output Variable"
      },
      plugin: {
        pluginKey: "Plugin Key",
        method: "Method",
        inputJson: "Input JSON",
        timeoutMs: "Timeout (ms)",
        outputKey: "Output Variable"
      },
      ltm: {
        action: "Action",
        actionRead: "Read",
        actionWrite: "Write",
        actionDelete: "Delete",
        namespace: "Namespace",
        keyName: "Key",
        valuePath: "Value Path",
        outputKey: "Output Variable"
      },
      loopControl: {
        signal: "Control Signal",
        reason: "Reason",
        reasonPlaceholder: "Optional reason for break/continue",
        outputKey: "Output Variable"
      },
      knowledgeWrite: {
        datasetId: "Dataset ID",
        title: "Document Title",
        titlePlaceholder: "Document Title",
        contentPath: "Content Path",
        chunkSize: "Chunk Size",
        outputKey: "Output Variable"
      },
      knowledgeSearch: {
        datasetId: "Dataset ID",
        queryPath: "Query Path",
        minScore: "Minimum Score",
        outputKey: "Output Variable"
      },
      io: {
        nodeType: "Node Type",
        templateText: "Output Template",
        prompt: "Prompt",
        defaultPrompt: "Please continue your input.",
        timeoutSeconds: "Timeout (seconds)",
        outputKey: "Variable Name"
      },
      intentDetector: {
        intents: "Candidate Intents (one per line)",
        inputPath: "Input Path",
        threshold: "Low Confidence Threshold",
        outputKey: "Output Variable"
      },
      generic: {
        advancedConfigTip: "This node is edited with advanced JSON config."
      },
      database: {
        databaseId: "Database Connection ID",
        tableName: "Table Name",
        mode: "Operation Type",
        whereJson: "Query Condition JSON",
        payloadJson: "Insert Payload JSON",
        updateWhereJson: "Update Condition JSON",
        updatePayloadJson: "Update Payload JSON",
        deleteWhereJson: "Delete Condition JSON",
        outputKey: "Output Variable"
      },
      conversation: {
        nodeType: "Node Type",
        conversationId: "Conversation ID",
        userId: "User ID",
        title: "Conversation Title",
        agentId: "Agent ID (Optional)",
        outputKey: "Output Variable"
      },
      conversationHistory: {
        nodeType: "Node Type",
        conversationId: "Conversation ID",
        limit: "Limit",
        outputKey: "Output Variable"
      },
      code: {
        language: "Execution Language",
        source: "Code (Monaco)",
        outputKey: "Output Variable"
      }
    }
  }
};

function mergeOne(composer: Composer, locale: "zh-CN" | "en-US", message: Record<string, unknown>): void {
  const current = composer.getLocaleMessage(locale) as Record<string, unknown>;
  composer.mergeLocaleMessage(locale, { ...message, ...current });
}

export function mergeWorkflowEditorLocaleMessages(composer: Composer): void {
  mergeOne(composer, "zh-CN", workflowZhCN);
  mergeOne(composer, "en-US", workflowEnUS);
}

