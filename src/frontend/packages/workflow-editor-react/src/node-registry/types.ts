import type { WorkflowNodeTypeKey } from "../types";

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
  | "variableRefPicker";

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

