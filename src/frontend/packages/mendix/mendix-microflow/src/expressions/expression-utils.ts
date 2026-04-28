import type { MicroflowDataType, MicroflowExpression } from "../schema/types";

export function expressionRaw(value: MicroflowExpression | string | undefined): string {
  return typeof value === "string" ? value : value?.raw ?? value?.text ?? "";
}

export function expressionTypeLabel(dataType?: MicroflowDataType): string {
  if (!dataType) {
    return "unknown";
  }
  if (dataType.kind === "object") {
    return dataType.entityQualifiedName || "object";
  }
  if (dataType.kind === "enumeration") {
    return dataType.enumerationQualifiedName || "enumeration";
  }
  if (dataType.kind === "list") {
    return `List<${expressionTypeLabel(dataType.itemType)}>`;
  }
  if (dataType.kind === "unknown") {
    return dataType.reason ? `unknown (${dataType.reason})` : "unknown";
  }
  return dataType.kind;
}
