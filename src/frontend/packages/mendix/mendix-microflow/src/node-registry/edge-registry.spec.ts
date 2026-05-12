import { describe, expect, it } from "vitest";
import {
  canConnectPortsV2,
  defaultMicroflowEdgeRegistry,
  edgeStyleByKind,
} from "./edge-registry";

function node(id: string, kind: string): any {
  return {
    id,
    kind,
    caption: id,
    officialType: "Microflows$Object",
    editor: {},
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 120, height: 80 },
    ports: [],
  };
}

function sequenceFlow(input: {
  id: string;
  originObjectId: string;
  destinationObjectId: string;
  edgeKind?: string;
  isErrorHandler?: boolean;
}): any {
  return {
    id: input.id,
    stableId: input.id,
    kind: "sequence",
    officialType: "Microflows$SequenceFlow",
    originObjectId: input.originObjectId,
    destinationObjectId: input.destinationObjectId,
    originConnectionIndex: 0,
    destinationConnectionIndex: 0,
    caseValues: [],
    isErrorHandler: Boolean(input.isErrorHandler),
    line: { points: [] },
    editor: { edgeKind: input.edgeKind ?? "sequence" },
  };
}

function schema(objects: any[], flows: any[] = []): any {
  return {
    objectCollection: {
      id: "root",
      officialType: "Microflows$MicroflowObjectCollection",
      objects,
      flows: [],
    },
    flows,
  };
}

describe("edge registry contract", () => {
  it("uses expected colors and line styles for 5 edge kinds", () => {
    const byKey = new Map(defaultMicroflowEdgeRegistry.map(item => [item.key, item]));
    expect(byKey.get("sequence")?.colorToken).toBe("#4e5969");
    expect(byKey.get("decisionCondition")?.colorToken).toBe("#165dff");
    expect(byKey.get("objectTypeCondition")?.colorToken).toBe("#722ed1");
    expect(byKey.get("errorHandler")?.colorToken).toBe("#f93920");
    expect(byKey.get("annotation")?.colorToken).toBe("#86909c");
    expect(byKey.get("errorHandler")?.lineStyle).toBe("dashed");
  });

  it("maps runtime edge style tokens correctly", () => {
    expect(edgeStyleByKind("sequence")).toEqual({ strokeType: "solid", colorToken: "#4e5969", arrow: true });
    expect(edgeStyleByKind("decisionCondition")).toEqual({ strokeType: "solid", colorToken: "#165dff", arrow: true });
    expect(edgeStyleByKind("objectTypeCondition")).toEqual({ strokeType: "solid", colorToken: "#722ed1", arrow: true });
    expect(edgeStyleByKind("errorHandler")).toEqual({ strokeType: "dashed", colorToken: "#f93920", arrow: true });
    expect(edgeStyleByKind("annotation")).toEqual({ strokeType: "dashed", colorToken: "#86909c", arrow: false });
  });

  it("rejects self connections", () => {
    const result = canConnectPortsV2({
      schema: schema([node("n1", "actionActivity")]),
      sourceObjectId: "n1",
      sourcePortId: "out",
      sourcePortKind: "sequenceOut",
      sourceConnectionIndex: 0,
      targetObjectId: "n1",
      targetPortId: "in",
      targetPortKind: "sequenceIn",
      targetConnectionIndex: 0,
      mode: "create",
    });
    expect(result.allowed).toBe(false);
    expect(result.reasonCode).toBe("MF_CONNECT_SELF_LOOP");
  });

  it("rejects incoming flows to StartEvent", () => {
    const result = canConnectPortsV2({
      schema: schema([node("source", "actionActivity"), node("start", "startEvent")]),
      sourceObjectId: "source",
      sourcePortId: "out",
      sourcePortKind: "sequenceOut",
      sourceConnectionIndex: 0,
      targetObjectId: "start",
      targetPortId: "in",
      targetPortKind: "sequenceIn",
      targetConnectionIndex: 0,
      mode: "create",
    });
    expect(result.allowed).toBe(false);
    expect(result.reasonCode).toBe("MF_CONNECT_START_TARGET");
  });

  it("allows only one error handler flow per source", () => {
    const objects = [node("source", "actionActivity"), node("catch-1", "errorEvent"), node("catch-2", "errorEvent")];
    const flows = [
      sequenceFlow({
        id: "error-flow-1",
        originObjectId: "source",
        destinationObjectId: "catch-1",
        edgeKind: "errorHandler",
        isErrorHandler: true,
      }),
    ];
    const result = canConnectPortsV2({
      schema: schema(objects, flows),
      sourceObjectId: "source",
      sourcePortId: "error",
      sourcePortKind: "errorOut",
      sourceConnectionIndex: 0,
      targetObjectId: "catch-2",
      targetPortId: "in",
      targetPortKind: "sequenceIn",
      targetConnectionIndex: 0,
      mode: "create",
    });
    expect(result.allowed).toBe(false);
    expect(result.reasonCode).toBe("MF_CONNECT_ERROR_DUPLICATED");
  });
});
