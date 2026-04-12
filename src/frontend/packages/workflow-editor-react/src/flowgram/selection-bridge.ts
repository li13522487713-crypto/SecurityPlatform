import { createContext, useContext } from "react";

interface FlowgramSelectionBridge {
  reportNodeSelection: (nodeKey: string, selected: boolean) => void;
  reportPortClick: (params: { nodeKey: string; portKey: string; portType: "input" | "output" }) => void;
}

const noopBridge: FlowgramSelectionBridge = {
  reportNodeSelection: () => {},
  reportPortClick: () => {}
};

export const FlowgramSelectionBridgeContext = createContext<FlowgramSelectionBridge>(noopBridge);

export function useFlowgramSelectionBridge(): FlowgramSelectionBridge {
  return useContext(FlowgramSelectionBridgeContext);
}
