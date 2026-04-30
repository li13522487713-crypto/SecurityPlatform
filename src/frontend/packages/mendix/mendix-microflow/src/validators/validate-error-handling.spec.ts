import { describe, expect, it } from "vitest";
import { validateErrorHandling } from "./validate-error-handling";
import { validateLoop } from "./validate-loop";
import type { MicroflowSchema } from "../schema/types";

function schemaWithObjects(objects: any[], flows: any[] = []): MicroflowSchema {
  return {
    schemaVersion: "1.0.0",
    id: "mf",
    name: "MF",
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: { id: "root", objects },
    flows,
    variables: {},
    editor: { selection: {}, viewport: { x: 0, y: 0, zoom: 1 } },
  } as unknown as MicroflowSchema;
}

describe("validateErrorHandling", () => {
  it("allows continue for restCall", () => {
    const schema = schemaWithObjects([
      {
        id: "rest",
        kind: "actionActivity",
        action: {
          id: "rest-action",
          kind: "restCall",
          errorHandlingType: "continue",
        },
      },
    ]);

    const issues = validateErrorHandling(schema, {} as never);

    expect(issues.some(issue => issue.code === "MF_ERROR_HANDLER_CONTINUE_NOT_ALLOWED")).toBe(false);
  });

  it("rejects continue for unsupported actions", () => {
    const schema = schemaWithObjects([
      {
        id: "thrower",
        kind: "actionActivity",
        action: {
          id: "throw-action",
          kind: "throwException",
          errorHandlingType: "continue",
        },
      },
    ]);

    const issues = validateErrorHandling(schema, {} as never);

    expect(issues.some(issue => issue.code === "MF_ERROR_HANDLER_CONTINUE_NOT_ALLOWED")).toBe(true);
  });

  it("does not require error handler flow for loop continue mode", () => {
    const schema = schemaWithObjects([
      {
        id: "loop",
        kind: "loopedActivity",
        errorHandlingType: "continue",
        objectCollection: { id: "loop-body", objects: [] },
        loopSource: {
          kind: "iterableList",
          listVariableName: "items",
          iteratorVariableName: "item",
        },
      },
    ]);

    const issues = validateErrorHandling(schema, {} as never);

    expect(issues.some(issue => issue.code === "MF_ERROR_HANDLER_WITH_ROLLBACK_MISSING_FLOW")).toBe(false);
  });
});

describe("validateLoop", () => {
  it("does not report ambiguous target when nested loops omit targetLoopObjectId", () => {
    const schema = schemaWithObjects([
      {
        id: "outer-loop",
        kind: "loopedActivity",
        objectCollection: {
          id: "outer-body",
          objects: [
            {
              id: "inner-loop",
              kind: "loopedActivity",
              objectCollection: {
                id: "inner-body",
                objects: [
                  {
                    id: "continue",
                    kind: "continueEvent",
                  },
                ],
              },
              loopSource: {
                kind: "iterableList",
                listVariableName: "innerItems",
                iteratorVariableName: "innerItem",
              },
            },
          ],
        },
        loopSource: {
          kind: "iterableList",
          listVariableName: "outerItems",
          iteratorVariableName: "outerItem",
        },
      },
    ]);

    const issues = validateLoop(schema, {} as never);

    expect(issues.some(issue => issue.code === "MF_LOOP_CONTROL_TARGET_AMBIGUOUS")).toBe(false);
  });
});
