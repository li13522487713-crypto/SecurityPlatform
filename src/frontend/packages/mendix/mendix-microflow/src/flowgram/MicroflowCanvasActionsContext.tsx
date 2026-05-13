import { createContext, useContext } from "react";

export interface MicroflowCanvasActions {
  deleteFlow: (flowId: string) => void;
  deleteNode: (objectId: string) => void;
  focusNodeIssue: (nodeId: string) => void;
}

export const MicroflowCanvasActionsContext = createContext<MicroflowCanvasActions | null>(null);

export function useMicroflowCanvasActions(): MicroflowCanvasActions {
  const ctx = useContext(MicroflowCanvasActionsContext);
  if (!ctx) {
    throw new Error("useMicroflowCanvasActions must be used inside MicroflowCanvasActionsContext.Provider");
  }
  return ctx;
}
