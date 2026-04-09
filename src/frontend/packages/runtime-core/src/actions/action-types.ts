/**
 * Unified runtime action protocol types.
 */

export type RuntimeId = string | number;
export type RuntimeKey = string;
export type RuntimeValueMap = Record<string, unknown>;

export interface RuntimeActionBase<TType extends string, TInput = unknown> {
  id?: RuntimeId;
  type: TType;
  name?: string;
  label?: string;
  description?: string;
  when?: string;
  input?: TInput;
  continueOnError?: boolean;
}

export interface NavigateAction extends RuntimeActionBase<"navigate", {
  pageKey: RuntimeKey;
  params?: RuntimeValueMap;
  query?: RuntimeValueMap;
  replace?: boolean;
}> {}

export interface OpenDialogAction extends RuntimeActionBase<"openDialog", {
  dialogKey: RuntimeKey;
  title?: string;
  payload?: RuntimeValueMap;
  width?: string | number;
}> {}

export interface SubmitFormAction extends RuntimeActionBase<"submitForm", {
  formKey?: RuntimeKey;
  validateOnly?: boolean;
}> {}

export interface CallApiAction extends RuntimeActionBase<"callApi", {
  apiKey?: RuntimeKey;
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  url?: string;
  pathParams?: RuntimeValueMap;
  query?: RuntimeValueMap;
  body?: unknown;
}> {}

export interface RunFlowAction extends RuntimeActionBase<"runFlow", {
  flowKey: RuntimeKey;
  input?: RuntimeValueMap;
}> {}

export interface RunWorkflowAction extends RuntimeActionBase<"runWorkflow", {
  workflowKey: RuntimeKey;
  input?: RuntimeValueMap;
}> {}

export interface RunAgentAction extends RuntimeActionBase<"runAgent", {
  agentKey: RuntimeKey;
  input?: RuntimeValueMap;
  awaitResult?: boolean;
}> {}

export interface RunApprovalAction extends RuntimeActionBase<"runApproval", {
  input?: RuntimeValueMap;
}> {}

export interface RefreshAction extends RuntimeActionBase<"refresh", {
  target?: string;
}> {}

export interface SetVarAction extends RuntimeActionBase<"setVar", {
  scope?: "global" | "page" | "local";
  name: string;
  value: unknown;
}> {}

export interface BranchAction extends RuntimeActionBase<"branch", {
  condition: string;
  then: RuntimeAction[];
  else?: RuntimeAction[];
}> {}

export interface ForeachAction extends RuntimeActionBase<"foreach", {
  items: string;
  itemName?: string;
  actions: RuntimeAction[];
}> {}

export type RuntimeAction =
  | NavigateAction
  | OpenDialogAction
  | SubmitFormAction
  | CallApiAction
  | RunFlowAction
  | RunWorkflowAction
  | RunApprovalAction
  | RunAgentAction
  | RefreshAction
  | SetVarAction
  | BranchAction
  | ForeachAction;

export interface RuntimeActionDefinition {
  key: RuntimeKey;
  name?: string;
  actions: RuntimeAction[];
}
