import type { MicroflowDataType, MicroflowVariableSymbol } from "../schema/types";
import { dataTypeLabel } from "./variable-type-utils";

export interface MicroflowVariableSelectorOption {
  value: string;
  label: string;
  typeLabel: string;
  sourceLabel: string;
  visibilityLabel?: string;
  symbol: MicroflowVariableSymbol;
}

export function variableSourceLabel(symbol: MicroflowVariableSymbol): string {
  switch (symbol.kind ?? symbol.source.kind) {
    case "parameter":
      return "parameter";
    case "localVariable":
      return "local variable";
    case "objectOutput":
    case "listOutput":
    case "primitiveOutput":
    case "actionOutput":
      return "action output";
    case "loopIterator":
      return "loop iterator";
    case "system":
      return "system";
    case "errorContext":
      return "error context";
    case "restResponse":
      return "REST response";
    case "soapFault":
      return "SOAP fault";
    case "microflowReturn":
      return "microflow return";
    case "modeledOnly":
      return "modeled-only output";
    case "unknown":
      return "unknown output";
    default:
      return symbol.source.kind;
  }
}

export function toVariableSelectorOption(symbol: MicroflowVariableSymbol): MicroflowVariableSelectorOption {
  return {
    value: symbol.name,
    label: symbol.name,
    typeLabel: dataTypeLabel(symbol.dataType),
    sourceLabel: variableSourceLabel(symbol),
    visibilityLabel: symbol.visibility === "maybe" ? "maybe" : undefined,
    symbol,
  };
}

export function filterVariableByType(symbol: MicroflowVariableSymbol, allowedTypeKinds?: MicroflowDataType["kind"][]): boolean {
  return !allowedTypeKinds?.length || allowedTypeKinds.includes(symbol.dataType.kind);
}
