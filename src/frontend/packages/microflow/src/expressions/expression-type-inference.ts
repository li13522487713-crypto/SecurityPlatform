import type { MicroflowDataType, MicroflowExpression, MicroflowVariableSymbol } from "../schema/types";

export function sameMicroflowDataType(left: MicroflowDataType | undefined, right: MicroflowDataType | undefined): boolean {
  if (!left || !right || left.kind === "unknown" || right.kind === "unknown") {
    return true;
  }
  if (left.kind !== right.kind) {
    return false;
  }
  if (left.kind === "object" && right.kind === "object") {
    return left.entityQualifiedName === right.entityQualifiedName;
  }
  if (left.kind === "list" && right.kind === "list") {
    return sameMicroflowDataType(left.itemType, right.itemType);
  }
  if (left.kind === "enumeration" && right.kind === "enumeration") {
    return left.enumerationQualifiedName === right.enumerationQualifiedName;
  }
  return true;
}

export function inferExpressionType(expression: MicroflowExpression | undefined, variables: MicroflowVariableSymbol[]): MicroflowDataType {
  const raw = expression?.raw ?? expression?.text ?? "";
  if (/^(true|false)$/i.test(raw.trim()) || /[<>=!]=?| and | or /.test(raw)) {
    return { kind: "boolean" };
  }
  if (/^'.*'$|^".*"$/.test(raw.trim())) {
    return { kind: "string" };
  }
  if (/^\d+$/.test(raw.trim())) {
    return { kind: "integer" };
  }
  const variableMatch = raw.trim().match(/^\$([A-Za-z_][\w]*)$/);
  if (variableMatch) {
    return variables.find(variable => variable.name === variableMatch[1] || variable.name === `$${variableMatch[1]}`)?.dataType ?? { kind: "unknown", reason: raw };
  }
  return expression?.inferredType ?? { kind: "unknown", reason: "expression" };
}
