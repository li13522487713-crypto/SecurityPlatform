import { describe, expect, it } from "vitest";

import type { MicroflowTraceFrame } from "../debug/trace-types";
import type { MicroflowDesignSchema, MicroflowWorkflowNodeJSON } from "../schema/types";
import { deriveNodeInlineConfig } from "./derive-node-inline-config";

function buildSchema(node: MicroflowWorkflowNodeJSON): MicroflowDesignSchema {
  return {
    schemaVersion: "flowgram.microflow.v1",
    id: "mf-inline-derive",
    moduleId: "module-1",
    name: "mf-inline-derive",
    displayName: "mf-inline-derive",
    workflow: {
      nodes: [node],
      edges: [],
    },
    editor: {
      viewport: { x: 0, y: 0, zoom: 1 },
      zoom: 1,
      selection: {},
      gridEnabled: true,
      showMiniMap: true,
    },
    parameters: [],
    returnType: "Nothing",
    returnVariableName: "result",
    variables: [],
    validation: { issues: [] },
    audit: {},
  } as unknown as MicroflowDesignSchema;
}

function runtimeFrame(nodeId: string): MicroflowTraceFrame {
  return {
    id: "frame-1",
    runId: "run-1",
    objectId: nodeId,
    status: "failed",
    startedAt: "2026-05-05T10:00:00.000Z",
    durationMs: 12,
    input: { riskScore: 92 },
    output: { statusCode: 500 },
    variablesSnapshot: {
      riskScore: {
        name: "riskScore",
        type: { kind: "primitive", name: "Integer" } as never,
        valuePreview: "92",
      },
    },
    error: {
      code: "RUNTIME_ASSERTION_FAILED" as never,
      message: "boom",
      callStack: ["node-1", "node-2"],
    },
  };
}

function buildMatrixSchema(): MicroflowDesignSchema {
  const start: MicroflowWorkflowNodeJSON = {
    id: "start",
    type: "event",
    data: {
      objectId: "start",
      objectKind: "startEvent",
      collectionId: "nodes",
      title: "开始",
      validationState: "valid",
      issueCount: 0,
    } as never,
    meta: { position: { x: 40, y: 120 } },
  };
  const decision: MicroflowWorkflowNodeJSON = {
    id: "decision",
    type: "decision",
    data: {
      objectId: "decision",
      objectKind: "exclusiveSplit",
      collectionId: "nodes",
      title: "判断",
      validationState: "valid",
      issueCount: 0,
      splitCondition: { kind: "expression", expression: { raw: "$riskScore >= 80" } },
    } as never,
    meta: { position: { x: 240, y: 120 } },
  };
  const rest: MicroflowWorkflowNodeJSON = {
    id: "rest",
    type: "activity",
    data: {
      objectId: "rest",
      objectKind: "actionActivity",
      collectionId: "nodes",
      title: "REST",
      validationState: "valid",
      issueCount: 0,
      actionKind: "restCall",
      action: {
        request: { method: "POST", urlExpression: { raw: "/api/incidents/$incidentId" }, body: { kind: "json", expression: { raw: "{\"id\":\"$incidentId\"}" } } },
        response: { handling: { kind: "storeToVariable", outputVariableName: "response" } },
      },
    } as never,
    meta: { position: { x: 440, y: 120 } },
  };
  const call: MicroflowWorkflowNodeJSON = {
    id: "call",
    type: "activity",
    data: {
      objectId: "call",
      objectKind: "actionActivity",
      collectionId: "nodes",
      title: "调用",
      validationState: "valid",
      issueCount: 0,
      actionKind: "callMicroflow",
      action: { calledMicroflowId: "Calc", argumentMappings: [{ parameterName: "a", valueExpression: { raw: "$.response.score" } }], outputVariableName: "riskScore" },
    } as never,
    meta: { position: { x: 640, y: 120 } },
  };
  const loop: MicroflowWorkflowNodeJSON = {
    id: "loop",
    type: "activity",
    data: {
      objectId: "loop",
      objectKind: "loopedActivity",
      collectionId: "nodes",
      title: "循环",
      validationState: "valid",
      issueCount: 0,
      loopSource: { kind: "iterableList", listVariableName: "$assetList", iteratorVariableName: "item" },
      currentIndexVariableName: "index",
    } as never,
    meta: { position: { x: 840, y: 120 } },
  };
  const end: MicroflowWorkflowNodeJSON = {
    id: "end",
    type: "event",
    data: {
      objectId: "end",
      objectKind: "endEvent",
      collectionId: "nodes",
      title: "结束",
      validationState: "valid",
      issueCount: 0,
    } as never,
    meta: { position: { x: 1040, y: 120 } },
  };
  return {
    ...buildSchema(start),
    parameters: [{ id: "p1", name: "incidentId", dataType: { kind: "String" } as never }] as never,
    workflow: {
      nodes: [start, decision, rest, call, loop, end],
      edges: [
        { id: "f1", sourceNodeID: "start", targetNodeID: "decision", data: { flowId: "f1" } as never },
        { id: "f2", sourceNodeID: "decision", targetNodeID: "rest", sourcePortID: "decision:true", caseValues: [{ kind: "boolean", value: true }], data: { flowId: "f2", label: "true", caseValues: [{ kind: "boolean", value: true }] } as never },
        { id: "f3", sourceNodeID: "rest", targetNodeID: "call", data: { flowId: "f3" } as never },
        { id: "f4", sourceNodeID: "call", targetNodeID: "loop", data: { flowId: "f4" } as never },
        { id: "f5", sourceNodeID: "loop", targetNodeID: "end", data: { flowId: "f5" } as never },
      ] as never,
    },
    variables: [{ id: "v1", name: "assetList", type: { kind: "List" } as never, scope: "workflow" } as never],
  } as MicroflowDesignSchema;
}

describe("deriveNodeInlineConfig", () => {
  it("provides variable options for all variable/expression-like editable fields", () => {
    const schema = buildMatrixSchema();
    for (const node of schema.workflow.nodes) {
      const config = deriveNodeInlineConfig({
        node,
        schema,
      });
      const optionRequiredEditTypes = new Set([
        "variable",
        "expression",
        "condition",
        "http",
        "assignment",
        "mapping",
        "json",
        "select",
        "text",
      ]);
      for (const section of config.sections) {
        for (const field of section.fields) {
          if (!optionRequiredEditTypes.has(field.editType)) {
            continue;
          }
          expect(Array.isArray(field.options)).toBe(true);
          expect((field.options ?? []).length).toBeGreaterThan(0);
        }
      }
    }
  });
  function findFieldOption(inline: ReturnType<typeof deriveNodeInlineConfig>, fieldPath: string) {
    return inline.sections.flatMap(section => section.fields).find(field => field.fieldPath === fieldPath)?.options;
  }
  function findField(inline: ReturnType<typeof deriveNodeInlineConfig>, fieldPath: string) {
    return inline.sections.flatMap(section => section.fields).find(field => field.fieldPath === fieldPath);
  }

  it("derives decision inline config with condition and branch sections", () => {
    const node: MicroflowWorkflowNodeJSON = {
      id: "decision-1",
      type: "decision",
      data: {
        objectId: "decision-1",
        objectKind: "exclusiveSplit",
        collectionId: "nodes",
        title: "判断",
        validationState: "valid",
        issueCount: 0,
        splitCondition: {
          kind: "expression",
          expression: { raw: "$riskScore >= 80" },
        },
      } as never,
      meta: { position: { x: 100, y: 100 } },
    };
    const schema = buildSchema(node);
    schema.workflow.edges = [{
      id: "flow-true",
      sourceNodeID: "decision-1",
      targetNodeID: "end",
      sourcePortID: "decision:true",
      targetPortID: "in",
      data: { flowId: "flow-true", caseValues: [{ kind: "boolean", value: true }] } as never,
    }] as never;

    const inline = deriveNodeInlineConfig({ node, schema });
    expect(inline.summaryLines[0]?.value).toContain("riskScore >= 80");
    expect(inline.sections.some(section => section.kind === "conditions")).toBe(true);
    expect(inline.sections.some(section => section.kind === "branches")).toBe(true);
    const conditionField = inline.sections
      .flatMap(section => section.fields)
      .find(field => field.fieldPath === "data.splitCondition.expression.raw");
    expect(Boolean(conditionField)).toBe(true);
    const branchField = inline.sections
      .flatMap(section => section.fields)
      .find(field => field.fieldPath.includes("edge:flow-true"));
    expect(Boolean(branchField)).toBe(true);
  });

  it("derives rest inline config with method/url/output fields", () => {
    const node: MicroflowWorkflowNodeJSON = {
      id: "rest-1",
      type: "activity",
      data: {
        objectId: "rest-1",
        objectKind: "actionActivity",
        collectionId: "nodes",
        title: "REST",
        validationState: "valid",
        issueCount: 0,
        actionKind: "restCall",
        action: {
          request: {
            method: "POST",
            urlExpression: { raw: "/api/incidents" },
            queryParameters: [{ key: "incidentId", valueExpression: { raw: "$incidentId" } }],
            body: { kind: "json", expression: { raw: "{\"incidentId\":\"$incidentId\"}" } },
          },
          response: {
            handling: { kind: "storeToVariable", outputVariableName: "response" },
          },
        },
      } as never,
      meta: { position: { x: 100, y: 100 } },
    };
    const inline = deriveNodeInlineConfig({ node, schema: buildSchema(node) });
    expect(inline.summaryLines[0]?.value).toContain("POST /api/incidents");
    const httpSection = inline.sections.find(section => section.kind === "http");
    expect(httpSection?.fields.some(field => field.fieldPath === "data.action.request.method")).toBe(true);
    expect(httpSection?.fields.some(field => field.fieldPath === "data.action.request.urlExpression.raw")).toBe(true);
    expect(httpSection?.fields.some(field => field.fieldPath === "data.action.response.handling.outputVariableName")).toBe(true);
  });

  it("derives variable inline config for create/change variable actions", () => {
    const node: MicroflowWorkflowNodeJSON = {
      id: "var-1",
      type: "activity",
      data: {
        objectId: "var-1",
        objectKind: "actionActivity",
        collectionId: "nodes",
        title: "变量",
        validationState: "valid",
        issueCount: 0,
        actionKind: "changeVariable",
        action: {
          kind: "changeVariable",
          targetVariableName: "approvalStatus",
          newValueExpression: { raw: "\"pending\"" },
        },
      } as never,
      meta: { position: { x: 100, y: 100 } },
    };

    const inline = deriveNodeInlineConfig({ node, schema: buildSchema(node) });
    expect(inline.summaryLines.some(line => line.value.includes("approvalStatus"))).toBe(true);
    expect(inline.sections.some(section => section.kind === "variables")).toBe(true);
    const variableSection = inline.sections.find(section => section.kind === "variables");
    expect(variableSection?.fields.some(field => field.fieldPath === "data.action.targetVariableName")).toBe(true);
    expect(variableSection?.fields.some(field => field.fieldPath === "data.action.newValueExpression.raw")).toBe(true);
  });

  it("derives call microflow inline config with mapping/return fields", () => {
    const node: MicroflowWorkflowNodeJSON = {
      id: "call-1",
      type: "activity",
      data: {
        objectId: "call-1",
        objectKind: "actionActivity",
        collectionId: "nodes",
        title: "调用子流程",
        validationState: "valid",
        issueCount: 0,
        actionKind: "callMicroflow",
        action: {
          kind: "callMicroflow",
          calledMicroflowId: "CalculateRiskScore",
          argumentMappings: [
            { parameterName: "incidentId", valueExpression: { raw: "$incidentId" } },
            { parameterName: "amount", valueExpression: { raw: "$order.amount" } },
          ],
          outputVariableName: "riskScore",
        },
      } as never,
      meta: { position: { x: 120, y: 100 } },
    };

    const inline = deriveNodeInlineConfig({ node, schema: buildSchema(node) });
    expect(inline.summaryLines.some(line => line.value.includes("CalculateRiskScore"))).toBe(true);
    expect(inline.sections.some(section => section.kind === "inputs")).toBe(true);
    expect(inline.sections.some(section => section.kind === "outputs")).toBe(true);
    const mappingField = inline.sections
      .flatMap(section => section.fields)
      .find(field => field.fieldPath.includes("argumentMappings"));
    expect(Boolean(mappingField)).toBe(true);
  });

  it("provides variable options for generic action inline fields with upstream link and $. alias", () => {
    const sourceNode: MicroflowWorkflowNodeJSON = {
      id: "rest-source",
      type: "activity",
      data: {
        objectId: "rest-source",
        objectKind: "actionActivity",
        collectionId: "nodes",
        title: "REST Source",
        validationState: "valid",
        issueCount: 0,
        actionKind: "restCall",
        action: {
          kind: "restCall",
          request: { method: "GET", urlExpression: { raw: "/api/source" } },
          response: {
            handling: { kind: "storeToVariable", outputVariableName: "response" },
          },
        },
      } as never,
      meta: { position: { x: 0, y: 0 } },
    };
    const targetNode: MicroflowWorkflowNodeJSON = {
      id: "action-target",
      type: "activity",
      data: {
        objectId: "action-target",
        objectKind: "actionActivity",
        collectionId: "nodes",
        title: "Action Target",
        validationState: "valid",
        issueCount: 0,
        actionKind: "customAction",
        action: {
          inputExpression: { raw: "$response" },
          outputVariableName: "resultVar",
        },
      } as never,
      meta: { position: { x: 220, y: 0 } },
    };
    const schema = buildSchema(sourceNode);
    schema.workflow.nodes.push(targetNode);
    schema.workflow.edges.push({
      id: "flow-source-target",
      sourceNodeID: "rest-source",
      targetNodeID: "action-target",
      sourcePortID: "out",
      targetPortID: "in",
      data: { flowId: "flow-source-target" } as never,
    } as never);

    const inline = deriveNodeInlineConfig({ node: targetNode, schema });
    const inputField = inline.sections
      .flatMap(section => section.fields)
      .find(field => field.fieldPath === "data.action.inputExpression.raw");
    const outputField = inline.sections
      .flatMap(section => section.fields)
      .find(field => field.fieldPath === "data.action.outputVariableName");

    expect(Array.isArray(inputField?.options)).toBe(true);
    expect(inputField?.options?.some(option => option.value === "$response")).toBe(true);
    expect(inputField?.options?.some(option => option.value === "$.response")).toBe(true);
    expect(Array.isArray(outputField?.options)).toBe(true);
    expect(outputField?.options?.some(option => option.value === "response")).toBe(true);
  });

  it("injects runtime/error projection from trace frame", () => {
    const node: MicroflowWorkflowNodeJSON = {
      id: "node-1",
      type: "activity",
      data: {
        objectId: "node-1",
        objectKind: "actionActivity",
        collectionId: "nodes",
        title: "动作",
        validationState: "valid",
        issueCount: 0,
      } as never,
      meta: { position: { x: 100, y: 100 } },
    };
    const inline = deriveNodeInlineConfig({
      node,
      schema: buildSchema(node),
      runtimeFrame: runtimeFrame("node-1"),
      viewMode: "inspectingError",
    });

    expect(inline.viewMode).toBe("inspectingError");
    expect(inline.runtime?.failed).toBe(true);
    expect(inline.runtime?.durationMs).toBe(12);
    expect(inline.runtime?.error?.message).toBe("boom");
    expect(inline.runtime?.variableSnapshot?.[0]?.name).toBe("riskScore");
  });

  it("derives start/end inline config fields", () => {
    const startNode: MicroflowWorkflowNodeJSON = {
      id: "start-1",
      type: "start",
      data: {
        objectId: "start-1",
        objectKind: "startEvent",
        collectionId: "nodes",
        title: "开始",
        validationState: "valid",
        issueCount: 0,
      } as never,
      meta: { position: { x: 0, y: 0 } },
    };
    const endNode: MicroflowWorkflowNodeJSON = {
      id: "end-1",
      type: "end",
      data: {
        objectId: "end-1",
        objectKind: "endEvent",
        collectionId: "nodes",
        title: "结束",
        validationState: "valid",
        issueCount: 0,
      } as never,
      meta: { position: { x: 200, y: 0 } },
    };
    const startSchema = buildSchema(startNode);
    startSchema.parameters = [
      { id: "p-order", name: "orderId", dataType: { kind: "string" }, required: true } as never,
      { id: "p-amount", name: "amount", dataType: { kind: "decimal" }, required: false } as never,
    ];
    const startInline = deriveNodeInlineConfig({ node: startNode, schema: startSchema });
    const endInline = deriveNodeInlineConfig({ node: endNode, schema: buildSchema(endNode) });
    expect(startInline.summaryLines.length).toBeGreaterThan(0);
    expect(startInline.sections.some(section => section.kind === "inputs")).toBe(true);
    expect(findField(startInline, "parameters.0.name")).toMatchObject({ value: "orderId", editType: "text" });
    expect(findFieldOption(startInline, "parameters.0.dataType.kind")?.some(option => option.value === "string")).toBe(true);
    expect(findFieldOption(startInline, "parameters.0.required")?.some(option => option.value === "true")).toBe(true);
    expect(findField(startInline, "parameters.1.name")).toMatchObject({ value: "amount", editType: "text" });
    expect(endInline.sections.some(section => section.id === "output-mappings")).toBe(true);
  });

  it("derives loop and error handler branch-like summaries", () => {
    const loopNode: MicroflowWorkflowNodeJSON = {
      id: "loop-1",
      type: "activity",
      data: {
        objectId: "loop-1",
        objectKind: "actionActivity",
        collectionId: "nodes",
        title: "循环",
        validationState: "valid",
        issueCount: 0,
        actionKind: "forEach",
        action: {
          kind: "forEach",
          collectionExpression: { raw: "$assetList" },
          itemVariableName: "item",
        },
      } as never,
      meta: { position: { x: 100, y: 0 } },
    };
    const errorNode: MicroflowWorkflowNodeJSON = {
      id: "error-1",
      type: "activity",
      data: {
        objectId: "error-1",
        objectKind: "actionActivity",
        collectionId: "nodes",
        title: "错误处理",
        validationState: "valid",
        issueCount: 0,
        actionKind: "errorHandler",
        action: {
          kind: "errorHandler",
          catchType: "HTTP_ERROR",
          errorVariableName: "error",
          fallbackExpression: { raw: "$fallbackResult" },
        },
      } as never,
      meta: { position: { x: 300, y: 0 } },
    };
    const loopInline = deriveNodeInlineConfig({ node: loopNode, schema: buildSchema(loopNode) });
    const errorInline = deriveNodeInlineConfig({ node: errorNode, schema: buildSchema(errorNode) });
    expect(loopInline.summaryLines.some(line => line.value.includes("$assetList") || line.value.includes("item"))).toBe(true);
    expect(errorInline.summaryLines.some(line => line.value.toLowerCase().includes("error"))).toBe(true);
  });

  it("ensures key inline-editable fields provide variable options across node kinds", () => {
    const decisionNode: MicroflowWorkflowNodeJSON = {
      id: "decision-v",
      type: "decision",
      data: {
        objectId: "decision-v",
        objectKind: "exclusiveSplit",
        collectionId: "nodes",
        title: "判断",
        validationState: "valid",
        issueCount: 0,
        splitCondition: { kind: "expression", expression: { raw: "$riskScore >= 80" } },
      } as never,
      meta: { position: { x: 0, y: 0 } },
    };
    const restNode: MicroflowWorkflowNodeJSON = {
      id: "rest-v",
      type: "activity",
      data: {
        objectId: "rest-v",
        objectKind: "actionActivity",
        collectionId: "nodes",
        title: "REST",
        validationState: "valid",
        issueCount: 0,
        actionKind: "restCall",
        action: {
          request: {
            method: "POST",
            urlExpression: { raw: "/api/incidents" },
            body: { kind: "json", expression: { raw: "{\"id\":\"$incidentId\"}" } },
          },
          response: { handling: { kind: "storeToVariable", outputVariableName: "response" } },
        },
      } as never,
      meta: { position: { x: 100, y: 0 } },
    };
    const actionNode: MicroflowWorkflowNodeJSON = {
      id: "action-v",
      type: "activity",
      data: {
        objectId: "action-v",
        objectKind: "actionActivity",
        collectionId: "nodes",
        title: "Action",
        validationState: "valid",
        issueCount: 0,
        actionKind: "customAction",
        action: {
          inputExpression: { raw: "$response" },
          outputVariableName: "resultVar",
        },
      } as never,
      meta: { position: { x: 200, y: 0 } },
    };
    const variableNode: MicroflowWorkflowNodeJSON = {
      id: "var-v",
      type: "activity",
      data: {
        objectId: "var-v",
        objectKind: "actionActivity",
        collectionId: "nodes",
        title: "变量",
        validationState: "valid",
        issueCount: 0,
        actionKind: "changeVariable",
        action: {
          kind: "changeVariable",
          targetVariableName: "approvalStatus",
          newValueExpression: { raw: "\"pending\"" },
        },
      } as never,
      meta: { position: { x: 300, y: 0 } },
    };
    const callNode: MicroflowWorkflowNodeJSON = {
      id: "call-v",
      type: "activity",
      data: {
        objectId: "call-v",
        objectKind: "actionActivity",
        collectionId: "nodes",
        title: "调用子流程",
        validationState: "valid",
        issueCount: 0,
        actionKind: "callMicroflow",
        action: {
          kind: "callMicroflow",
          calledMicroflowId: "CalculateRiskScore",
          argumentMappings: [{ parameterName: "incidentId", valueExpression: { raw: "$incidentId" } }],
          outputVariableName: "riskScore",
        },
      } as never,
      meta: { position: { x: 400, y: 0 } },
    };
    const schema = buildSchema(decisionNode);
    schema.parameters = [{ id: "p1", name: "incidentId", dataType: { kind: "string" }, required: true } as never];
    schema.workflow.nodes.push(restNode, actionNode, variableNode, callNode);
    schema.workflow.edges = [
      { id: "e1", sourceNodeID: "rest-v", targetNodeID: "action-v", sourcePortID: "out", targetPortID: "in", data: { flowId: "e1" } as never },
      { id: "e2", sourceNodeID: "rest-v", targetNodeID: "decision-v", sourcePortID: "out", targetPortID: "in", data: { flowId: "e2" } as never },
      { id: "e3", sourceNodeID: "action-v", targetNodeID: "var-v", sourcePortID: "out", targetPortID: "in", data: { flowId: "e3" } as never },
      { id: "e4", sourceNodeID: "var-v", targetNodeID: "call-v", sourcePortID: "out", targetPortID: "in", data: { flowId: "e4" } as never },
    ] as never;

    const decisionInline = deriveNodeInlineConfig({ node: decisionNode, schema });
    const restInline = deriveNodeInlineConfig({ node: restNode, schema });
    const actionInline = deriveNodeInlineConfig({ node: actionNode, schema });
    const variableInline = deriveNodeInlineConfig({ node: variableNode, schema });
    const callInline = deriveNodeInlineConfig({ node: callNode, schema });

    expect((findFieldOption(decisionInline, "data.splitCondition.expression.raw") ?? []).length).toBeGreaterThan(0);
    expect((findFieldOption(restInline, "data.action.request.urlExpression.raw") ?? []).length).toBeGreaterThan(0);
    expect((findFieldOption(restInline, "data.action.request.body.expression.raw") ?? []).length).toBeGreaterThan(0);
    expect((findFieldOption(actionInline, "data.action.inputExpression.raw") ?? []).length).toBeGreaterThan(0);
    expect((findFieldOption(actionInline, "data.action.outputVariableName") ?? []).length).toBeGreaterThan(0);
    expect((findFieldOption(variableInline, "data.action.newValueExpression.raw") ?? []).length).toBeGreaterThan(0);
    expect((findFieldOption(callInline, "data.action.argumentMappings.0.valueExpression.raw") ?? []).length).toBeGreaterThan(0);
    const loopNode: MicroflowWorkflowNodeJSON = {
      id: "loop-v",
      type: "activity",
      data: {
        objectId: "loop-v",
        objectKind: "loopedActivity",
        collectionId: "nodes",
        title: "循环",
        validationState: "valid",
        issueCount: 0,
        actionKind: "forEach",
        loopSource: { kind: "iterableList", listVariableName: "$assetList", iteratorVariableName: "item" },
        currentIndexVariableName: "index",
      } as never,
      meta: { position: { x: 520, y: 0 } },
    };
    const loopSchema = buildSchema(loopNode);
    const loopInline = deriveNodeInlineConfig({ node: loopNode, schema: loopSchema });
    expect((findFieldOption(loopInline, "data.loopSource.iteratorVariableName") ?? []).length).toBeGreaterThan(0);
    expect((findFieldOption(loopInline, "data.currentIndexVariableName") ?? []).length).toBeGreaterThan(0);
  });

  it("derives approval inline config with approver/branch/result fields", () => {
    const approvalNode: MicroflowWorkflowNodeJSON = {
      id: "approval-1",
      type: "activity",
      data: {
        objectId: "approval-1",
        objectKind: "actionActivity",
        collectionId: "nodes",
        title: "人工审批",
        validationState: "valid",
        issueCount: 0,
        actionKind: "completeUserTask",
        action: {
          kind: "completeUserTask",
        },
        approver: "$manager",
        resultVariable: "approvalResult",
      } as never,
      meta: { position: { x: 480, y: 0 } },
    };
    const inline = deriveNodeInlineConfig({
      node: approvalNode,
      schema: buildSchema(approvalNode),
      runtimeFrame: {
        ...runtimeFrame("approval-1"),
        status: "success",
        output: {
          branchTrace: [{
            flowId: "flow-approved",
            branchId: "approved",
            selected: true,
            status: "completed",
          }],
        },
      },
    });
    expect(inline.sections.some(section => section.kind === "approval")).toBe(true);
    expect(inline.summaryLines.some(line => line.value.includes("$manager"))).toBe(true);
    expect(
      inline.sections
        .flatMap(section => section.fields)
        .some(field => field.fieldPath === "data.resultVariable" && field.value.includes("approvalResult")),
    ).toBe(true);
    expect((findFieldOption(inline, "data.approver") ?? []).length).toBeGreaterThan(0);
    expect((findFieldOption(inline, "data.resultVariable") ?? []).length).toBeGreaterThan(0);
    expect(inline.runtime?.success).toBe(true);
  });

  it("derives generic action activity inline config instead of default fallback", () => {
    const node: MicroflowWorkflowNodeJSON = {
      id: "action-1",
      type: "activity",
      data: {
        objectId: "action-1",
        objectKind: "actionActivity",
        collectionId: "nodes",
        title: "普通动作",
        validationState: "valid",
        issueCount: 0,
        actionKind: "invokeAction",
        action: {
          kind: "invokeAction",
          inputExpression: { raw: "$valueA" },
          outputVariableName: "actionResult",
        },
      } as never,
      meta: { position: { x: 420, y: 0 } },
    };
    const inline = deriveNodeInlineConfig({ node, schema: buildSchema(node) });
    expect(inline.sections.some(section => section.kind === "inputs")).toBe(true);
    expect(inline.sections.some(section => section.kind === "outputs")).toBe(true);
    expect(inline.summaryLines.some(line => line.value.includes("in:"))).toBe(true);
    expect(inline.summaryLines.some(line => line.value.includes("out:"))).toBe(true);
  });

  it("projects validation quick-fix into inline runtime error suggestions", () => {
    const node: MicroflowWorkflowNodeJSON = {
      id: "decision",
      type: "decision",
      data: {
        objectId: "decision",
        objectKind: "exclusiveSplit",
        collectionId: "nodes",
        title: "判断",
        validationState: "warning",
        issueCount: 1,
      } as never,
      meta: { position: { x: 120, y: 80 } },
    };
    const inline = deriveNodeInlineConfig({
      node,
      schema: buildSchema(node),
      issues: [{
        id: "issue-1",
        code: "MF_DECISION_BOOLEAN_FALSE_MISSING",
        message: "Decision false branch missing",
        severity: "warning",
        source: "schema",
        objectId: "decision",
        quickFixes: [{
          kind: "createMissingFlow",
          label: "补全 false 分支",
          payload: { caseKind: "boolean", value: false },
        }],
      }] as never,
    });
    const fix = inline.runtime?.error?.fixSuggestions?.find(item => item.actionKind === "createMissingFlow");
    expect(fix?.value).toBe(false);
    expect(fix?.editType).toBe("branch");
  });

  it("adds unified output-mappings section for executable nodes without compatibility fallback", () => {
    const node: MicroflowWorkflowNodeJSON = {
      id: "action-2",
      type: "activity",
      data: {
        objectId: "action-2",
        objectKind: "actionActivity",
        collectionId: "nodes",
        title: "输出动作",
        validationState: "valid",
        issueCount: 0,
        actionKind: "invokeAction",
        action: {
          kind: "invokeAction",
          outputVariableName: "canonicalResult",
        },
      } as never,
      meta: { position: { x: 520, y: 80 } },
    };
    const inline = deriveNodeInlineConfig({ node, schema: buildSchema(node) });
    const mappingField = inline.sections
      .flatMap(section => section.fields)
      .find(field => field.fieldPath === "data.outputMappings");
    expect(mappingField?.editType).toBe("outputMappings");
    expect(mappingField?.value).toBe("[]");
    expect(inline.sections.some(section => section.id === "output-mappings")).toBe(true);
  });
});
