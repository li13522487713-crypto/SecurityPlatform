import { useMemo } from "react";

import type { MicroflowVariableSymbol } from "../../schema";
import type { MicroflowVariableQueryOptions } from "../../variables/variable-scope-engine";
import { useFlowGramMicroflowContext } from "./useFlowGramMicroflowContext";

export function useNodeVariableScope(
  objectId: string,
  options?: MicroflowVariableQueryOptions,
): MicroflowVariableSymbol[] {
  const { getVariablesForNode } = useFlowGramMicroflowContext();
  // stringify options as memoization key since options are passed as literals
  // eslint-disable-next-line react-hooks/exhaustive-deps
  return useMemo(() => getVariablesForNode(objectId, options), [getVariablesForNode, objectId, JSON.stringify(options)]);
}
