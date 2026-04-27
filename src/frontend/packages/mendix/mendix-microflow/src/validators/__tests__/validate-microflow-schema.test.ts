import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, createSequenceFlow } from "../../adapters";
import { getDefaultMockMetadataCatalog } from "../../metadata";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import { createBooleanCaseValue, sampleMicroflowSchema, validateMicroflowSchema, type MicroflowObject, type MicroflowSchema } from "../../schema";

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

function actionObject(key: string, id: string) {
  const object = objectFrom(key, id);
  if (object.kind !== "actionActivity") {
    throw new Error(`Expected action object for ${key}.`);
  }
  return object;
}

function schemaWith(objects: MicroflowObject[], flows: MicroflowSchema["flows"] = []): MicroflowSchema {
  return {
    ...sampleMicroflowSchema,
    id: "MF_VALIDATION_TEST",
    stableId: "MF_VALIDATION_TEST",
    parameters: [],
    objectCollection: { ...sampleMicroflowSchema.objectCollection, id: "validation-root", objects },
    flows,
    editor: { ...sampleMicroflowSchema.editor, selection: {} },
  };
}

function validSchema(): MicroflowSchema {
  const start = objectFrom("startEvent", "start", 0, 0);
  const end = objectFrom("endEvent", "end", 240, 0);
  return schemaWith([start, end], [createSequenceFlow({ originObjectId: start.id, destinationObjectId: end.id })]);
}

function validate(schema: MicroflowSchema) {
  return validateMicroflowSchema({ schema, metadata: getDefaultMockMetadataCatalog(), options: { mode: "save", includeWarnings: true, includeInfo: true } }).issues;
}

describe("validateMicroflowSchema Stage 20 save gate rules", () => {
  it("reports missing Start as an error", () => {
    const schema = validSchema();
    const issues = validate({ ...schema, objectCollection: { ...schema.objectCollection, objects: schema.objectCollection.objects.filter(object => object.kind !== "startEvent") } });

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_START_MISSING", severity: "error" })]));
  });

  it("reports duplicate object id as an error", () => {
    const schema = validSchema();
    const duplicated = { ...schema.objectCollection.objects[0], caption: "Duplicated Start" } as MicroflowObject;
    const issues = validate({ ...schema, objectCollection: { ...schema.objectCollection, objects: [...schema.objectCollection.objects, duplicated] } });

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_OBJECT_ID_DUPLICATED", severity: "error" })]));
  });

  it("reports dangling flow as an error", () => {
    const schema = validSchema();
    const dangling = createSequenceFlow({ originObjectId: "start", destinationObjectId: "missing-target" });
    const issues = validate({ ...schema, flows: [dangling] });

    expect(issues.some(issue => issue.code === "MF_FLOW_INVALID_TARGET" || issue.code === "MF_FLOW_DESTINATION_MISSING")).toBe(true);
  });

  it("reports duplicate parameter names as an error", () => {
    const schema = {
      ...validSchema(),
      parameters: [
        { id: "param-a", name: "amount", dataType: { kind: "string" as const }, required: true },
        { id: "param-b", name: "Amount", dataType: { kind: "string" as const }, required: true },
      ],
    };
    const issues = validate(schema);

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_PARAMETER_DUPLICATED", severity: "error" })]));
  });

  it("reports Change Variable missing target as an error", () => {
    const schema = schemaWith([objectFrom("startEvent", "start"), actionObject("activity:variableChange", "change-variable"), objectFrom("endEvent", "end")]);
    const issues = validate(schema);

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_CHANGE_VARIABLE_TARGET_MISSING", severity: "error" })]));
  });

  it("reports duplicate true Decision branch as an error and missing false as a warning", () => {
    const start = objectFrom("startEvent", "start");
    const decision = objectFrom("decision", "decision");
    const end = objectFrom("endEvent", "end");
    const first = createSequenceFlow({ originObjectId: decision.id, destinationObjectId: end.id, caseValues: [createBooleanCaseValue(true)], edgeKind: "decisionCondition" });
    const second = createSequenceFlow({ originObjectId: decision.id, destinationObjectId: end.id, caseValues: [createBooleanCaseValue(true)], edgeKind: "decisionCondition", originConnectionIndex: 1 });
    const issues = validate(schemaWith([start, decision, end], [
      createSequenceFlow({ originObjectId: start.id, destinationObjectId: decision.id }),
      first,
      second,
    ]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_DECISION_DUPLICATE_CASE", severity: "error" })]));
    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_DECISION_BOOLEAN_FALSE_MISSING", severity: "warning" })]));
  });

  it("reports Call Microflow missing and stale targets", () => {
    const call = actionObject("activity:callMicroflow", "call-microflow");
    const missingIssues = validate(schemaWith([objectFrom("startEvent", "start"), call, objectFrom("endEvent", "end")]));
    const staleCall = { ...call, action: { ...call.action, targetMicroflowId: "missing-mf" } };
    const staleIssues = validate(schemaWith([objectFrom("startEvent", "start"), staleCall, objectFrom("endEvent", "end")]));

    expect(missingIssues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_CALL_MICROFLOW_TARGET_MISSING" })]));
    expect(staleIssues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_METADATA_MICROFLOW_NOT_FOUND" })]));
  });

  it("reports Break outside Loop as an error", () => {
    const issues = validate(schemaWith([objectFrom("startEvent", "start"), objectFrom("breakEvent", "break"), objectFrom("endEvent", "end")]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_BREAK_OUTSIDE_LOOP", severity: "error" })]));
  });

  it("reports List Operation missing source as an error", () => {
    const issues = validate(schemaWith([objectFrom("startEvent", "start"), actionObject("activity:listOperation", "list-operation"), objectFrom("endEvent", "end")]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_LIST_OPERATION_SOURCE_MISSING", severity: "error" })]));
  });

  it("reports stale Object Activity entity as a domain model issue", () => {
    const createObject = actionObject("activity:objectCreate", "create-object");
    const stale = { ...createObject, action: { ...createObject.action, entityQualifiedName: "Missing.Entity", outputVariableName: "missingEntity" } };
    const issues = validate(schemaWith([objectFrom("startEvent", "start"), stale, objectFrom("endEvent", "end")]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_METADATA_ENTITY_NOT_FOUND", source: "domainModel" })]));
  });

  it("keeps A/B schema validation isolated and does not mutate input", () => {
    const schemaA = { ...validSchema(), id: "MF_A", stableId: "MF_A" };
    const schemaB = { ...validSchema(), id: "MF_B", stableId: "MF_B", objectCollection: { ...validSchema().objectCollection, objects: [] } };
    const before = JSON.stringify(schemaA);
    const issuesA = validate(schemaA);
    const issuesB = validate(schemaB);

    expect(issuesA.some(issue => issue.code === "MF_START_MISSING")).toBe(false);
    expect(issuesB.some(issue => issue.code === "MF_START_MISSING")).toBe(true);
    expect(JSON.stringify(schemaA)).toBe(before);
  });
});
