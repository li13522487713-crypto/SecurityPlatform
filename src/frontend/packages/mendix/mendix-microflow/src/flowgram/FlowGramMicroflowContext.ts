import { createContext } from "react";

import type { MicroflowTraceFrame } from "../debug/trace-types";
import type { MicroflowSchema, MicroflowVariableIndex, MicroflowVariableSymbol } from "../schema";
import type { MicroflowVariableQueryOptions } from "../variables/variable-scope-engine";

export interface FlowGramMicroflowRuntimeContextValue {
  schema: MicroflowSchema;
  variableIndex: MicroflowVariableIndex;
  getVariablesForNode: (objectId: string, options?: MicroflowVariableQueryOptions) => MicroflowVariableSymbol[];
  runtimeTraceByObjectId: Map<string, MicroflowTraceFrame>;
  expandedObjectId: string | null;
  onExpandChange: (objectId: string | null) => void;
  onSchemaChange: (next: MicroflowSchema, reason: string) => void;
  readonly: boolean;
  registerDraftValidator: (fn: (() => { valid: boolean; summary: string }) | null) => void;
}

export const FlowGramMicroflowRuntimeContext = createContext<FlowGramMicroflowRuntimeContextValue | null>(null);
