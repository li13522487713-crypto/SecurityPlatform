import { createContext } from "react";

import type { MicroflowSchema } from "../schema";

export interface FlowGramMicroflowRuntimeContextValue {
  schema: MicroflowSchema;
}

export const FlowGramMicroflowRuntimeContext = createContext<FlowGramMicroflowRuntimeContextValue | null>(null);

