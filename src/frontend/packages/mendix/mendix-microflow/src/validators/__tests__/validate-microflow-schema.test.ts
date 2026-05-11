import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, createSequenceFlow } from "../../adapters";
import { sampleMicroflowSchema } from "../../__fixtures__/sample-microflow";
import { createMetadataCatalog, EMPTY_MICROFLOW_METADATA_CATALOG } from "../../metadata";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import { createBooleanCaseValue, validateMicroflowSchema, type MicroflowDesignSchema, type MicroflowObject, type MicroflowSchema } from "../../schema";

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

function validDesignSchema(): MicroflowDesignSchema {
  return {
    schemaVersion: "flowgram.microflow.v1",
    id: "MF_DESIGN_VALIDATION_TEST",
    moduleId: "module-1",
    name: "DesignValidationTest",
    displayName: "Design Validation Test",
    workflow: {
      nodes: [
        { id: "start", type: "startEvent", data: { objectId: "start", objectKind: "startEvent" }, meta: { position: { x: 0, y: 0 } } },
        { id: "end", type: "endEvent", data: { objectId: "end", objectKind: "endEvent" }, meta: { position: { x: 240, y: 0 } } },
      ],
      edges: [
        { id: "flow-start-end", sourceNodeID: "start", targetNodeID: "end", data: { flowId: "flow-start-end" } },
      ],
    },
    editor: { viewport: { x: 0, y: 0, zoom: 1 }, zoom: 1, selection: {}, gridEnabled: true, showMiniMap: true },
    parameters: [],
    returnType: { kind: "void" },
    variables: [],
    validation: { issues: [] },
    audit: { version: "1", status: "draft" },
  };
}

function validate(schema: MicroflowSchema) {
  const metadata = createMetadataCatalog({
    ...EMPTY_MICROFLOW_METADATA_CATALOG,
    microflows: [
      {
        id: "MF_CHILD_VALID",
        name: "ChildValid",
        qualifiedName: "Sales.ChildValid",
        moduleName: "Sales",
        parameters: [],
        returnType: { kind: "void" },
        status: "published",
      },
    ],
    entities: [
      {
        id: "Sales.Order",
        name: "Order",
        qualifiedName: "Sales.Order",
        moduleName: "Sales",
        attributes: [],
        associations: [],
        specializations: [],
        isPersistable: true,
      },
    ],
  });
  return validateMicroflowSchema({ schema, metadata, options: { mode: "save", includeWarnings: true, includeInfo: true } }).issues;
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
    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "MF_DECISION_BOOLEAN_FALSE_MISSING",
      severity: "warning",
      objectId: "decision",
      fieldPath: "splitCondition",
      relatedFlowIds: expect.arrayContaining([first.id, second.id]),
      quickFixAvailable: true,
      quickFixes: expect.arrayContaining([expect.objectContaining({ kind: "createMissingFlow" })]),
    })]));
  });

  it("allows Inclusive Gateway expression case values on outgoing sequence flows", () => {
    const start = objectFrom("startEvent", "start");
    const gateway = objectFrom("inclusiveGateway", "inclusive");
    const a = actionObject("activity:variableCreate", "branch-a");
    const b = actionObject("activity:variableCreate", "branch-b");
    const flowA = createSequenceFlow({
      originObjectId: gateway.id,
      destinationObjectId: a.id,
      caseValues: [{ kind: "expression", condition: "$hasFive = true", expression: "$hasFive = true" }],
    });
    const flowB = createSequenceFlow({
      originObjectId: gateway.id,
      destinationObjectId: b.id,
      caseValues: [{ kind: "expression", condition: "$listScore = 18", expression: "$listScore = 18" }],
      originConnectionIndex: 1,
    });
    const issues = validate(schemaWith([start, gateway, a, b], [
      createSequenceFlow({ originObjectId: start.id, destinationObjectId: gateway.id }),
      flowA,
      flowB,
    ]));

    expect(issues.some(issue => issue.code === "MF_FLOW_SEQUENCE_CASE_VALUES")).toBe(false);
    expect(issues.some(issue => issue.code === "MF_INCLUSIVE_GATEWAY_CASE_KIND")).toBe(false);
    expect(issues.some(issue => issue.code === "MF_INCLUSIVE_GATEWAY_EXPRESSION_MISSING")).toBe(false);
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

  it("allows Break outside Loop when targetLoopObjectId points to an existing loop", () => {
    const loop = objectFrom("loop", "loop");
    if (loop.kind !== "loopedActivity") {
      throw new Error("Expected loop object.");
    }
    const brk = { ...objectFrom("breakEvent", "break"), targetLoopObjectId: loop.id };
    const issues = validate(schemaWith([objectFrom("startEvent", "start"), loop, brk, objectFrom("endEvent", "end")]));

    expect(issues.some(issue => issue.code === "MF_BREAK_OUTSIDE_LOOP")).toBe(false);
  });

  it("warns when the microflow is approaching the recommended element limit", () => {
    const start = objectFrom("startEvent", "start");
    const end = objectFrom("endEvent", "end", 6000, 0);
    const activities = Array.from({ length: 20 }, (_, index) => actionObject("activity:logMessage", `log-${index + 1}`));
    const issues = validate(schemaWith([start, ...activities, end]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_APPROACHING_LIMIT", severity: "warning" })]));
  });

  it("warns when a complex microflow has no annotation", () => {
    const start = objectFrom("startEvent", "start");
    const end = objectFrom("endEvent", "end", 3200, 0);
    const activities = Array.from({ length: 11 }, (_, index) => actionObject("activity:logMessage", `log-${index + 1}`));
    const issues = validate(schemaWith([start, ...activities, end]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_MISSING_ANNOTATION", severity: "warning" })]));
  });

  it("reports duplicate variable definitions in Problems", () => {
    const start = objectFrom("startEvent", "start");
    const end = objectFrom("endEvent", "end");
    const first = actionObject("activity:variableCreate", "create-variable-a");
    const second = actionObject("activity:variableCreate", "create-variable-b");
    first.action = { ...first.action, variableName: "approvalLevel" } as typeof first.action;
    second.action = { ...second.action, variableName: "ApprovalLevel" } as typeof second.action;
    const issues = validate(schemaWith([start, first, second, end]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "MF_VARIABLE_DUPLICATED",
      severity: "error",
    })]));
  });

  it("warns when Commit happens inside a Loop body", () => {
    const start = objectFrom("startEvent", "start");
    const end = objectFrom("endEvent", "end");
    const createList = actionObject("activity:listCreate", "create-list");
    const loop = objectFrom("loop", "loop");
    const commit = actionObject("activity:objectCommit", "commit-in-loop");
    if (loop.kind !== "loopedActivity") {
      throw new Error("Expected loop object.");
    }
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      entityQualifiedName: "Sales.Order",
    } as typeof createList.action;
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

    const issues = validate(schemaWith([start, createList, loop, end]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "LOOP_COMMIT",
      severity: "warning",
      objectId: "commit-in-loop",
    })]));
  });

  it("warns when integration actions use rollback-only error handling", () => {
    const start = objectFrom("startEvent", "start");
    const end = objectFrom("endEvent", "end");
    const javaAction = actionObject("activity:callJavaAction", "call-java");
    javaAction.action = {
      ...javaAction.action,
      javaActionQualifiedName: "Sales.DoWork",
      errorHandlingType: "rollback",
    } as typeof javaAction.action;

    const issues = validate(schemaWith([start, javaAction, end]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "MISSING_ERROR_HANDLER",
      severity: "warning",
      objectId: "call-java",
    })]));
  });

  it("reports nested if expressions in Decision nodes as an info suggestion", () => {
    const start = objectFrom("startEvent", "start");
    const end = objectFrom("endEvent", "end");
    const decision = objectFrom("decision", "decision");
    if (decision.kind !== "exclusiveSplit" || decision.splitCondition.kind !== "expression") {
      throw new Error("Expected expression decision.");
    }
    decision.splitCondition = {
      ...decision.splitCondition,
      expression: { ...decision.splitCondition.expression, raw: "if $flag then (if $retry then true else false) else false" },
    };

    const issues = validate(schemaWith([start, decision, end]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "NESTED_IF_EXPRESSION",
      severity: "info",
      objectId: "decision",
    })]));
  });

  it("reports List Operation missing source as an error", () => {
    const issues = validate(schemaWith([objectFrom("startEvent", "start"), actionObject("activity:listOperation", "list-operation"), objectFrom("endEvent", "end")]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_LIST_OPERATION_SOURCE_MISSING", severity: "error" })]));
  });

  it("reports Counter missing metricName as an error", () => {
    const issues = validate(schemaWith([objectFrom("startEvent", "start"), actionObject("activity:counter", "counter"), objectFrom("endEvent", "end")]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_ACTION_REQUIRED_FIELD_MISSING", severity: "error" })]));
  });

  it("reports IncrementCounter missing metricName as an error", () => {
    const issues = validate(schemaWith([objectFrom("startEvent", "start"), actionObject("activity:incrementCounter", "increment-counter"), objectFrom("endEvent", "end")]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_ACTION_REQUIRED_FIELD_MISSING", severity: "error" })]));
  });

  it("reports Gauge missing valueExpression as an error", () => {
    const issues = validate(schemaWith([objectFrom("startEvent", "start"), actionObject("activity:gauge", "gauge"), objectFrom("endEvent", "end")]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_ACTION_REQUIRED_FIELD_MISSING", severity: "error" })]));
  });

  it("reports stale Object Activity entity as a metadata issue", () => {
    const createObject = actionObject("activity:objectCreate", "create-object");
    const stale = { ...createObject, action: { ...createObject.action, entityQualifiedName: "Missing.Entity", outputVariableName: "missingEntity" } };
    const issues = validate(schemaWith([objectFrom("startEvent", "start"), stale, objectFrom("endEvent", "end")]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_METADATA_ENTITY_NOT_FOUND", source: "metadata" })]));
  });

  it("normalizes issue microflow id and blocker flags", () => {
    const schema = { ...validSchema(), objectCollection: { ...validSchema().objectCollection, objects: [] } };
    const issues = validate(schema);
    const missingStart = issues.find(issue => issue.code === "MF_START_MISSING");

    expect(missingStart).toEqual(expect.objectContaining({
      microflowId: "MF_VALIDATION_TEST",
      blockPublish: true,
    }));
    expect(missingStart?.id.startsWith("MF_VALIDATION_TEST:")).toBe(true);
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

  it("validates canonical design schema without requiring authoring objectCollection or flows", () => {
    const issues = validateMicroflowSchema({
      schema: validDesignSchema(),
      metadata: EMPTY_MICROFLOW_METADATA_CATALOG,
      options: { mode: "save", includeWarnings: true, includeInfo: true },
    }).issues;

    expect(issues.some(issue => issue.code === "MF_OBJECT_COLLECTION_MISSING")).toBe(false);
    expect(issues.some(issue => issue.code === "MF_FLOWS_MISSING")).toBe(false);
  });

  it("reports dangling workflow edges on canonical design schema", () => {
    const schema = validDesignSchema();
    const issues = validateMicroflowSchema({
      schema: {
        ...schema,
        workflow: {
          ...schema.workflow,
          edges: [{ id: "dangling", sourceNodeID: "start", targetNodeID: "missing-target" }],
        },
      },
      metadata: EMPTY_MICROFLOW_METADATA_CATALOG,
      options: { mode: "save", includeWarnings: true, includeInfo: true },
    }).issues;

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "MF_FLOW_INVALID_TARGET",
      fieldPath: "workflow.edges.0.targetNodeID",
      flowId: "dangling",
    })]));
  });

  it("reports node-count guidance on canonical design schema", () => {
    const schema = validDesignSchema();
    const issues = validateMicroflowSchema({
      schema: {
        ...schema,
        workflow: {
          ...schema.workflow,
          nodes: [
            ...schema.workflow.nodes,
            ...Array.from({ length: 20 }, (_, index) => ({
              id: `activity-${index + 1}`,
              type: "actionActivity",
              data: { objectId: `activity-${index + 1}`, objectKind: "actionActivity", actionKind: "logMessage" },
              meta: { position: { x: 120 * (index + 1), y: 120 } },
            })),
          ],
        },
      },
      metadata: EMPTY_MICROFLOW_METADATA_CATALOG,
      options: { mode: "save", includeWarnings: true, includeInfo: true },
    }).issues;

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({ code: "MF_APPROACHING_LIMIT", severity: "warning" })]));
  });

  it("reports integration error-handling guidance on canonical design schema", () => {
    const schema = validDesignSchema();
    const issues = validateMicroflowSchema({
      schema: {
        ...schema,
        workflow: {
          ...schema.workflow,
          nodes: [
            ...schema.workflow.nodes,
            {
              id: "call-java",
              type: "actionActivity",
              data: {
                objectId: "call-java",
                objectKind: "actionActivity",
                actionKind: "callJavaAction",
                action: {
                  kind: "callJavaAction",
                  errorHandlingType: "rollback",
                },
              },
              meta: { position: { x: 120, y: 120 } },
            },
          ],
        },
      },
      metadata: EMPTY_MICROFLOW_METADATA_CATALOG,
      options: { mode: "save", includeWarnings: true, includeInfo: true },
    }).issues;

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "MISSING_ERROR_HANDLER",
      severity: "warning",
      objectId: "call-java",
    })]));
  });
});
