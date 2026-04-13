import { describe, expect, it } from "vitest";
import { buildNodePanelGroups } from "./NodePanelPopover";
import { WORKFLOW_NODE_CATALOG } from "../constants/node-catalog";

describe("buildNodePanelGroups", () => {
  it("keeps Coze-style category order for available nodes", () => {
    const groups = buildNodePanelGroups({
      keyword: "",
      nodes: WORKFLOW_NODE_CATALOG,
      enabledTypes: ["Llm", "Selector", "InputReceiver", "DatabaseQuery"],
      translate: (key) => key
    });

    expect(groups.map((group) => group.category)).toEqual(["featured", "logic", "io", "database"]);
  });

  it("filters out nodes not enabled by backend metadata", () => {
    const groups = buildNodePanelGroups({
      keyword: "",
      nodes: WORKFLOW_NODE_CATALOG,
      enabledTypes: ["Llm", "Plugin"],
      translate: (key) => key
    });

    expect(groups).toHaveLength(1);
    expect(groups[0]?.items.map((item) => item.type)).toEqual(["Llm", "Plugin"]);
  });

  it("supports keyword matching against translated titles", () => {
    const groups = buildNodePanelGroups({
      keyword: "知识库",
      nodes: WORKFLOW_NODE_CATALOG,
      translate: (key) => {
        if (key === "wfUi.nodeTypes.KnowledgeRetriever") {
          return "知识库检索";
        }
        if (key === "wfUi.nodeTypes.KnowledgeIndexer") {
          return "知识库写入";
        }
        return key;
      }
    });

    expect(groups.map((group) => group.category)).toEqual(["knowledge"]);
    expect(groups[0]?.items.map((item) => item.type)).toEqual(["KnowledgeRetriever", "KnowledgeIndexer"]);
  });
});
