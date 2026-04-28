import type { MicroflowDataType, MicroflowExpression, MicroflowValidationIssue } from "@atlas/microflow/schema";
import type { MicroflowRuntimeVariableValue } from "@atlas/microflow/debug";

/** 与 runtime-expression-contract.md 一致；禁止在前端/后端用 eval 求值。 */
export interface MicroflowRuntimeExpressionRequest {
  expression: MicroflowExpression;
  expectedType?: MicroflowDataType;
  variables: Record<string, MicroflowRuntimeVariableValue>;
  metadataVersion?: string;
}

export interface MicroflowRuntimeExpressionResult {
  value: unknown;
  dataType: MicroflowDataType;
  diagnostics: MicroflowValidationIssue[];
}
