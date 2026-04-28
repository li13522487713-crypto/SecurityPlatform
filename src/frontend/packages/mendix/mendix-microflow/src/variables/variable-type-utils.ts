import type { MicroflowDataType } from "../schema/types";

export function dataTypeLabel(dataType?: MicroflowDataType): string {
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
    return `List<${dataTypeLabel(dataType.itemType)}>`;
  }
  if (dataType.kind === "unknown") {
    return dataType.reason ? `unknown (${dataType.reason})` : "unknown";
  }
  return dataType.kind;
}

export function isDataTypeAssignableTo(actual: MicroflowDataType, expectedKinds: MicroflowDataType["kind"][]): boolean {
  return expectedKinds.includes(actual.kind);
}

export function getObjectEntityQualifiedName(dataType?: MicroflowDataType): string | undefined {
  return dataType?.kind === "object" ? dataType.entityQualifiedName : undefined;
}

export function getListItemType(dataType?: MicroflowDataType): MicroflowDataType | undefined {
  return dataType?.kind === "list" ? dataType.itemType : undefined;
}
