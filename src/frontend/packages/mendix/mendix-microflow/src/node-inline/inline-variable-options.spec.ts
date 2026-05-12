import { describe, expect, it } from "vitest";
import type { MicroflowDesignSchema } from "../schema/types";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";

function schema(linked = true): MicroflowDesignSchema {
  return {
    schemaVersion: "1",
    mendixProfile: "mx10",
    id: "mf-inline-options",
    stableId: "mf-inline-options",
    name: "InlineOptions",
    displayName: "InlineOptions",
    moduleId: "module",
    parameters: [{ id: "p1", name: "incidentId", dataType: { kind: "string" }, required: true }],
    returnType: { kind: "void" },
    variables: [{ id: "v1", name: "globalFlag", type: { kind: "boolean" }, scope: "microflow", readonly: false }],
    workflow: {
      nodes: [
        {
          id: "node-a",
          type: "action.createVariable",
          data: {
            title: "CreateRisk",
            action: {
              kind: "createVariable",
              variableName: "riskScore",
            },
          },
        },
        {
          id: "node-b",
          type: "gateway.exclusiveSplit",
          data: {
            title: "Decision",
            condition: { raw: "if $.riskScore > 80 and $incidentId != \"\" then true else false" },
          },
        },
      ],
      edges: linked
        ? [{ id: "e-a-b", sourceNodeID: "node-a", targetNodeID: "node-b" }]
        : [],
    },
  } as unknown as MicroflowDesignSchema;
}

describe("buildNodeInlineVariableOptions", () => {
  it("only exposes upstream variable when link exists", () => {
    const linked = schema(true);
    const unlinked = schema(false);
    const linkedOptions = buildNodeInlineVariableOptions({ schema: linked, node: linked.workflow.nodes[1], mode: "expression" });
    const unlinkedOptions = buildNodeInlineVariableOptions({ schema: unlinked, node: unlinked.workflow.nodes[1], mode: "expression" });
    expect(linkedOptions.some(item => item.value === "$riskScore")).toBe(true);
    expect(unlinkedOptions.some(item => item.value === "$riskScore")).toBe(false);
  });

  it("provides both $var and $.var options", () => {
    const s = schema(true);
    const options = buildNodeInlineVariableOptions({ schema: s, node: s.workflow.nodes[1], mode: "expression" });
    expect(options.some(item => item.value === "$incidentId")).toBe(true);
    expect(options.some(item => item.value === "$.incidentId")).toBe(true);
  });

  it("normalizes a derived variable index before building inline options", () => {
    const s = schema(true);
    s.variables = {
      parameters: {},
      localVariables: {
        approvalLevel: {
          name: "approvalLevel",
          dataType: { kind: "integer" },
          source: { kind: "localVariable", variableId: "approvalLevel" },
          scope: { collectionId: "root" },
          readonly: false,
        },
      },
      objectOutputs: {},
      listOutputs: {},
      loopVariables: {},
      errorVariables: {},
      systemVariables: {},
    } as never;

    const options = buildNodeInlineVariableOptions({ schema: s, node: s.workflow.nodes[1], mode: "expression" });

    expect(options.some(item => item.value === "$approvalLevel")).toBe(true);
  });

  it("parses reference counts from $.dot.path expressions", () => {
    const s = schema(true);
    (s.workflow.nodes[1] as { data: Record<string, unknown> }).data.condition = {
      raw: "$.riskScore.value > 80 && $.incidentId != \"\"",
    };
    const options = buildNodeInlineVariableOptions({ schema: s, node: s.workflow.nodes[1], mode: "expression" });
    const riskScoreLabel = options.find(item => item.value === "$riskScore")?.label ?? "";
    expect(riskScoreLabel.includes("refCount=")).toBe(true);
  });

  it("exposes upstream outputs for rest/callMicroflow/createVariable/changeVariable only when linked", () => {
    const makeSchema = (linked: boolean): MicroflowDesignSchema => ({
      schemaVersion: "1",
      mendixProfile: "mx10",
      id: "mf-inline-options-all-kinds",
      stableId: "mf-inline-options-all-kinds",
      name: "InlineOptionsAllKinds",
      displayName: "InlineOptionsAllKinds",
      moduleId: "module",
      parameters: [],
      returnType: { kind: "void" },
      variables: [],
      workflow: {
        nodes: [
          {
            id: "rest-1",
            type: "activity",
            data: {
              title: "REST",
              action: {
                kind: "restCall",
                request: { method: "GET", urlExpression: { raw: "/api/orders" } },
                response: {
                  handling: { kind: "storeToVariable", outputVariableName: "response" },
                  statusCodeVariableName: "statusCode",
                  headersVariableName: "headers",
                },
              },
            },
          },
          {
            id: "call-1",
            type: "activity",
            data: {
              title: "CallMF",
              action: {
                kind: "callMicroflow",
                calledMicroflowId: "mf-a",
                returnValue: { outputVariableName: "riskScore" },
              },
            },
          },
          {
            id: "create-1",
            type: "activity",
            data: {
              title: "CreateVar",
              action: {
                kind: "createVariable",
                variableName: "createdVar",
              },
            },
          },
          {
            id: "change-1",
            type: "activity",
            data: {
              title: "ChangeVar",
              action: {
                kind: "changeVariable",
                targetVariableName: "changedVar",
              },
            },
          },
          {
            id: "target",
            type: "gateway.exclusiveSplit",
            data: {
              title: "TargetDecision",
            },
          },
        ],
        edges: linked
          ? [
            { id: "e1", sourceNodeID: "rest-1", targetNodeID: "target" },
            { id: "e2", sourceNodeID: "call-1", targetNodeID: "target" },
            { id: "e3", sourceNodeID: "create-1", targetNodeID: "target" },
            { id: "e4", sourceNodeID: "change-1", targetNodeID: "target" },
          ]
          : [],
      },
    }) as unknown as MicroflowDesignSchema;

    const linked = buildNodeInlineVariableOptions({
      schema: makeSchema(true),
      node: makeSchema(true).workflow.nodes.find(node => node.id === "target")!,
      mode: "expression",
    });
    const unlinked = buildNodeInlineVariableOptions({
      schema: makeSchema(false),
      node: makeSchema(false).workflow.nodes.find(node => node.id === "target")!,
      mode: "expression",
    });

    for (const token of ["$response", "$statusCode", "$headers", "$riskScore", "$createdVar", "$changedVar"]) {
      expect(linked.some(item => item.value === token)).toBe(true);
      expect(unlinked.some(item => item.value === token)).toBe(false);
    }
  });

  it("exposes upstream outputs for generic alias fields only when linked", () => {
    const makeSchema = (linked: boolean): MicroflowDesignSchema => ({
      schemaVersion: "1",
      mendixProfile: "mx10",
      id: "mf-inline-options-generic-aliases",
      stableId: "mf-inline-options-generic-aliases",
      name: "InlineOptionsGenericAliases",
      displayName: "InlineOptionsGenericAliases",
      moduleId: "module",
      parameters: [],
      returnType: { kind: "void" },
      variables: [],
      workflow: {
        nodes: [
          {
            id: "generate-doc",
            type: "activity",
            data: {
              title: "GenerateDoc",
              action: {
                kind: "generateDocument",
                outputFileDocumentVariableName: "invoiceDoc",
              },
            },
          },
          {
            id: "call-workflow",
            type: "activity",
            data: {
              title: "CallWorkflow",
              action: {
                kind: "callWorkflow",
                outputWorkflowVariableName: "workflowInstance",
              },
            },
          },
          {
            id: "target",
            type: "activity",
            data: { title: "TargetAction" },
          },
        ],
        edges: linked
          ? [
            { id: "e1", sourceNodeID: "generate-doc", targetNodeID: "target" },
            { id: "e2", sourceNodeID: "call-workflow", targetNodeID: "target" },
          ]
          : [],
      },
    }) as unknown as MicroflowDesignSchema;

    const linkedSchema = makeSchema(true);
    const unlinkedSchema = makeSchema(false);
    const linked = buildNodeInlineVariableOptions({
      schema: linkedSchema,
      node: linkedSchema.workflow.nodes.find(node => node.id === "target")!,
      mode: "expression",
    });
    const unlinked = buildNodeInlineVariableOptions({
      schema: unlinkedSchema,
      node: unlinkedSchema.workflow.nodes.find(node => node.id === "target")!,
      mode: "expression",
    });

    for (const token of ["$invoiceDoc", "$workflowInstance"]) {
      expect(linked.some(item => item.value === token)).toBe(true);
      expect(unlinked.some(item => item.value === token)).toBe(false);
    }
  });

  it("exposes data-level output variables only when upstream node is linked", () => {
    const makeSchema = (linked: boolean): MicroflowDesignSchema => ({
      schemaVersion: "1",
      mendixProfile: "mx10",
      id: "mf-inline-options-fallback",
      stableId: "mf-inline-options-fallback",
      name: "InlineOptionsFallback",
      displayName: "InlineOptionsFallback",
      moduleId: "module",
      parameters: [],
      returnType: { kind: "void" },
      variables: [],
      workflow: {
        nodes: [
          {
            id: "approval-1",
            type: "activity",
            data: {
              title: "Approval",
              approver: "$manager",
              resultVariable: "approvalResult",
            },
          },
          {
            id: "error-1",
            type: "activity",
            data: {
              title: "ErrorHandler",
              customHandlerVariable: "capturedError",
            },
          },
          {
            id: "target",
            type: "activity",
            data: { title: "TargetAction" },
          },
        ],
        edges: linked
          ? [
            { id: "e1", sourceNodeID: "approval-1", targetNodeID: "target" },
            { id: "e2", sourceNodeID: "error-1", targetNodeID: "target" },
          ]
          : [],
      },
    }) as unknown as MicroflowDesignSchema;

    const linkedSchema = makeSchema(true);
    const unlinkedSchema = makeSchema(false);
    const linked = buildNodeInlineVariableOptions({
      schema: linkedSchema,
      node: linkedSchema.workflow.nodes.find(node => node.id === "target")!,
      mode: "expression",
    });
    const unlinked = buildNodeInlineVariableOptions({
      schema: unlinkedSchema,
      node: unlinkedSchema.workflow.nodes.find(node => node.id === "target")!,
      mode: "expression",
    });

    for (const token of ["$approvalResult", "$capturedError"]) {
      expect(linked.some(item => item.value === token)).toBe(true);
      expect(unlinked.some(item => item.value === token)).toBe(false);
    }
  });

  it("keeps upstream provenance when name collides with context variable", () => {
    const s = schema(true);
    s.variables.push({
      id: "v-collision",
      name: "riskScore",
      type: { kind: "integer" },
      scope: "microflow",
      readonly: false,
    } as never);
    const options = buildNodeInlineVariableOptions({ schema: s, node: s.workflow.nodes[1], mode: "expression" });
    const riskScoreLabels = options
      .filter(item => item.value === "$riskScore")
      .map(item => item.label);
    expect(riskScoreLabels.some(label => label.includes("upstream-direct::") || label.includes("upstream-indirect::"))).toBe(true);
  });

  it("distinguishes direct and indirect upstream variables by source label", () => {
    const s = {
      schemaVersion: "1",
      mendixProfile: "mx10",
      id: "mf-inline-options-depth",
      stableId: "mf-inline-options-depth",
      name: "InlineOptionsDepth",
      displayName: "InlineOptionsDepth",
      moduleId: "module",
      parameters: [],
      returnType: { kind: "void" },
      variables: [],
      workflow: {
        nodes: [
          { id: "n1", type: "activity", data: { title: "N1", action: { kind: "createVariable", variableName: "fromN1" } } },
          { id: "n2", type: "activity", data: { title: "N2", action: { kind: "createVariable", variableName: "fromN2" } } },
          { id: "n3", type: "activity", data: { title: "N3", action: { kind: "createVariable", variableName: "fromN3" } } },
        ],
        edges: [
          { id: "e1", sourceNodeID: "n1", targetNodeID: "n2" },
          { id: "e2", sourceNodeID: "n2", targetNodeID: "n3" },
        ],
      },
    } as unknown as MicroflowDesignSchema;
    const options = buildNodeInlineVariableOptions({
      schema: s,
      node: s.workflow.nodes.find(node => node.id === "n3")!,
      mode: "expression",
    });
    const directLabel = options.find(item => item.value === "$fromN2")?.label ?? "";
    const indirectLabel = options.find(item => item.value === "$fromN1")?.label ?? "";
    expect(directLabel.includes("upstream-direct::")).toBe(true);
    expect(indirectLabel.includes("upstream-indirect::")).toBe(true);
  });

  it("exposes $currentIndex for while loop nodes", () => {
    const s = {
      schemaVersion: "1",
      mendixProfile: "mx10",
      id: "mf-inline-options-while",
      stableId: "mf-inline-options-while",
      name: "InlineOptionsWhile",
      displayName: "InlineOptionsWhile",
      moduleId: "module",
      parameters: [],
      returnType: { kind: "void" },
      variables: [],
      workflow: {
        nodes: [
          {
            id: "while-1",
            type: "loop",
            data: {
              title: "WhileLoop",
              loopSource: {
                kind: "whileCondition",
                expression: { raw: "$retryCount < 5" },
              },
              currentIndexVariableName: "$currentIndex",
            },
          },
        ],
        edges: [],
      },
    } as unknown as MicroflowDesignSchema;

    const options = buildNodeInlineVariableOptions({
      schema: s,
      node: s.workflow.nodes[0],
      mode: "expression",
    });

    expect(options.some(item => item.value === "$currentIndex")).toBe(true);
  });
});
