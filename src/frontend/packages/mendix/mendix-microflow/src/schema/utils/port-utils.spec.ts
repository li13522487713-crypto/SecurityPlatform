import { describe, expect, it } from "vitest";
import type { MicroflowExclusiveSplit } from "../types";
import { portsForObject } from "./port-utils";

describe("portsForObject", () => {
  it("treats rule decisions as boolean decisions", () => {
    const decision = {
      id: "decision",
      stableId: "decision",
      kind: "exclusiveSplit",
      officialType: "Microflows$ExclusiveSplit",
      caption: "Decision",
      documentation: "",
      relativeMiddlePoint: { x: 0, y: 0 },
      size: { width: 44, height: 44 },
      editor: {},
      splitCondition: {
        kind: "rule",
        ruleQualifiedName: "Sales.Rule1",
        parameterMappings: [],
        resultType: "boolean",
      },
      errorHandlingType: "rollback",
    } as MicroflowExclusiveSplit;

    expect(portsForObject(decision).map(port => port.id)).toEqual(["in", "true", "false", "error"]);
  });
});
