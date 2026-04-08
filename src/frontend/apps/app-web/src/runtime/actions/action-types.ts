/**
 * 统一动作协议类型定义（平台动作协议）。
 *
 * 所有页面行为统一通过 RuntimeAction 描述，
 * 由 ActionExecutor 统一执行。
 */

import type { Id, Key, ValueMap } from "../types/base-types";

export interface RuntimeActionBase<TType extends string, TInput = unknown> {
  id?: Id;
  type: TType;
  name?: string;
  label?: string;
  description?: string;
  /** CEL expression — 为 true 时才执行此动作 */
  when?: string;
  input?: TInput;
  continueOnError?: boolean;
}

export interface NavigateAction extends RuntimeActionBase<"navigate", {
  pageKey: Key;
  params?: ValueMap;
  query?: ValueMap;
  replace?: boolean;
}> {}

export interface OpenDialogAction extends RuntimeActionBase<"openDialog", {
  dialogKey: Key;
  title?: string;
  payload?: ValueMap;
  width?: string | number;
}> {}

export interface SubmitFormAction extends RuntimeActionBase<"submitForm", {
  formKey?: Key;
  validateOnly?: boolean;
}> {}

export interface CallApiAction extends RuntimeActionBase<"callApi", {
  apiKey?: Key;
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  url?: string;
  pathParams?: ValueMap;
  query?: ValueMap;
  body?: unknown;
}> {}

export interface RunFlowAction extends RuntimeActionBase<"runFlow", {
  flowKey: Key;
  input?: ValueMap;
}> {}

export interface RunWorkflowAction extends RuntimeActionBase<"runWorkflow", {
  workflowKey: Key;
  input?: ValueMap;
}> {}

export interface RunAgentAction extends RuntimeActionBase<"runAgent", {
  agentKey: Key;
  input?: ValueMap;
  awaitResult?: boolean;
}> {}

export interface RunApprovalAction extends RuntimeActionBase<"runApproval", {
  input?: ValueMap;
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
  key: Key;
  name?: string;
  actions: RuntimeAction[];
}
