import { describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: "button",
  Space: "div",
  Tabs: { TabPane: "div" },
  Tooltip: "div",
  Typography: {
    Text: "span",
    Title: "h6",
  },
}));

vi.mock("@douyinfe/semi-icons", () => ({
  IconClose: "span",
  IconCopy: "span",
  IconDelete: "span",
}));

import type { MicroflowObject } from "../schema";
import { getObjectTabs } from "./panel-shared";

function eventObject(kind: "startEvent" | "endEvent"): MicroflowObject {
  if (kind === "startEvent") {
    return {
      id: "start-1",
      stableId: "start-1",
      kind: "startEvent",
      officialType: "Microflows$StartEvent",
      caption: "Start",
      documentation: "",
      editor: {
        position: { x: 0, y: 0 },
        size: { width: 124, height: 70 },
      },
    } as unknown as MicroflowObject;
  }
  return {
    id: "end-1",
    stableId: "end-1",
    kind: "endEvent",
    officialType: "Microflows$EndEvent",
    caption: "End",
    documentation: "",
    editor: {
      position: { x: 0, y: 0 },
      size: { width: 124, height: 70 },
    },
  } as unknown as MicroflowObject;
}

function actionObject(actionKind: string): MicroflowObject {
  return {
    id: `action-${actionKind}`,
    stableId: `action-${actionKind}`,
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: actionKind,
    documentation: "",
    editor: {
      position: { x: 0, y: 0 },
      size: { width: 178, height: 76 },
    },
    action: {
      id: `action-data-${actionKind}`,
      stableId: `action-data-${actionKind}`,
      kind: actionKind,
      officialType: "Microflows$Action",
    },
  } as unknown as MicroflowObject;
}

describe("getObjectTabs", () => {
  it("keeps Start Event as single-tab Parameters surface", () => {
    expect(getObjectTabs(eventObject("startEvent"))).toEqual(["properties"]);
  });

  it("maps Create Object to 3 tabs", () => {
    expect(getObjectTabs(actionObject("createObject"))).toEqual(["properties", "output", "documentation"]);
  });

  it("maps Call REST to 5 tabs", () => {
    expect(getObjectTabs(actionObject("restCall"))).toEqual(["properties", "advanced", "output", "errorHandling", "documentation"]);
  });

  it("maps Retrieve and Change Variable to 2 tabs", () => {
    expect(getObjectTabs(actionObject("retrieve"))).toEqual(["properties", "documentation"]);
    expect(getObjectTabs(actionObject("changeVariable"))).toEqual(["properties", "documentation"]);
  });
});
