import type { MicroflowDataType } from "../schema/types";

export type MendixDataType =
  | "String"
  | "Boolean"
  | "Integer"
  | "Long"
  | "Decimal"
  | "DateTime"
  | "Enumeration"
  | "Object"
  | "List"
  | "Nothing"
  | "Empty";

export interface MendixFunctionSignature {
  name: string;
  signature: string;
}

export interface MicroflowVariable {
  name: string;
  type: MendixDataType;
  dataType?: MicroflowDataType;
  entityType?: string;
  optional?: boolean;
  documentation?: string;
  attributes?: string[];
}

export interface MicroflowParameter extends MicroflowVariable {
  optional?: boolean;
}

export const MENDIX_FUNCTIONS: readonly MendixFunctionSignature[] = [
  { name: "toLowerCase", signature: "toLowerCase(String) → String" },
  { name: "toUpperCase", signature: "toUpperCase(String) → String" },
  { name: "substring", signature: "substring(String, Integer, Integer?) → String" },
  { name: "find", signature: "find(String, String) → Integer" },
  { name: "findLast", signature: "findLast(String, String) → Integer" },
  { name: "contains", signature: "contains(String, String) → Boolean" },
  { name: "startsWith", signature: "startsWith(String, String) → Boolean" },
  { name: "endsWith", signature: "endsWith(String, String) → Boolean" },
  { name: "trim", signature: "trim(String) → String" },
  { name: "isMatch", signature: "isMatch(String, String) → Boolean" },
  { name: "replaceAll", signature: "replaceAll(String, String, String) → String" },
  { name: "replaceFirst", signature: "replaceFirst(String, String, String) → String" },
  { name: "urlEncode", signature: "urlEncode(String) → String" },
  { name: "urlDecode", signature: "urlDecode(String) → String" },
  { name: "round", signature: "round(Decimal, Integer?) → Decimal" },
  { name: "floor", signature: "floor(Decimal) → Long" },
  { name: "ceil", signature: "ceil(Decimal) → Long" },
  { name: "abs", signature: "abs(Number) → Number" },
  { name: "pow", signature: "pow(Number, Number) → Decimal" },
  { name: "sqrt", signature: "sqrt(Number) → Decimal" },
  { name: "max", signature: "max(Number...) → Number" },
  { name: "min", signature: "min(Number...) → Number" },
  { name: "random", signature: "random() → Decimal" },
  { name: "isNew", signature: "isNew(Object) → Boolean" },
  { name: "isSynced", signature: "isSynced(Object) → Boolean" },
  { name: "formatDateTime", signature: "formatDateTime(DateTime, String) → String" },
  { name: "toDateTime", signature: "toDateTime(String, String) → DateTime" },
  { name: "addDays", signature: "addDays(DateTime, Integer) → DateTime" },
  { name: "addMonths", signature: "addMonths(DateTime, Integer) → DateTime" },
  { name: "addYears", signature: "addYears(DateTime, Integer) → DateTime" },
] as const;

export function toMendixDataType(value: MicroflowDataType): MendixDataType {
  switch (value.kind) {
    case "string":
      return "String";
    case "boolean":
      return "Boolean";
    case "integer":
      return "Integer";
    case "long":
      return "Long";
    case "decimal":
      return "Decimal";
    case "dateTime":
      return "DateTime";
    case "enumeration":
      return "Enumeration";
    case "object":
      return "Object";
    case "list":
      return "List";
    case "void":
      return "Nothing";
    case "unknown":
    case "fileDocument":
    case "binary":
    case "json":
    default:
      return "Empty";
  }
}

export function fromMendixDataType(type: MendixDataType): MicroflowDataType {
  if (type === "String") {
    return { kind: "string" };
  }
  if (type === "Boolean") {
    return { kind: "boolean" };
  }
  if (type === "Integer") {
    return { kind: "integer" };
  }
  if (type === "Long") {
    return { kind: "long" };
  }
  if (type === "Decimal") {
    return { kind: "decimal" };
  }
  if (type === "DateTime") {
    return { kind: "dateTime" };
  }
  if (type === "Enumeration") {
    return { kind: "enumeration", enumerationQualifiedName: "" };
  }
  if (type === "Object") {
    return { kind: "object", entityQualifiedName: "" };
  }
  if (type === "List") {
    return { kind: "list", itemType: { kind: "unknown" } };
  }
  if (type === "Nothing") {
    return { kind: "void" };
  }
  return { kind: "unknown", reason: "mendix-empty" };
}

