import { describe, expect, it, vi } from "vitest";
import { getObjectTabLabels, getObjectTabs } from "./panel-shared";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: () => null,
  Space: ({ children }: any) => children ?? null,
  Tabs: {
    TabPane: () => null,
  },
  Tooltip: ({ children }: any) => children ?? null,
  Typography: {
    Text: ({ children }: any) => children ?? null,
    Title: ({ children }: any) => children ?? null,
  },
}));

vi.mock("@douyinfe/semi-icons", () => ({
  IconClose: () => null,
  IconCopy: () => null,
  IconDelete: () => null,
}));

function objectNode(kind: string): any {
  return {
    id: `${kind}-1`,
    kind,
    officialType: "Microflows$Object",
    caption: kind,
    documentation: "",
    editor: {},
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 120, height: 80 },
    ports: [],
  };
}

function actionNode(actionKind: string): any {
  return {
    ...objectNode("actionActivity"),
    officialType: "Microflows$ActionActivity",
    action: {
      id: `${actionKind}-action-1`,
      kind: actionKind,
      officialType: `Microflows$${actionKind}`,
      caption: actionKind,
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "object", iconKey: actionKind, availability: "supported" },
    },
  };
}

describe("panel shared tab mapping", () => {
  it("maps simple object nodes to compact tabs", () => {
    expect(getObjectTabs(objectNode("startEvent"))).toEqual(["properties"]);
    expect(getObjectTabs(objectNode("annotation"))).toEqual(["properties"]);
    expect(getObjectTabs(objectNode("exclusiveMerge"))).toEqual(["documentation"]);
  });

  it("maps action complexity to tab counts", () => {
    expect(getObjectTabs(actionNode("createObject"))).toEqual(["properties", "output", "documentation"]);
    expect(getObjectTabs(actionNode("callMicroflow"))).toEqual(["properties", "output", "documentation"]);
    expect(getObjectTabs(actionNode("restCall"))).toEqual(["properties", "advanced", "output", "errorHandling", "documentation"]);
  });

  it("uses mendix-style labels for key tabs", () => {
    expect(getObjectTabLabels(objectNode("startEvent"))).toEqual({ properties: "Parameters" });
    expect(getObjectTabLabels(objectNode("exclusiveSplit"))).toEqual({ properties: "Cases", documentation: "Documentation" });
    expect(getObjectTabLabels(actionNode("createObject"))).toEqual({
      properties: "Configuration",
      output: "Input / Output",
      documentation: "Documentation",
    });
    expect(getObjectTabLabels(actionNode("restCall"))).toEqual({
      properties: "General",
      advanced: "Request",
      output: "Response",
      errorHandling: "Authentication",
      documentation: "Documentation",
    });
  });
});
