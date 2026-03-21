import type { NodeType, FlowNode } from "@/types/workflow";
import { nanoid } from "nanoid";

export interface PaletteItem {
  type: NodeType;
}

export const paletteItems: PaletteItem[] = [
  { type: "start" },
  { type: "approve" },
  { type: "condition" },
  { type: "parallel" },
  { type: "parallel-join" },
  { type: "copy" },
  { type: "task" },
  { type: "end" }
];

export function createNode(type: NodeType, name?: string): FlowNode {
  return {
    id: nanoid(),
    type,
    name: name || defaultName(type),
    children: [],
    ext: {}
  };
}

function defaultName(type: NodeType): string {
  switch (type) {
    case "start":
      return "Start";
    case "end":
      return "End";
    case "approve":
      return "Approve";
    case "condition":
      return "Condition";
    case "parallel":
      return "Parallel";
    case "parallel-join":
      return "Join";
    case "copy":
      return "CC";
    case "task":
      return "Task";
    default:
      return type;
  }
}
