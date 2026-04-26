import { createContext, useContext } from "react";

export interface MicroflowStudioContextValue {
  workspaceId?: string;
  currentUser?: { id: string; name: string };
  readonly?: boolean;
}

export const MicroflowStudioContext = createContext<MicroflowStudioContextValue>({});

export function useMicroflowStudioContext() {
  return useContext(MicroflowStudioContext);
}
