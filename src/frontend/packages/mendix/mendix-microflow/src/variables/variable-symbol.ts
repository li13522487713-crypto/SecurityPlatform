import type { MicroflowVariableSymbol } from "../schema/types";
import type { MicroflowVariableAvailability } from "./variable-source";

export interface MicroflowScopedVariableSymbol extends MicroflowVariableSymbol {
  availability: MicroflowVariableAvailability;
}

export function variableDisplayName(symbol: Pick<MicroflowVariableSymbol, "name">): string {
  return symbol.name.startsWith("$") ? symbol.name : `$${symbol.name}`;
}
