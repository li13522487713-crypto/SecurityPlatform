import type { TextRange } from "./expression-types";

export interface MicroflowExpressionAstBase {
  kind: string;
  raw: string;
  range: TextRange;
}

export interface MicroflowExpressionLiteralNode extends MicroflowExpressionAstBase {
  kind: "literal";
  literalKind: "string" | "integer" | "decimal" | "boolean" | "null" | "empty";
  value: string | number | boolean | null;
}

export interface MicroflowExpressionVariableNode extends MicroflowExpressionAstBase {
  kind: "variable";
  variableName: string;
}

export interface MicroflowExpressionMemberAccessNode extends MicroflowExpressionAstBase {
  kind: "memberAccess";
  variableName: string;
  path: string[];
}

export interface MicroflowExpressionBinaryNode extends MicroflowExpressionAstBase {
  kind: "binary";
  operator: "=" | "!=" | ">" | "<" | ">=" | "<=" | "and" | "or" | "+" | "-" | "*" | "/";
  left: MicroflowExpressionAstNode;
  right: MicroflowExpressionAstNode;
}

export interface MicroflowExpressionUnaryNode extends MicroflowExpressionAstBase {
  kind: "unary";
  operator: "not" | "-";
  argument: MicroflowExpressionAstNode;
}

export interface MicroflowExpressionFunctionCallNode extends MicroflowExpressionAstBase {
  kind: "functionCall";
  functionName: string;
  args: MicroflowExpressionAstNode[];
}

export interface MicroflowExpressionIfNode extends MicroflowExpressionAstBase {
  kind: "if";
  condition: MicroflowExpressionAstNode;
  thenBranch: MicroflowExpressionAstNode;
  elseBranch: MicroflowExpressionAstNode;
}

export interface MicroflowExpressionEnumValueNode extends MicroflowExpressionAstBase {
  kind: "enumValue";
  qualifiedName: string;
  valueName: string;
}

export interface MicroflowExpressionUnknownNode extends MicroflowExpressionAstBase {
  kind: "unknown";
  reason: string;
  children?: MicroflowExpressionAstNode[];
}

export type MicroflowExpressionAstNode =
  | MicroflowExpressionLiteralNode
  | MicroflowExpressionVariableNode
  | MicroflowExpressionMemberAccessNode
  | MicroflowExpressionBinaryNode
  | MicroflowExpressionUnaryNode
  | MicroflowExpressionFunctionCallNode
  | MicroflowExpressionIfNode
  | MicroflowExpressionEnumValueNode
  | MicroflowExpressionUnknownNode;
