import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, createSequenceFlow } from "../../adapters";
import { sampleMicroflowSchema } from "../../schema/sample";
import { createMetadataCatalog, EMPTY_MICROFLOW_METADATA_CATALOG } from "../../metadata";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import { createBooleanCaseValue, updateEndEventReturnValue, updateMicroflowReturnType, validateMicroflowSchema, type MicroflowDesignSchema, type MicroflowObject, type MicroflowSchema } from "../../schema";

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

function rawExpression(raw: string) {
  return {
    raw,
    referencedVariables: [],
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: [],
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

  it("reports reserved system variable names on parameters", () => {
    const schema = {
      ...validSchema(),
      parameters: [
        { id: "param-reserved", name: "latestSoapFault", dataType: { kind: "string" as const }, required: true },
      ],
    };
    const issues = validate(schema);

    expect(issues).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: "MF_PARAMETER_NAME_SYSTEM_RESERVED", severity: "error", parameterId: "param-reserved" }),
    ]));
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

  it("reports unknown variables when a deleted parameter is still referenced by an End return expression", () => {
    const start = objectFrom("startEvent", "start");
    const end = objectFrom("endEvent", "end");
    const parameterObject = objectFrom("parameter", "param-node");
    if (parameterObject.kind !== "parameterObject") {
      throw new Error("Expected parameter object.");
    }
    const schema = {
      ...schemaWith([start, parameterObject, end], [createSequenceFlow({ originObjectId: start.id, destinationObjectId: end.id })]),
      parameters: [{ id: parameterObject.parameterId, name: "amount", dataType: { kind: "decimal" as const }, required: true }],
    };
    const typed = updateMicroflowReturnType(schema, { kind: "string" });
    const withExpression = updateEndEventReturnValue(typed, end.id, {
      raw: "if $amount > 100 then 'vip' else 'normal'",
      inferredType: { kind: "string" },
      references: { variables: ["$amount"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
      diagnostics: [],
    });
    const deleted = {
      ...withExpression,
      parameters: [],
      objectCollection: {
        ...withExpression.objectCollection,
        objects: withExpression.objectCollection.objects.filter(object => object.id !== parameterObject.id),
      },
    };

    const issues = validate(deleted);

    expect(issues).toEqual(expect.arrayContaining([
      expect.objectContaining({
        code: "MF_EXPRESSION_UNKNOWN_VARIABLE",
        objectId: "end",
        fieldPath: "returnValue",
        severity: "error",
      }),
    ]));
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

  it("treats rule decisions as boolean decisions for branch validation", () => {
    const start = objectFrom("startEvent", "start");
    const end = objectFrom("endEvent", "end");
    const decision = objectFrom("decision", "decision");
    if (decision.kind !== "exclusiveSplit") {
      throw new Error("Expected exclusiveSplit.");
    }
    decision.splitCondition = {
      kind: "rule",
      ruleQualifiedName: "Sales.Rule1",
      parameterMappings: [],
      resultType: "boolean",
    };
    const trueFlow = createSequenceFlow({
      id: "flow-true",
      originObjectId: decision.id,
      destinationObjectId: end.id,
      edgeKind: "decisionCondition",
      caseValues: [createBooleanCaseValue(true)],
      label: "true",
    });

    const issues = validate(schemaWith([start, decision, end], [trueFlow]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "MF_DECISION_BOOLEAN_FALSE_MISSING",
      severity: "warning",
      objectId: "decision",
    })]));
  });

  it("requires rule decisions to reference a rule", () => {
    const start = objectFrom("startEvent", "start");
    const end = objectFrom("endEvent", "end");
    const decision = objectFrom("decision", "decision");
    if (decision.kind !== "exclusiveSplit") {
      throw new Error("Expected exclusiveSplit.");
    }
    decision.splitCondition = {
      kind: "rule",
      ruleQualifiedName: "",
      parameterMappings: [],
      resultType: "boolean",
    };

    const issues = validate(schemaWith([start, decision, end]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "MF_DECISION_RULE_MISSING",
      objectId: "decision",
      fieldPath: "splitCondition.ruleQualifiedName",
    })]));
  });

  it("validates rule decision parameter mapping expressions", () => {
    const start = objectFrom("startEvent", "start");
    const end = objectFrom("endEvent", "end");
    const decision = objectFrom("decision", "decision");
    if (decision.kind !== "exclusiveSplit") {
      throw new Error("Expected exclusiveSplit.");
    }
    decision.splitCondition = {
      kind: "rule",
      ruleQualifiedName: "Sales.Rule1",
      resultType: "boolean",
      parameterMappings: [
        {
          parameterName: "amount",
          targetParameterName: "amount",
          parameterType: { kind: "decimal" },
          targetType: { kind: "decimal" },
          argumentExpression: { raw: "", referencedVariables: [], diagnostics: [] },
          expression: { raw: "", referencedVariables: [], diagnostics: [] },
        },
      ],
    };

    const issues = validate(schemaWith([start, decision, end]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "MF_EXPR_REQUIRED",
      objectId: "decision",
      fieldPath: "splitCondition.parameterMappings.0.argumentExpression",
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

  it("validates Show Message expressions", () => {
    const message = actionObject("activity:showMessage", "show-message");
    message.action = {
      ...message.action,
      messageExpression: rawExpression("$missingMessage"),
    } as typeof message.action;

    const issues = validate(schemaWith([objectFrom("startEvent", "start"), message, objectFrom("endEvent", "end")]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "MF_EXPR_UNKNOWN_VARIABLE",
      objectId: "show-message",
      fieldPath: "action.messageExpression",
      severity: "error",
    })]));
  });

  it("validates Counter and Gauge value expressions", () => {
    const counter = actionObject("activity:counter", "counter");
    counter.action = {
      ...counter.action,
      metricName: "orders_total",
      valueExpression: rawExpression("1 / 2"),
    } as typeof counter.action;
    const gauge = actionObject("activity:gauge", "gauge");
    gauge.action = {
      ...gauge.action,
      metricName: "queue_depth",
      valueExpression: rawExpression("$missingGaugeValue"),
    } as typeof gauge.action;

    const issues = validate(schemaWith([objectFrom("startEvent", "start"), counter, gauge, objectFrom("endEvent", "end")]));

    expect(issues).toEqual(expect.arrayContaining([
      expect.objectContaining({
        code: "MF_EXPR_USE_DIV_OPERATOR",
        objectId: "counter",
        fieldPath: "action.valueExpression",
        severity: "error",
      }),
      expect.objectContaining({
        code: "MF_EXPR_UNKNOWN_VARIABLE",
        objectId: "gauge",
        fieldPath: "action.valueExpression",
        severity: "error",
      }),
    ]));
  });

  it("validates Notify Workflow payload expressions", () => {
    const notify = actionObject("activity:notifyWorkflow", "notify-workflow");
    notify.action = {
      ...notify.action,
      workflowInstanceVariableName: "workflowInstance",
      notificationName: "OrderSubmitted",
      payloadExpression: rawExpression("$missingPayload"),
    } as typeof notify.action;

    const issues = validate(schemaWith([objectFrom("startEvent", "start"), notify, objectFrom("endEvent", "end")]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "MF_EXPR_UNKNOWN_VARIABLE",
      objectId: "notify-workflow",
      fieldPath: "action.payloadExpression",
      severity: "error",
    })]));
  });

  it("validates Create List initial items expressions", () => {
    const createList = actionObject("activity:listCreate", "create-list");
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "string" },
      itemType: { kind: "string" },
      initialItemsExpression: rawExpression("$missingInitialItems"),
    } as typeof createList.action;

    const issues = validate(schemaWith([objectFrom("startEvent", "start"), createList, objectFrom("endEvent", "end")]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "MF_EXPR_UNKNOWN_VARIABLE",
      objectId: "create-list",
      fieldPath: "action.initialItemsExpression",
      severity: "error",
    })]));
  });

  it("validates Parameter default value expressions", () => {
    const parameterObject = objectFrom("parameter", "param-node");
    if (parameterObject.kind !== "parameterObject") {
      throw new Error("Expected parameterObject.");
    }
    const schema = {
      ...validSchema(),
      parameters: [
        {
          id: parameterObject.parameterId,
          stableId: parameterObject.parameterId,
          name: "amount",
          dataType: { kind: "decimal" as const },
          required: false,
          defaultValue: rawExpression("$missingDefaultValue"),
        },
      ],
      objectCollection: {
        ...validSchema().objectCollection,
        objects: [parameterObject, ...validSchema().objectCollection.objects],
      },
    };

    const issues = validate(schema);

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "MF_EXPR_UNKNOWN_VARIABLE",
      objectId: "param-node",
      parameterId: parameterObject.parameterId,
      fieldPath: `parameters.${parameterObject.parameterId}.defaultValue`,
      severity: "error",
    })]));
  });

  it("validates Throw Exception message expressions", () => {
    const throwException = actionObject("activity:throwException", "throw-exception");
    throwException.action = {
      ...throwException.action,
      messageExpression: rawExpression("$missingThrownMessage"),
    } as typeof throwException.action;

    const issues = validate(schemaWith([objectFrom("startEvent", "start"), throwException, objectFrom("endEvent", "end")]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "MF_EXPR_UNKNOWN_VARIABLE",
      objectId: "throw-exception",
      fieldPath: "action.messageExpression",
      severity: "error",
    })]));
  });

  it("validates Filter List expressions", () => {
    const filterList = actionObject("activity:listFilter", "filter-list");
    filterList.action = {
      ...filterList.action,
      sourceListVariableName: "orders",
      listVariableName: "orders",
      outputVariableName: "filteredOrders",
      conditionExpression: rawExpression("1 / 2"),
      filterExpression: rawExpression("$missingFilterFlag"),
    } as typeof filterList.action;

    const issues = validate(schemaWith([objectFrom("startEvent", "start"), filterList, objectFrom("endEvent", "end")]));

    expect(issues).toEqual(expect.arrayContaining([
      expect.objectContaining({
        code: "MF_EXPR_USE_DIV_OPERATOR",
        objectId: "filter-list",
        fieldPath: "action.conditionExpression",
        severity: "error",
      }),
      expect.objectContaining({
        code: "MF_EXPR_UNKNOWN_VARIABLE",
        objectId: "filter-list",
        fieldPath: "action.filterExpression",
        severity: "error",
      }),
    ]));
  });

  it("accepts Filter List item variable in condition expressions", () => {
    const start = objectFrom("startEvent", "start");
    const createList = actionObject("activity:listCreate", "create-list");
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "integer" },
      itemType: { kind: "integer" },
      listType: "mutable",
    } as typeof createList.action;
    const filterList = actionObject("activity:listFilter", "filter-list-item");
    filterList.action = {
      ...filterList.action,
      sourceListVariableName: "orders",
      listVariableName: "orders",
      outputVariableName: "positiveOrders",
      itemVariableName: "item",
      itemType: { kind: "integer" },
      conditionExpression: rawExpression("$item > 2"),
      filterExpression: rawExpression("$item > 2"),
    } as typeof filterList.action;
    const end = objectFrom("endEvent", "end");

    const issues = validate(schemaWith(
      [start, createList, filterList, end],
      [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: createList.id }),
        createSequenceFlow({ originObjectId: createList.id, destinationObjectId: filterList.id }),
        createSequenceFlow({ originObjectId: filterList.id, destinationObjectId: end.id }),
      ],
    ));

    expect(issues.some(issue =>
      issue.objectId === "filter-list-item"
      && issue.fieldPath === "action.conditionExpression"
      && issue.code === "MF_EXPR_UNKNOWN_VARIABLE"
    )).toBe(false);
    expect(issues.some(issue =>
      issue.objectId === "filter-list-item"
      && issue.fieldPath === "action.filterExpression"
      && issue.code === "MF_EXPR_UNKNOWN_VARIABLE"
    )).toBe(false);
  });

  it("accepts Change List removeWhere item variable in condition expressions", () => {
    const start = objectFrom("startEvent", "start");
    const createList = actionObject("activity:listCreate", "create-list-remove-where");
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "integer" },
      itemType: { kind: "integer" },
      listType: "mutable",
    } as typeof createList.action;
    const changeList = actionObject("activity:listChange", "change-list-remove-where");
    changeList.action = {
      ...changeList.action,
      targetListVariableName: "orders",
      operation: "removeWhere",
      conditionExpression: rawExpression("$item > 2"),
    } as typeof changeList.action;
    const end = objectFrom("endEvent", "end");

    const issues = validate(schemaWith(
      [start, createList, changeList, end],
      [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: createList.id }),
        createSequenceFlow({ originObjectId: createList.id, destinationObjectId: changeList.id }),
        createSequenceFlow({ originObjectId: changeList.id, destinationObjectId: end.id }),
      ],
    ));

    expect(issues.some(issue =>
      issue.objectId === "change-list-remove-where"
      && issue.fieldPath === "action.conditionExpression"
      && issue.code === "MF_EXPR_UNKNOWN_VARIABLE"
    )).toBe(false);
  });

  it("accepts List Operation filter item variable in filter expressions", () => {
    const start = objectFrom("startEvent", "start");
    const createList = actionObject("activity:listCreate", "create-list-list-operation-filter");
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "integer" },
      itemType: { kind: "integer" },
      listType: "mutable",
    } as typeof createList.action;
    const listOperation = actionObject("activity:listOperation", "list-operation-filter");
    listOperation.action = {
      ...listOperation.action,
      leftListVariableName: "orders",
      sourceListVariableName: "orders",
      operation: "filter",
      filterExpression: rawExpression("$item > 2"),
      expression: rawExpression("$item > 2"),
      outputVariableName: "positiveOrders",
      outputListVariableName: "positiveOrders",
      outputElementType: { kind: "integer" },
    } as typeof listOperation.action;
    const end = objectFrom("endEvent", "end");

    const issues = validate(schemaWith(
      [start, createList, listOperation, end],
      [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: createList.id }),
        createSequenceFlow({ originObjectId: createList.id, destinationObjectId: listOperation.id }),
        createSequenceFlow({ originObjectId: listOperation.id, destinationObjectId: end.id }),
      ],
    ));

    expect(issues.some(issue =>
      issue.objectId === "list-operation-filter"
      && issue.fieldPath === "action.filterExpression"
      && issue.code === "MF_EXPR_UNKNOWN_VARIABLE"
    )).toBe(false);
    expect(issues.some(issue =>
      issue.objectId === "list-operation-filter"
      && issue.fieldPath === "action.expression"
      && issue.code === "MF_EXPR_UNKNOWN_VARIABLE"
    )).toBe(false);
  });

  it("accepts List Operation map item variable in map expressions", () => {
    const start = objectFrom("startEvent", "start");
    const createList = actionObject("activity:listCreate", "create-list-list-operation-map");
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "integer" },
      itemType: { kind: "integer" },
      listType: "mutable",
    } as typeof createList.action;
    const listOperation = actionObject("activity:listOperation", "list-operation-map");
    listOperation.action = {
      ...listOperation.action,
      leftListVariableName: "orders",
      sourceListVariableName: "orders",
      operation: "map",
      expression: rawExpression("$item + 1"),
      outputVariableName: "mappedOrders",
      outputListVariableName: "mappedOrders",
      outputElementType: { kind: "integer" },
    } as typeof listOperation.action;
    const end = objectFrom("endEvent", "end");

    const issues = validate(schemaWith(
      [start, createList, listOperation, end],
      [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: createList.id }),
        createSequenceFlow({ originObjectId: createList.id, destinationObjectId: listOperation.id }),
        createSequenceFlow({ originObjectId: listOperation.id, destinationObjectId: end.id }),
      ],
    ));

    expect(issues.some(issue =>
      issue.objectId === "list-operation-map"
      && issue.fieldPath === "action.expression"
      && issue.code === "MF_EXPR_UNKNOWN_VARIABLE"
    )).toBe(false);
  });

  it("accepts List Operation sort item variable in sort expressions", () => {
    const start = objectFrom("startEvent", "start");
    const createList = actionObject("activity:listCreate", "create-list-list-operation-sort");
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "object", entityQualifiedName: "Sales.Order" },
      itemType: { kind: "object", entityQualifiedName: "Sales.Order" },
      listType: "mutable",
    } as typeof createList.action;
    const listOperation = actionObject("activity:listOperation", "list-operation-sort");
    listOperation.action = {
      ...listOperation.action,
      leftListVariableName: "orders",
      sourceListVariableName: "orders",
      operation: "sort",
      sortExpression: rawExpression("$item/score"),
      sortKeys: [{ expression: rawExpression("$item/score"), direction: "asc" }],
      outputVariableName: "sortedOrders",
      outputListVariableName: "sortedOrders",
      outputElementType: { kind: "object", entityQualifiedName: "Sales.Order" },
    } as typeof listOperation.action;
    const end = objectFrom("endEvent", "end");

    const issues = validate(schemaWith(
      [start, createList, listOperation, end],
      [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: createList.id }),
        createSequenceFlow({ originObjectId: createList.id, destinationObjectId: listOperation.id }),
        createSequenceFlow({ originObjectId: listOperation.id, destinationObjectId: end.id }),
      ],
    ));

    expect(issues.some(issue =>
      issue.objectId === "list-operation-sort"
      && issue.fieldPath === "action.sortExpression"
      && issue.code === "MF_EXPR_UNKNOWN_VARIABLE"
    )).toBe(false);
  });

  it("accepts Sort List item variable in sort expressions", () => {
    const start = objectFrom("startEvent", "start");
    const createList = actionObject("activity:listCreate", "create-list-sort-list");
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "object", entityQualifiedName: "Sales.Order" },
      itemType: { kind: "object", entityQualifiedName: "Sales.Order" },
      listType: "mutable",
    } as typeof createList.action;
    const sortList = actionObject("activity:listSort", "sort-list");
    sortList.action = {
      ...sortList.action,
      kind: "sortList",
      sourceListVariableName: "orders",
      listVariableName: "orders",
      sortExpression: rawExpression("$item/score"),
      direction: "asc",
      outputVariableName: "sortedOrders",
      outputElementType: { kind: "object", entityQualifiedName: "Sales.Order" },
    } as typeof sortList.action;
    const end = objectFrom("endEvent", "end");

    const issues = validate(schemaWith(
      [start, createList, sortList, end],
      [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: createList.id }),
        createSequenceFlow({ originObjectId: createList.id, destinationObjectId: sortList.id }),
        createSequenceFlow({ originObjectId: sortList.id, destinationObjectId: end.id }),
      ],
    ));

    expect(issues.some(issue =>
      issue.objectId === "sort-list"
      && issue.fieldPath === "action.sortExpression"
      && issue.code === "MF_EXPR_UNKNOWN_VARIABLE"
    )).toBe(false);
  });

  it("accepts Sort List item variable in sort key expressions", () => {
    const start = objectFrom("startEvent", "start");
    const createList = actionObject("activity:listCreate", "create-list-sort-list-sort-key");
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "object", entityQualifiedName: "Sales.Order" },
      itemType: { kind: "object", entityQualifiedName: "Sales.Order" },
      listType: "mutable",
    } as typeof createList.action;
    const sortList = actionObject("activity:listSort", "sort-list-sort-key");
    sortList.action = {
      ...sortList.action,
      kind: "sortList",
      sourceListVariableName: "orders",
      listVariableName: "orders",
      sortKeys: [{ expression: rawExpression("$item/score"), direction: "asc" }],
      direction: "asc",
      outputVariableName: "sortedOrders",
      outputElementType: { kind: "object", entityQualifiedName: "Sales.Order" },
    } as typeof sortList.action;
    const end = objectFrom("endEvent", "end");

    const issues = validate(schemaWith(
      [start, createList, sortList, end],
      [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: createList.id }),
        createSequenceFlow({ originObjectId: createList.id, destinationObjectId: sortList.id }),
        createSequenceFlow({ originObjectId: sortList.id, destinationObjectId: end.id }),
      ],
    ));

    expect(issues.some(issue =>
      issue.objectId === "sort-list-sort-key"
      && issue.fieldPath === "action.sortKeys.0.expression"
      && issue.code === "MF_EXPR_UNKNOWN_VARIABLE"
    )).toBe(false);
  });

  it("validates Error Event message expressions", () => {
    const errorEvent = objectFrom("errorEvent", "error-1");
    if (errorEvent.kind !== "errorEvent") {
      throw new Error("Expected errorEvent.");
    }
    errorEvent.error = {
      ...errorEvent.error,
      messageExpression: rawExpression("$missingErrorMessage"),
    };

    const issues = validate(schemaWith([objectFrom("startEvent", "start"), errorEvent, objectFrom("endEvent", "end")]));

    expect(issues).toEqual(expect.arrayContaining([expect.objectContaining({
      code: "MF_EXPR_UNKNOWN_VARIABLE",
      objectId: "error-1",
      fieldPath: "error.messageExpression",
      severity: "error",
    })]));
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

  it("reports duplicate variable definitions on canonical design schema", () => {
    const schema = validDesignSchema();
    const issues = validateMicroflowSchema({
      schema: {
        ...schema,
        workflow: {
          ...schema.workflow,
          nodes: [
            ...schema.workflow.nodes,
            {
              id: "create-variable-a",
              type: "actionActivity",
              data: {
                objectId: "create-variable-a",
                objectKind: "actionActivity",
                title: "Create Variable",
                actionKind: "createVariable",
                action: {
                  id: "action-create-variable-a",
                  kind: "createVariable",
                  officialType: "Microflows$CreateVariableAction",
                  errorHandlingType: "rollback",
                  documentation: "",
                  editor: { category: "variable", iconKey: "variable", availability: "supported" },
                  variableName: "approvalLevel",
                  dataType: { kind: "string" },
                  initialValue: { raw: "'L1'", referencedVariables: [], references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
                  readonly: false,
                },
              },
              meta: { position: { x: 120, y: 120 } },
            },
            {
              id: "create-variable-b",
              type: "actionActivity",
              data: {
                objectId: "create-variable-b",
                objectKind: "actionActivity",
                title: "Create Variable",
                actionKind: "createVariable",
                action: {
                  id: "action-create-variable-b",
                  kind: "createVariable",
                  officialType: "Microflows$CreateVariableAction",
                  errorHandlingType: "rollback",
                  documentation: "",
                  editor: { category: "variable", iconKey: "variable", availability: "supported" },
                  variableName: "ApprovalLevel",
                  dataType: { kind: "string" },
                  initialValue: { raw: "'L2'", referencedVariables: [], references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
                  readonly: false,
                },
              },
              meta: { position: { x: 300, y: 120 } },
            },
          ],
        },
      },
      metadata: EMPTY_MICROFLOW_METADATA_CATALOG,
      options: { mode: "save", includeWarnings: true, includeInfo: true },
    }).issues;

    expect(issues).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: "MF_VARIABLE_DUPLICATED", severity: "error", objectId: "create-variable-a" }),
      expect.objectContaining({ code: "MF_VARIABLE_DUPLICATED", severity: "error", objectId: "create-variable-b" }),
    ]));
  });

  it("reports reserved system variable names from variable index diagnostics", () => {
    const createVariable = actionObject("activity:variableCreate", "create-variable-reserved");
    if (createVariable.action.kind !== "createVariable") {
      throw new Error("Expected create variable action.");
    }
    const issues = validate({
      ...schemaWith([{
        ...createVariable,
        action: {
          ...createVariable.action,
          variableName: "latestHttpResponse",
        },
      }]),
      parameters: [],
    });

    expect(issues).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: "MF_VARIABLE_NAME_RESERVED", severity: "error", objectId: "create-variable-reserved" }),
    ]));
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

