import { describe, expect, it } from "vitest";

import { createObjectFromRegistry } from "../adapters";
import { sampleMicroflowSchema } from "../__fixtures__/sample-microflow";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../node-registry";
import type { MicroflowObject, MicroflowSchema } from "../schema/types";
import { collectMicroflowBestPracticeWarnings, summarizeMicroflowComplexity } from "./microflow-validator";

function registry(key: string) {
  const item = defaultMicroflowNodeRegistry.find(entry => getMicroflowNodeRegistryKey(entry) === key || entry.type === key);
  if (!item) {
    throw new Error(`Missing registry item ${key}`);
  }
  return item;
}

function objectFrom(key: string, id: string, x = 0, y = 0): MicroflowObject {
  return createObjectFromRegistry(registry(key), { x, y }, id);
}

function schemaWith(objects: MicroflowObject[]): MicroflowSchema {
  return {
    ...sampleMicroflowSchema,
    id: "MF_COMPLEXITY_TEST",
    stableId: "MF_COMPLEXITY_TEST",
    objectCollection: { ...sampleMicroflowSchema.objectCollection, id: "complexity-root", objects },
    flows: [],
    parameters: [],
    editor: { ...sampleMicroflowSchema.editor, selection: {} },
  };
}

describe("summarizeMicroflowComplexity", () => {
  it("counts elements excluding Start/End and flags annotation recommendation", () => {
    const start = objectFrom("startEvent", "start");
    const end = objectFrom("endEvent", "end");
    const activities = Array.from({ length: 11 }, (_, index) => objectFrom("activity:logMessage", `log-${index + 1}`));
    const summary = summarizeMicroflowComplexity(schemaWith([start, ...activities, end]));

    expect(summary.totalElements).toBe(11);
    expect(summary.activityCount).toBe(11);
    expect(summary.decisionCount).toBe(0);
    expect(summary.annotationRecommended).toBe(true);
    expect(summary.hasAnnotation).toBe(false);
    expect(summary.level).toBe("ok");
  });

  it("switches to warning/error level near the recommended threshold", () => {
    const start = objectFrom("startEvent", "start");
    const end = objectFrom("endEvent", "end");
    const warningSummary = summarizeMicroflowComplexity(schemaWith([
      start,
      ...Array.from({ length: 20 }, (_, index) => objectFrom("activity:logMessage", `warn-${index + 1}`)),
      end,
    ]));
    const errorSummary = summarizeMicroflowComplexity(schemaWith([
      start,
      ...Array.from({ length: 25 }, (_, index) => objectFrom("activity:logMessage", `error-${index + 1}`)),
      end,
    ]));

    expect(warningSummary.level).toBe("warning");
    expect(errorSummary.level).toBe("error");
    expect(errorSummary.recommendedMaxNodes).toBe(25);
  });

  it("detects loop commit and missing error handler best-practice warnings", () => {
    const createList = objectFrom("activity:listCreate", "create-list");
    const callJava = objectFrom("activity:callJavaAction", "call-java");
    const loop = objectFrom("loop", "loop");
    const commit = objectFrom("activity:objectCommit", "commit-in-loop");
    if (createList.kind !== "actionActivity" || callJava.kind !== "actionActivity" || loop.kind !== "loopedActivity" || commit.kind !== "actionActivity") {
      throw new Error("Expected typed authoring objects.");
    }
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      entityQualifiedName: "Sales.Order",
    } as typeof createList.action;
    callJava.action = {
      ...callJava.action,
      javaActionQualifiedName: "Sales.DoWork",
      errorHandlingType: "rollback",
    } as typeof callJava.action;
    loop.loopSource = {
      kind: "iterableList",
      officialType: "Microflows$IterableList",
      listVariableName: "orders",
      iteratorVariableName: "orderItem",
      currentIndexVariableName: "$currentIndex",
      iteratorVariableDataType: { kind: "object", entityQualifiedName: "Sales.Order" },
    };
    commit.action = {
      ...commit.action,
      objectOrListVariableName: "orderItem",
    } as typeof commit.action;
    loop.objectCollection = { ...loop.objectCollection, objects: [commit], flows: [] };

    const warnings = collectMicroflowBestPracticeWarnings(schemaWith([createList, callJava, loop]));

    expect(warnings.map(item => item.code)).toEqual(expect.arrayContaining(["LOOP_COMMIT", "MISSING_ERROR_HANDLER"]));
  });

  it("detects nested if expressions in Decision nodes", () => {
    const decision = objectFrom("decision", "decision");
    if (decision.kind !== "exclusiveSplit" || decision.splitCondition.kind !== "expression") {
      throw new Error("Expected expression decision object.");
    }
    decision.splitCondition = {
      ...decision.splitCondition,
      expression: { ...decision.splitCondition.expression, raw: "if $flag then (if $retry then true else false) else false" },
    };

    const warnings = collectMicroflowBestPracticeWarnings(schemaWith([decision]));

    expect(warnings).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "NESTED_IF_EXPRESSION",
      severity: "info",
      objectId: "decision",
    })]));
  });
});
