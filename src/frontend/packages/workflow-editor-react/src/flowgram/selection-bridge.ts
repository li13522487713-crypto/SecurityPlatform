import { createContext, useContext } from "react";

interface FlowgramSelectionBridge {
  selectNode: (nodeKey: string) => void;
  reportPortClick: (params: { nodeKey: string; portKey: string; portType: "input" | "output" }) => void;
}

const noopBridge: FlowgramSelectionBridge = {
  selectNode: () => {},
  reportPortClick: () => {}
};

export const FlowgramSelectionBridgeContext = createContext<FlowgramSelectionBridge>(noopBridge);

export function useFlowgramSelectionBridge(): FlowgramSelectionBridge {
  return useContext(FlowgramSelectionBridgeContext);
}
