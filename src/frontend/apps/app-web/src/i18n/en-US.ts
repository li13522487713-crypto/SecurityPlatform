export default {
  common: {
    success: "Success",
    error: "Error",
    loading: "Loading...",
    confirm: "Confirm",
    cancel: "Cancel",
    ok: "OK"
  },
  auth: {
    login: "Login",
    logout: "Logout",
    username: "Username",
    password: "Password",
    tenantId: "Tenant ID",
    loginSubmit: "Login & Enter App",
    loginFailed: "Login failed",
    sessionExpired: "Session expired, please login again"
  },
  appLogin: {
    title: "App Login",
    tenantIdPlaceholder: "00000000-0000-0000-0000-000000000001",
    usernamePlaceholder: "Enter username",
    passwordPlaceholder: "Enter password",
    tenantIdRequired: "Please enter tenant ID",
    usernameRequired: "Please enter username",
    passwordRequired: "Please enter password",
    invalidTenantId: "Please enter a valid tenant ID"
  },
  appEntry: {
    entering: "Entering application",
    resolving: "Resolving default page and runtime entry...",
    unavailable: "Entry unavailable",
    missingAppKey: "Missing application key.",
    noPages: "No accessible pages configured for this application.",
    enterFailed: "Failed to enter application"
  },
  appRuntime: {
    pageTitle: "Runtime Page"
  },
  amis: {
    notice: "Notice",
    confirmTitle: "Please confirm",
    requestFailed: "Request failed"
  },
  runtimePage: {
    emptyNoPage: "No runnable page found. Publish a page from the designer first.",
    defaultTitle: "Runtime page",
    loadFailed: "Failed to load runtime page"
  },
  crud: {
    deleteSuccess: "Deleted successfully",
    deleteFailed: "Delete failed"
  },
  ai: {
    chat: {
      avatarAssistant: "AI",
      avatarUser: "User",
      convList: "Conversations",
      newConv: "+ New",
      newConvTitle: "New chat",
      deleteConvConfirm: "Delete this conversation?",
      emptyConv: "No conversations yet. Click New to start.",
      defaultAgentName: "Agent chat",
      clearContextTip: "Clear context (keep history, but new messages won't use old context)",
      clearContext: "Clear context",
      clearHistoryTip: "Clear all message history",
      clearHistory: "Clear history",
      emptySelect: "Select or create a conversation to start",
      enableRag: "Enable knowledge base (RAG)",
      attachImage: "Upload image",
      startRecord: "Start recording",
      stopRecord: "Stop recording",
      clearAttachments: "Clear attachments",
      recordUnsupported: "Recording is not supported in this browser",
      recordAttachmentHint: "Please transcribe this audio first",
      placeholderStreaming: "Replying…",
      placeholderInput: "Type a message, Ctrl+Enter to send",
      newConversationTitle: "New chat",
      loadConvFailed: "Failed to load conversations",
      loadMsgFailed: "Failed to load messages",
      createConvFailed: "Failed to create conversation",
      clearContextOk: "Context cleared",
      clearHistoryOk: "History cleared",
      opFailed: "Operation failed",
      reactPanelTitle: "ReAct trace",
      reactThought: "Thought",
      reactAction: "Action",
      reactObservation: "Observation",
      reactFinal: "Final answer",
      missingAgentTitle: "Agent required",
      missingAgentDesc: "Provide a valid Agent ID in the URL, e.g. /apps/{app}/ai/chat/{agentId}"
    }
  },
  layout: {
    backToLogin: "Back to Login",
    profile: "Profile",
    appRuntime: "App Runtime"
  },
  route: {
    home: "Home",
    agentChat: "Agent Chat",
    aiAssistant: "AI Assistant",
    approvalWorkspace: "Approval Workspace",
    approvalDetail: "Approval Detail",
    reports: "Reports",
    dashboards: "Dashboards",
    visualization: "Visualization"
  }
};
