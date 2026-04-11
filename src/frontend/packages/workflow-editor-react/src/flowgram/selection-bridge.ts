import { createContext, useContext } from "react";

interface FlowgramSelectionBridge {
  reportNodeSelection: (nodeKey: string, selected: boolean) => void;
}

const noopBridge: FlowgramSelectionBridge = {
  reportNodeSelection: () => {}
};

export const FlowgramSelectionBridgeContext = createContext<FlowgramSelectionBridge>(noopBridge);

export function useFlowgramSelectionBridge(): FlowgramSelectionBridge {
  return useContext(FlowgramSelectionBridgeContext);
}
