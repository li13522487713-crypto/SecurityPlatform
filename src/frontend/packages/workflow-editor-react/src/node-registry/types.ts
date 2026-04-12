import type { WorkflowNodeTypeKey } from "../types";
import type React from "react";

export type FormFieldKind =
  | "text"
  | "textarea"
  | "number"
  | "switch"
  | "select"
  | "json"
  | "code"
  | "keyValue"
  | "slider"
  | "radioGroup"
  | "codeEditor"
  | "tagInput"
  | "arrayEditor"
  | "objectEditor"
  | "conditionBuilder"
  | "variableRefPicker"
  | "expression";

export interface FormSelectOption {
  label: string;
  value: string | number | boolean;
}

export interface FormFieldSchema {
  key: string;
  label: string;
  kind: FormFieldKind;
  path: string;
  placeholder?: string;
  required?: boolean;
  rows?: number;
  min?: number;
  max?: number;
  step?: number;
  options?: FormSelectOption[];
  languagePath?: string;
  editorLanguage?: string;
  itemFields?: Array<{
    key: string;
    label: string;
    kind?: "text" | "textarea" | "number" | "switch" | "select";
    options?: FormSelectOption[];
  }>;
}

export interface FormSectionSchema {
  key: string;
  title: string;
  fields: FormFieldSchema[];
  advanced?: boolean;
}

export interface NodeValidationContext {
  type: WorkflowNodeTypeKey;
  config: Record<string, unknown>;
}

export interface NodeDefinition {
  type: WorkflowNodeTypeKey;
  sections: FormSectionSchema[];
  getFallbackDefaults: () => Record<string, unknown>;
  validate?: (ctx: NodeValidationContext) => string[];
}

export interface NodeMeta {
  nodeDTOType: string;
  size: { width: number; height: number };
  defaultPorts: Array<{ type: "input" | "output"; portID?: string }>;
  useDynamicPort?: boolean;
  subCanvas?: { enabled: boolean; key?: string };
  deleteDisable?: boolean;
  copyDisable?: boolean;
  headerReadonly?: boolean;
  helpLink?: string;
  nodeMetaPath?: string;
  outputsPath?: string;
  inputParametersPath?: string;
}

export type ValidateTrigger = "change" | "blur" | "submit";
export type ValidatorFn = (value: unknown, values: Record<string, unknown>) => string | undefined;
export type EffectFn = (values: Record<string, unknown>) => Record<string, unknown> | void;

export interface FormMetaV2 {
  render: React.ComponentType<{ nodeKey: string }>;
  validate?: Record<string, ValidatorFn>;
  validateTrigger?: ValidateTrigger;
  effect?: EffectFn[];
  formatOnInit?: (values: Record<string, unknown>) => Record<string, unknown>;
  formatOnSubmit?: (values: Record<string, unknown>) => Record<string, unknown>;
}

export interface VariablesMeta {
  outputsPathList?: string[];
  inputsPathList?: string[];
  batchInputListPath?: string;
}

export interface WorkflowNodeRegistryV2 extends NodeDefinition {
  meta?: NodeMeta;
  formMeta?: FormMetaV2;
  variablesMeta?: VariablesMeta;
  onInit?: (nodeJSON: Record<string, unknown>, context: unknown) => Promise<void>;
  onDispose?: (nodeJSON: Record<string, unknown>, context: unknown) => void;
  checkError?: (nodeJSON: Record<string, unknown>, context: unknown) => string | undefined;
  beforeNodeSubmit?: (nodeJSON: Record<string, unknown>) => void;
}

