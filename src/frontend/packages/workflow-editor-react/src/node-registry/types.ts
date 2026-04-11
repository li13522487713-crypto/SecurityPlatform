import type { WorkflowNodeTypeKey } from "../types";

export type FormFieldKind = "text" | "textarea" | "number" | "switch" | "select" | "json" | "code" | "keyValue";

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
  options?: FormSelectOption[];
  languagePath?: string;
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

