/**
 * 统一动作协议类型定义（平台动作协议）。
 *
 * 所有页面行为统一通过 RuntimeAction 描述，
 * 由 ActionExecutor 统一执行。
 */

export type RuntimeAction =
  | NavigateAction
  | OpenDialogAction
  | SubmitFormAction
  | CallApiAction
  | RunFlowAction
  | RunWorkflowAction
  | RunAgentAction
  | RefreshAction
  | SetVarAction
  | BranchAction
  | ForeachAction;

export interface NavigateAction {
  type: "navigate";
  pageKey: string;
  params?: Record<string, unknown>;
}

export interface OpenDialogAction {
  type: "openDialog";
  dialogKey: string;
  payload?: Record<string, unknown>;
}

export interface SubmitFormAction {
  type: "submitForm";
  formKey?: string;
}

export interface CallApiAction {
  type: "callApi";
  apiKey: string;
  method?: string;
  input?: unknown;
}

export interface RunFlowAction {
  type: "runFlow";
  flowKey: string;
  input?: unknown;
}

export interface RunWorkflowAction {
  type: "runWorkflow";
  workflowKey: string;
  input?: unknown;
}

export interface RunAgentAction {
  type: "runAgent";
  agentKey: string;
  input?: unknown;
}

export interface RefreshAction {
  type: "refresh";
  target?: string;
}

export interface SetVarAction {
  type: "setVar";
  name: string;
  value: unknown;
}

export interface BranchAction {
  type: "branch";
  when: string;
  then: RuntimeAction[];
  else?: RuntimeAction[];
}

export interface ForeachAction {
  type: "foreach";
  items: string;
  do: RuntimeAction[];
}
