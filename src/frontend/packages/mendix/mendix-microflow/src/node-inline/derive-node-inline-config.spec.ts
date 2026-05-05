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

describe("deriveNodeInlineConfig", () => {
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
    expect(inline.summaryLines[0]?.value).toContain("$riskScore >= 80");
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
        returnExpression: { raw: "$approvalResult" },
      } as never,
      meta: { position: { x: 200, y: 0 } },
    };
    const startInline = deriveNodeInlineConfig({ node: startNode, schema: buildSchema(startNode) });
    const endInline = deriveNodeInlineConfig({ node: endNode, schema: buildSchema(endNode) });
    expect(startInline.summaryLines.length).toBeGreaterThan(0);
    expect(endInline.summaryLines.some(line => line.value.includes("$approvalResult"))).toBe(true);
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
});
