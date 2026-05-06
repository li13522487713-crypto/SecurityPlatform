import { useContext } from "react";

import {
  FlowGramMicroflowRuntimeContext,
  type FlowGramMicroflowRuntimeContextValue,
} from "../FlowGramMicroflowContext";

export function useFlowGramMicroflowContext(): FlowGramMicroflowRuntimeContextValue {
  const ctx = useContext(FlowGramMicroflowRuntimeContext);
  if (!ctx) {
    throw new Error(
      "useFlowGramMicroflowContext must be used inside FlowGramMicroflowNativeCanvas",
    );
  }
  return ctx;
}
