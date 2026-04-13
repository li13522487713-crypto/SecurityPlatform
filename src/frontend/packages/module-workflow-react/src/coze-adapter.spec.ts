import { describe, expect, it } from "vitest";
import { buildReferenceSidebarSections, buildResourceSidebarSections, filterWorkflowItems } from "./coze-adapter";
import { getWorkflowModuleCopy } from "./copy";

describe("module-workflow-react coze adapter", () => {
  const copy = getWorkflowModuleCopy("zh-CN");

  it("filters workflow items by mode and keyword", () => {
    const result = filterWorkflowItems(
      [
        { id: "w1", name: "审批流", mode: 0 },
        { id: "w2", name: "Support Chat", mode: 1 },
        { id: "w3", name: "知识检索", mode: 0, description: "知识库" }
      ],
      "workflow",
      "知识"
    );

    expect(result.map(item => item.id)).toEqual(["w3"]);
  });

  it("builds resource sidebar sections with current workflow badge", () => {
    const sections = buildResourceSidebarSections({
      copy,
      mode: "workflow",
      currentWorkflowId: "w1",
      workflowItems: [
        { id: "w1", name: "当前流程", mode: 0 },
        { id: "w2", name: "其他流程", mode: 0 }
      ],
      pluginItems: [],
      knowledgeItems: [],
      databaseItems: [],
      conversations: [],
      keyword: ""
    });

    expect(sections[0]?.items[0]).toMatchObject({
      key: "w1",
      badge: copy.currentWorkflowLabel,
      active: true
    });
    expect(sections[3]?.items.map(item => item.key)).toEqual(["conversations", "variables"]);
  });

  it("builds reference sections from nodes and versions", () => {
    const sections = buildReferenceSidebarSections({
      copy,
      detail: null,
      versions: [
        {
          id: "v1",
          workflowId: "w1",
          versionNumber: 3,
          canvasJson: "{}",
          publishedAt: "2026-04-13T08:21:27Z",
          publishedByUserId: "u1"
        }
      ],
      nodes: [
        {
          key: "entry_1",
          type: "Entry",
          title: "开始",
          layout: { x: 0, y: 0, width: 0, height: 0 },
          configs: {},
          inputMappings: {}
        }
      ],
      nodeTypes: [{ key: "Entry", name: "开始", category: "flow", description: "入口" }]
    });

    expect(sections[0]?.items[0]?.badge).toBe("开始");
    expect(sections[1]?.items[0]?.name).toBe("v3");
  });
});
