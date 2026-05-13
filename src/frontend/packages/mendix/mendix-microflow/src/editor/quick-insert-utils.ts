import type { MicroflowEditorGraph } from "../schema/types";
import type { MicroflowNodeRegistryItem } from "../node-registry";

export type QuickInsertGroupKey = "object" | "list" | "variable" | "flow" | "call" | "events";

export interface QuickInsertGroupMeta {
  key: QuickInsertGroupKey;
  label: string;
  color: string;
  background: string;
}

export interface QuickInsertIncomingEdgeChoice {
  flowId: string;
  sourceObjectId: string;
  sourceTitle: string;
  edgeKind: string;
}

export const quickInsertGroupOrder: QuickInsertGroupMeta[] = [
  { key: "object", label: "Object", color: "#165dff", background: "#eef4ff" },
  { key: "list", label: "List", color: "#d48806", background: "#fff7e8" },
  { key: "variable", label: "Variable", color: "#13a8a8", background: "#e6fffb" },
  { key: "flow", label: "Flow", color: "#ff8800", background: "#fff7e8" },
  { key: "call", label: "Call", color: "#722ed1", background: "#f2edff" },
  { key: "events", label: "Events", color: "#12b886", background: "#e8f8ef" },
];

export function quickInsertGroupKeyFromItem(item: MicroflowNodeRegistryItem): QuickInsertGroupKey {
  if (item.group === "Events") {
    return "events";
  }
  if (item.subgroup === "object") {
    return "object";
  }
  if (item.subgroup === "list") {
    return "list";
  }
  if (item.subgroup === "variable") {
    return "variable";
  }
  if (item.subgroup === "call" || item.subgroup === "integration") {
    return "call";
  }
  return "flow";
}

export function resolveIncomingQuickInsertChoices(
  graph: Pick<MicroflowEditorGraph, "nodes" | "edges">,
  targetObjectId: string,
): QuickInsertIncomingEdgeChoice[] {
  const nodeByObjectId = new Map(
    graph.nodes.map(node => [String(node.objectId), node]),
  );
  return graph.edges
    .filter(edge => {
      if (!edge.flowId || edge.edgeKind === "annotation" || edge.edgeKind === "errorHandler") {
        return false;
      }
      const normalizedTarget = String(edge.targetNodeId ?? "").replace(/^node-/, "");
      return normalizedTarget === targetObjectId;
    })
    .map(edge => {
      const sourceObjectId = String(edge.sourceNodeId ?? "").replace(/^node-/, "");
      const sourceTitle = nodeByObjectId.get(sourceObjectId)?.title ?? sourceObjectId;
      return {
        flowId: edge.flowId,
        sourceObjectId,
        sourceTitle,
        edgeKind: edge.edgeKind,
      };
    });
}
