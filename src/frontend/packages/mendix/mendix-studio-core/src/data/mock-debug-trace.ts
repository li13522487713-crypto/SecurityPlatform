import type { FlowExecutionTraceSchema } from "@atlas/mendix-schema";

export interface DisplayTraceStep {
  index: number;
  nodeName: string;
  nodeType: string;
  status: "success" | "error" | "running";
  durationMs: number;
  summary: string;
}

export const MOCK_DEBUG_TRACE: FlowExecutionTraceSchema = {
  traceId: "trc_20250530_001",
  flowType: "microflow",
  flowId: "mf_submit_purchase_request",
  startedAt: "2025-05-30T10:30:21.000Z",
  endedAt: "2025-05-30T10:30:21.256Z",
  status: "succeeded",
  inputArguments: {
    Request: {
      RequestNo: "PR-202505-0001",
      Amount: 120000,
      Status: "Draft",
      Reason: "购买办公设备",
      ApplicantName: "张三",
      DepartmentName: "研发部"
    }
  },
  steps: [
    {
      stepId: "step_1",
      nodeId: "node_start",
      nodeType: "startEvent",
      expressionResults: [],
      permissionChecks: [],
      databaseQueries: [],
      uiCommands: [],
      inputSnapshot: {},
      outputSnapshot: {}
    },
    {
      stepId: "step_2",
      nodeId: "node_retrieve_user",
      nodeType: "retrieveObject",
      expressionResults: [],
      permissionChecks: [{ check: "Account.read", allowed: true }],
      databaseQueries: ["SELECT * FROM Account WHERE [%CurrentUser%]"],
      uiCommands: [],
      inputSnapshot: {},
      outputSnapshot: { CurrentUser: { name: "张三", id: "usr_001" } }
    },
    {
      stepId: "step_3",
      nodeId: "node_decision_amount",
      nodeType: "decision",
      expressionResults: [
        { expression: "$Request/Amount > 50000", result: true }
      ],
      permissionChecks: [],
      databaseQueries: [],
      uiCommands: [],
      inputSnapshot: { Amount: 120000 },
      outputSnapshot: { outcome: "true" }
    },
    {
      stepId: "step_4",
      nodeId: "node_change_status",
      nodeType: "changeObject",
      expressionResults: [
        { expression: "PurchaseStatus.NeedFinanceApproval", result: "NeedFinanceApproval" }
      ],
      permissionChecks: [{ check: "PurchaseRequest.write", allowed: true }],
      databaseQueries: [],
      uiCommands: [],
      inputSnapshot: { Status: "Draft" },
      outputSnapshot: { Status: "NeedFinanceApproval" }
    },
    {
      stepId: "step_5",
      nodeId: "node_commit",
      nodeType: "commitObject",
      expressionResults: [],
      permissionChecks: [{ check: "PurchaseRequest.commit", allowed: true }],
      databaseQueries: ["UPDATE PurchaseRequest SET Status='NeedFinanceApproval' WHERE Id='pr_001'"],
      uiCommands: [],
      inputSnapshot: {},
      outputSnapshot: { committed: true }
    },
    {
      stepId: "step_6",
      nodeId: "node_call_workflow",
      nodeType: "callWorkflow",
      expressionResults: [],
      permissionChecks: [{ check: "WF_PurchaseApproval.execute", allowed: true }],
      databaseQueries: [],
      uiCommands: [],
      inputSnapshot: { workflowRef: "wf_purchase_approval" },
      outputSnapshot: { workflowInstanceId: "WF-202505-0001" }
    },
    {
      stepId: "step_7",
      nodeId: "node_show_message",
      nodeType: "showMessage",
      expressionResults: [],
      permissionChecks: [],
      databaseQueries: [],
      uiCommands: [
        { type: "showMessage", level: "info", message: "采购申请已提交审批" }
      ],
      inputSnapshot: {},
      outputSnapshot: {}
    },
    {
      stepId: "step_8",
      nodeId: "node_end",
      nodeType: "endEvent",
      expressionResults: [{ expression: "true", result: true }],
      permissionChecks: [],
      databaseQueries: [],
      uiCommands: [],
      inputSnapshot: {},
      outputSnapshot: { returnValue: true }
    }
  ]
};

export const DISPLAY_TRACE_STEPS: DisplayTraceStep[] = [
  { index: 1, nodeName: "Start Event", nodeType: "startEvent", status: "success", durationMs: 2, summary: "开始执行" },
  { index: 2, nodeName: "Retrieve Object (CurrentUser)", nodeType: "retrieveObject", status: "success", durationMs: 12, summary: "查询用户：张三" },
  { index: 3, nodeName: "Decision (Amount > 50000)", nodeType: "decision", status: "success", durationMs: 5, summary: "结果：true（120000.00 > 50000）" },
  { index: 4, nodeName: "Change Object (Request)", nodeType: "changeObject", status: "success", durationMs: 18, summary: "Status = NeedFinanceApproval" },
  { index: 5, nodeName: "Commit Object", nodeType: "commitObject", status: "success", durationMs: 23, summary: "提交成功" },
  { index: 6, nodeName: "Call Workflow (WF_PurchaseApproval)", nodeType: "callWorkflow", status: "success", durationMs: 45, summary: "启动流程实例 WF-202505-0001" },
  { index: 7, nodeName: "Show Message", nodeType: "showMessage", status: "success", durationMs: 3, summary: "采购申请已提交审批" },
  { index: 8, nodeName: "End Event", nodeType: "endEvent", status: "success", durationMs: 1, summary: "返回 true" }
];

export const MOCK_VALIDATION_ERRORS = [
  {
    severity: "error" as const,
    code: "E1001",
    message: "绑定的属性 Amount 不存在于实体 PurchaseRequest",
    target: {
      kind: "Page",
      id: "page_purchase_request_edit",
      path: "PurchaseRequest_EditPage > numberInput1"
    }
  },
  {
    severity: "error" as const,
    code: "E2003",
    message: "Microflow 调用参数类型不匹配：Request 期望 PurchaseRequest",
    target: {
      kind: "Microflow",
      id: "mf_submit_purchase_request",
      path: "MF_SubmitPurchaseRequest > Call Microflow"
    }
  },
  {
    severity: "error" as const,
    code: "E3005",
    message: "Workflow User Task 缺少 Outcome 配置",
    target: {
      kind: "Workflow",
      id: "wf_purchase_approval",
      path: "WF_PurchaseApproval > ManagerApprove"
    }
  },
  {
    severity: "warning" as const,
    code: "W1002",
    message: "实体 PurchaseRequest 未配置任何唯一性校验规则",
    target: {
      kind: "Entity",
      id: "ent_purchase_request",
      path: "PurchaseRequest"
    }
  }
];
