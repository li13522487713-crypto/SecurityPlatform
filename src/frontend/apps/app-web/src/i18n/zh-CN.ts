export default {
  common: {
    success: "成功",
    error: "错误",
    loading: "加载中...",
    confirm: "确认",
    cancel: "取消",
    ok: "确定"
  },
  auth: {
    login: "登录",
    logout: "退出登录",
    username: "用户名",
    password: "密码",
    tenantId: "租户 ID",
    loginSubmit: "登录并进入应用",
    loginFailed: "登录失败",
    sessionExpired: "会话已过期，请重新登录"
  },
  appLogin: {
    title: "应用登录",
    tenantIdPlaceholder: "00000000-0000-0000-0000-000000000001",
    usernamePlaceholder: "请输入用户名",
    passwordPlaceholder: "请输入密码",
    tenantIdRequired: "请输入租户 ID",
    usernameRequired: "请输入用户名",
    passwordRequired: "请输入密码",
    invalidTenantId: "请输入有效的租户 ID"
  },
  appEntry: {
    entering: "正在进入应用",
    resolving: "正在解析默认页面和运行时入口。",
    unavailable: "入口暂不可用",
    missingAppKey: "缺少应用标识。",
    noPages: "应用尚未配置可访问页面。",
    enterFailed: "进入应用失败"
  },
  appRuntime: {
    pageTitle: "运行时页面"
  },
  amis: {
    notice: "提示",
    confirmTitle: "请确认",
    requestFailed: "请求失败"
  },
  runtimePage: {
    emptyNoPage: "未找到可运行页面，请先在设计器发布页面",
    defaultTitle: "运行态页面",
    loadFailed: "加载运行态页面失败"
  },
  crud: {
    deleteSuccess: "删除成功",
    deleteFailed: "删除失败"
  },
  ai: {
    chat: {
      avatarAssistant: "AI",
      avatarUser: "用户",
      convList: "对话列表",
      newConv: "+ 新建",
      newConvTitle: "新对话",
      deleteConvConfirm: "确认删除该对话？",
      emptyConv: "暂无对话，点击「新建」开始",
      defaultAgentName: "Agent 对话",
      clearContextTip: "清除上下文（保留历史，但新消息不使用旧上下文）",
      clearContext: "清除上下文",
      clearHistoryTip: "清除全部历史消息",
      clearHistory: "清除历史",
      emptySelect: "选择或新建一个对话开始聊天",
      enableRag: "启用知识库 (RAG)",
      attachImage: "上传图片",
      startRecord: "开始录音",
      stopRecord: "结束录音",
      clearAttachments: "清空附件",
      recordUnsupported: "当前浏览器不支持录音",
      recordAttachmentHint: "请优先识别这段语音内容",
      placeholderStreaming: "正在回复中…",
      placeholderInput: "输入消息，Ctrl+Enter 发送",
      newConversationTitle: "新对话",
      loadConvFailed: "加载对话列表失败",
      loadMsgFailed: "加载消息失败",
      createConvFailed: "创建对话失败",
      clearContextOk: "上下文已清除",
      clearHistoryOk: "历史已清除",
      opFailed: "操作失败",
      reactPanelTitle: "ReAct 执行过程",
      reactThought: "思考（Thought）",
      reactAction: "行动（Action）",
      reactObservation: "观察（Observation）",
      reactFinal: "最终回答（Final）",
      missingAgentTitle: "缺少 Agent",
      missingAgentDesc: "请在地址中提供有效的 Agent ID，例如：/apps/{应用}/ai/chat/{AgentId}"
    }
  },
  layout: {
    backToLogin: "返回登录",
    profile: "个人中心",
    appRuntime: "应用运行时"
  },
  route: {
    home: "首页",
    agentChat: "Agent 聊天",
    aiAssistant: "AI 助手",
    approvalWorkspace: "审批工作台",
    approvalDetail: "审批详情",
    reports: "报表",
    dashboards: "仪表盘",
    visualization: "可视化"
  },
  approvalWorkspace: {
    pageTitle: "审批工作台",
    tabPending: "待办",
    tabDone: "已办",
    tabRequests: "我发起",
    tabCc: "抄送我",
    colTitle: "标题",
    colFlow: "流程",
    colNode: "当前节点",
    colStatus: "状态",
    colTime: "时间",
    colRead: "已读",
    statusPending: "待处理",
    statusApproved: "已通过",
    statusRejected: "已驳回",
    statusRunning: "进行中",
    statusCompleted: "已完成",
    statusCancelled: "已取消",
    readYes: "已读",
    readNo: "未读",
    empty: "暂无数据",
    loadFailed: "加载失败"
  }
};
