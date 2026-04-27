import type { MicroflowDataType, MicroflowExpressionDiagnostic } from "../schema/types";
import type { MicroflowExpressionAstNode } from "./expression-ast";
import type { MicroflowExpressionToken } from "./expression-tokenizer";

export interface TextRange {
  start: number;
  end: number;
}

export type ExpressionDiagnostic = MicroflowExpressionDiagnostic & {
  id: string;
  code: string;
};

export type ExpressionReference =
  | {
      kind: "variable";
      raw: string;
      variableName: string;
      range: TextRange;
    }
  | {
      kind: "memberAccess";
      raw: string;
      variableName: string;
      memberName: string;
      path: string[];
      range: TextRange;
    }
  | {
      kind: "functionCall";
      raw: string;
      functionName: string;
      range: TextRange;
    };

export interface ExpressionParseResult {
  raw: string;
  ast?: MicroflowExpressionAstNode;
  tokens?: MicroflowExpressionToken[];
  references: ExpressionReference[];
  diagnostics: ExpressionDiagnostic[];
  variables: string[];
  attributeAccesses: Array<{ variableName: string; attributeName: string }>;
}

export interface ExpressionTypeInferenceResult {
  inferredType: MicroflowDataType;
  confidence: "high" | "medium" | "low";
  diagnostics: ExpressionDiagnostic[];
  references?: ExpressionReference[];
}

export interface ExpressionValidationResult extends ExpressionTypeInferenceResult {
  references: ExpressionReference[];
}

export function expressionDiagnostic(input: {
  code: string;
  message: string;
  severity?: ExpressionDiagnostic["severity"];
  range?: TextRange;
  variableName?: string;
  memberName?: string;
  expectedType?: MicroflowDataType;
  actualType?: MicroflowDataType;
}): ExpressionDiagnostic {
  return {
    id: `${input.code}:${input.range?.start ?? 0}:${input.variableName ?? input.memberName ?? ""}`,
    severity: input.severity ?? "error",
    code: input.code,
    message: input.message,
    range: input.range,
    variableName: input.variableName,
    memberName: input.memberName,
    expectedType: input.expectedType,
    actualType: input.actualType,
  };
}
