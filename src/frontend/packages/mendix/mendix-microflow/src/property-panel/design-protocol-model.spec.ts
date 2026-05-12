import { describe, expect, it } from "vitest";

import {
  createMicroflowDesignSchema,
  createMicroflowWorkflowEdge,
  createWorkflowNodeFromPanelItem,
} from "../flowgram/flowgram-native-schema";
import { defaultMicroflowNodePanelRegistry } from "../node-registry";
import type { MicroflowActionActivity, MicroflowDesignSchema, MicroflowSequenceFlow, MicroflowWorkflowEdgeJSON } from "../schema";
import {
  applyDesignDocumentSchema,
  applyDesignFlowPatch,
  applyDesignObjectPatch,
  buildDesignPropertyPanelModel,
  deleteDesignFlow,
  deleteDesignObject,
  duplicateDesignObject,
  setDesignParameterAsReturnValue,
} from "./design-protocol-model";

function designSchema(): MicroflowDesignSchema {
  const schema = createMicroflowDesignSchema({
    id: "mf-design",
    name: "MF_Design",
    moduleId: "module-a",
  });
  return {
    ...schema,
    workflow: {
      ...schema.workflow,
      edges: [
        createMicroflowWorkflowEdge({
          id: "flow-start-end",
          sourceNodeID: "start",
          targetNodeID: "end",
        }) as MicroflowWorkflowEdgeJSON,
      ],
    },
    editor: {
      ...schema.editor,
      selection: { objectId: "start", objectIds: ["start"], flowIds: [], mode: "single" },
      selectedObjectId: "start",
    },
  };
}

describe("design protocol property panel adapter", () => {
  it("builds property objects from FlowGram design workflow", () => {
    const model = buildDesignPropertyPanelModel(designSchema());

    expect(model.selectedObject?.id).toBe("start");
    expect(model.authoringSchema.objectCollection.objects.map(object => object.id)).toEqual(["start", "end"]);
    expect(model.authoringSchema.flows[0]).toMatchObject({
      id: "flow-start-end",
      originObjectId: "start",
      destinationObjectId: "end",
    });
    expect(model.authoringSchema.security.applyEntityAccess).toBe(true);
  });

  it("syncs document-level security, concurrency and exposure from authoring projection back to design schema", () => {
    const schema = designSchema();
    const model = buildDesignPropertyPanelModel(schema);
    const next = applyDesignDocumentSchema(schema, {
      ...model.authoringSchema,
      security: {
        ...model.authoringSchema.security,
        applyEntityAccess: false,
      },
      concurrency: {
        ...model.authoringSchema.concurrency,
        allowConcurrentExecution: false,
      },
      exposure: {
        ...model.authoringSchema.exposure,
        asMicroflowAction: {
          enabled: true,
          caption: "Process Order",
          category: "Sales",
        },
        url: {
          enabled: true,
          path: "/p/process-order",
        },
      },
    });

    expect(next.security?.applyEntityAccess).toBe(false);
    expect(next.concurrency?.allowConcurrentExecution).toBe(false);
    expect(next.exposure?.asMicroflowAction).toMatchObject({
      enabled: true,
      caption: "Process Order",
      category: "Sales",
    });
    expect(next.exposure?.url).toMatchObject({
      enabled: true,
      path: "/p/process-order",
    });
  });

  it("updates node properties without moving nodes or changing edges", () => {
    const schema = designSchema();
    const startPosition = schema.workflow.nodes[0].meta?.position;
    const edgeSignature = JSON.stringify(schema.workflow.edges);
    const model = buildDesignPropertyPanelModel(schema);
    const nextObject = { ...model.selectedObject!, caption: "Start Renamed", documentation: "doc" };

    const next = applyDesignObjectPatch(schema, "start", { object: nextObject });

    expect(next.workflow.nodes[0].meta?.position).toEqual(startPosition);
    expect(JSON.stringify(next.workflow.edges)).toBe(edgeSignature);
    expect(next.workflow.nodes[0].data).toMatchObject({
      title: "Start Renamed",
      documentation: "doc",
    });
    expect(Object.keys(next.workflow.nodes[0].data ?? {})).not.toContain("property" + "Object");
  });

  it("updates edge properties without moving nodes", () => {
    const schema = designSchema();
    const nodeSignature = JSON.stringify(schema.workflow.nodes.map(node => node.meta?.position));
    const model = buildDesignPropertyPanelModel({
      ...schema,
      editor: { ...schema.editor, selection: { flowId: "flow-start-end", objectIds: [], flowIds: ["flow-start-end"], mode: "single" }, selectedObjectId: undefined, selectedFlowId: "flow-start-end" },
    });
    const flow = { ...model.selectedFlow!, editor: { ...(model.selectedFlow as MicroflowSequenceFlow).editor, label: "Done", description: "flow doc", branchOrder: 2 } } as MicroflowSequenceFlow;

    const next = applyDesignFlowPatch(schema, "flow-start-end", flow);

    expect(JSON.stringify(next.workflow.nodes.map(node => node.meta?.position))).toBe(nodeSignature);
    expect(next.workflow.edges[0].data).toMatchObject({
      label: "Done",
      description: "flow doc",
      branchOrder: 2,
    });
    expect(Object.keys(next.workflow.edges[0].data ?? {})).not.toContain("property" + "Flow");
  });

  it("roundtrips error-handler flow variable exposure flags in design mode", () => {
    const schema = designSchema();
    const flowSchema = {
      ...schema,
      workflow: {
        ...schema.workflow,
        edges: [
          {
            ...schema.workflow.edges[0],
            data: {
              ...(schema.workflow.edges[0].data as Record<string, unknown>),
              edgeKind: "errorHandler",
              isErrorHandler: true,
              exposeLatestError: true,
              exposeLatestHttpResponse: false,
              exposeLatestSoapFault: false,
              targetErrorVariableName: "$latestError",
            },
          },
        ],
      },
      editor: {
        ...schema.editor,
        selection: { flowId: "flow-start-end", objectIds: [], flowIds: ["flow-start-end"], mode: "single" },
        selectedObjectId: undefined,
        selectedFlowId: "flow-start-end",
      },
    } as MicroflowDesignSchema;

    const model = buildDesignPropertyPanelModel(flowSchema);
    const flow = {
      ...(model.selectedFlow as MicroflowSequenceFlow),
      exposeLatestHttpResponse: true,
      exposeLatestSoapFault: true,
      targetErrorVariableName: "$lastTransportError",
    } as MicroflowSequenceFlow;

    const next = applyDesignFlowPatch(flowSchema, "flow-start-end", flow);
    const nextModel = buildDesignPropertyPanelModel(next);

    expect(next.workflow.edges[0]?.data).toMatchObject({
      exposeLatestError: true,
      exposeLatestHttpResponse: true,
      exposeLatestSoapFault: true,
      targetErrorVariableName: "$lastTransportError",
    });
    expect(nextModel.selectedFlow).toMatchObject({
      exposeLatestError: true,
      exposeLatestHttpResponse: true,
      exposeLatestSoapFault: true,
      targetErrorVariableName: "$lastTransportError",
    });
  });

  it("forces orthogonal routing when flow patch contains non-orthogonal line kinds", () => {
    const schema = designSchema();
    const model = buildDesignPropertyPanelModel({
      ...schema,
      editor: {
        ...schema.editor,
        selectedFlowId: "flow-start-end",
        selectedObjectId: undefined,
        selection: {
          flowId: "flow-start-end",
          objectIds: [],
          flowIds: ["flow-start-end"],
          mode: "single",
        },
      },
    });
    const patchedFlow = {
      ...model.selectedFlow!,
      line: {
        ...(model.selectedFlow?.line ?? {}),
        kind: "bezier",
      },
    } as MicroflowSequenceFlow;

    const next = applyDesignFlowPatch(schema, "flow-start-end", patchedFlow);

    expect(next.workflow.edges[0].data).toMatchObject({
      line: { kind: "orthogonal" },
    });
  });

  it("normalizes annotation flow line kind to orthogonal even when persisted data is non-orthogonal", () => {
    const schema = {
      ...designSchema(),
      workflow: {
        ...designSchema().workflow,
        edges: [
          {
            ...designSchema().workflow.edges[0],
            data: {
              ...(designSchema().workflow.edges[0].data as Record<string, unknown>),
              flowKind: "annotation",
              edgeKind: "annotation",
              line: {
                points: [],
                kind: "bezier",
                routing: { mode: "auto", bendPoints: [] },
                style: { strokeType: "solid", strokeWidth: 2, arrow: "target" },
              },
            },
          },
        ],
      },
      editor: {
        ...designSchema().editor,
        selectedFlowId: "flow-start-end",
        selection: {
          objectId: undefined,
          flowId: "flow-start-end",
          collectionId: undefined,
          objectIds: [],
          flowIds: ["flow-start-end"],
          mode: "single",
        },
      },
    } as MicroflowDesignSchema;

    const model = buildDesignPropertyPanelModel(schema);
    const selectedFlow = model.selectedFlow;

    expect(selectedFlow).toMatchObject({
      id: "flow-start-end",
      kind: "annotation",
      line: { kind: "orthogonal" },
    });
  });

  it("deletes flows only and keeps node positions stable", () => {
    const schema = designSchema();
    const nodeSignature = JSON.stringify(schema.workflow.nodes.map(node => node.meta?.position));

    const next = deleteDesignFlow(schema, "flow-start-end");

    expect(next.workflow.edges).toHaveLength(0);
    expect(JSON.stringify(next.workflow.nodes.map(node => node.meta?.position))).toBe(nodeSignature);
  });

  it("does not delete Start and removes related edges for other nodes", () => {
    const schema = designSchema();
    const withActivity = {
      ...schema,
      workflow: {
        ...schema.workflow,
        nodes: [
          ...schema.workflow.nodes,
          {
            ...schema.workflow.nodes[0],
            id: "activity-1",
            type: "actionActivity",
            data: {
              ...(schema.workflow.nodes[0].data ?? {}),
              objectId: "activity-1",
              objectKind: "actionActivity",
              title: "Activity",
            },
          },
        ],
        edges: [
          ...schema.workflow.edges,
          createMicroflowWorkflowEdge({ id: "flow-activity-end", sourceNodeID: "activity-1", targetNodeID: "end" }) as MicroflowWorkflowEdgeJSON,
        ],
      },
    } as MicroflowDesignSchema;

    expect(deleteDesignObject(withActivity, "start").workflow.nodes.some(node => node.id === "start")).toBe(true);
    const next = deleteDesignObject(withActivity, "activity-1");
    expect(next.workflow.nodes.some(node => node.id === "activity-1")).toBe(false);
    expect(next.workflow.edges.some(edge => edge.sourceNodeID === "activity-1" || edge.targetNodeID === "activity-1")).toBe(false);
  });

  it("preserves property panel action data for runtime compilation", () => {
    const schema = createMicroflowDesignSchema({ id: "mf-action", name: "MF_Action", moduleId: "module-a" });
    const actionNode = schema.workflow.nodes.find(node => node.id === "start")!;
    const actionSchema = {
      ...schema,
      workflow: {
        ...schema.workflow,
        nodes: [
          {
            ...actionNode,
            id: "log-1",
            type: "actionActivity",
            data: { objectId: "log-1", objectKind: "actionActivity", actionKind: "logMessage", title: "Log" },
          },
        ],
        edges: [],
      },
      editor: { ...schema.editor, selection: { objectId: "log-1", objectIds: ["log-1"], flowIds: [], mode: "single" }, selectedObjectId: "log-1" },
    } as MicroflowDesignSchema;
    const model = buildDesignPropertyPanelModel(actionSchema);
    const activity = model.selectedObject as MicroflowActionActivity;

    const next = applyDesignObjectPatch(actionSchema, "log-1", {
      object: { ...activity, caption: "Write Log", action: { ...activity.action, messageTemplate: "hello" } } as MicroflowActionActivity,
    });
    const nextModel = buildDesignPropertyPanelModel(next);

    expect((nextModel.selectedObject as MicroflowActionActivity).action).toMatchObject({ messageTemplate: "hello" });
  });

  it("roundtrips common background color and node-level error handling for non-action design nodes", () => {
    const schema = createMicroflowDesignSchema({ id: "mf-design-common-props", name: "MF_DesignCommonProps", moduleId: "module-a" });
    const startNode = schema.workflow.nodes.find(node => node.id === "start")!;
    const decisionSchema = {
      ...schema,
      workflow: {
        ...schema.workflow,
        nodes: [
          {
            ...startNode,
            id: "decision-1",
            type: "exclusiveSplit",
            data: {
              objectId: "decision-1",
              objectKind: "exclusiveSplit",
              title: "Decision",
              officialType: "Microflows$ExclusiveSplit",
              collectionId: "root-collection",
              documentation: "",
              disabled: false,
              backgroundColor: "default",
              errorHandlingType: "rollback",
              splitCondition: {
                kind: "expression",
                resultType: "boolean",
                expression: {
                  raw: "$flag = true",
                  references: { variables: ["flag"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
                  diagnostics: [],
                },
              },
            },
            meta: { nodeDTOType: "exclusiveSplit", collectionId: "root-collection", position: { x: 240, y: 160 }, size: { width: 44, height: 44 } },
          },
        ],
        edges: [],
      },
      editor: {
        ...schema.editor,
        selection: { objectId: "decision-1", objectIds: ["decision-1"], flowIds: [], mode: "single" },
        selectedObjectId: "decision-1",
      },
    } as MicroflowDesignSchema;

    const model = buildDesignPropertyPanelModel(decisionSchema);
    const nextDecision = {
      ...model.selectedObject!,
      backgroundColor: "purple",
      errorHandlingType: "customWithoutRollback",
      splitCondition: {
        kind: "expression",
        resultType: "boolean",
        expression: {
          raw: "$flag = false",
          references: { variables: ["flag"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
          diagnostics: [],
        },
      },
    } as typeof model.selectedObject;

    const next = applyDesignObjectPatch(decisionSchema, "decision-1", { object: nextDecision! });
    const nextNode = next.workflow.nodes.find(node => node.id === "decision-1");
    const nextModel = buildDesignPropertyPanelModel(next);

    expect(nextNode?.data).toMatchObject({
      backgroundColor: "purple",
      errorHandlingType: "customWithoutRollback",
      splitCondition: {
        expression: { raw: "$flag = false" },
      },
    });
    expect(nextModel.selectedObject).toMatchObject({
      backgroundColor: "purple",
      errorHandlingType: "customWithoutRollback",
      splitCondition: {
        expression: { raw: "$flag = false" },
      },
    });
  });

  it("roundtrips merge behavior changes for design merge nodes", () => {
    const schema = createMicroflowDesignSchema({ id: "mf-design-merge", name: "MF_DesignMerge", moduleId: "module-a" });
    const startNode = schema.workflow.nodes.find(node => node.id === "start")!;
    const mergeSchema = {
      ...schema,
      workflow: {
        ...schema.workflow,
        nodes: [
          {
            ...startNode,
            id: "merge-1",
            type: "exclusiveMerge",
            data: {
              objectId: "merge-1",
              objectKind: "exclusiveMerge",
              title: "Merge",
              officialType: "Microflows$ExclusiveMerge",
              collectionId: "root-collection",
              documentation: "",
              disabled: false,
              mergeBehavior: { strategy: "firstArrived" },
            },
            meta: { nodeDTOType: "exclusiveMerge", collectionId: "root-collection", position: { x: 320, y: 220 }, size: { width: 24, height: 24 } },
          },
        ],
        edges: [],
      },
      editor: {
        ...schema.editor,
        selection: { objectId: "merge-1", objectIds: ["merge-1"], flowIds: [], mode: "single" },
        selectedObjectId: "merge-1",
      },
    } as MicroflowDesignSchema;

    const model = buildDesignPropertyPanelModel(mergeSchema);
    const nextMerge = {
      ...model.selectedObject!,
      mergeBehavior: { strategy: "firstArrived" },
      documentation: "merge doc",
    } as typeof model.selectedObject;

    const next = applyDesignObjectPatch(mergeSchema, "merge-1", { object: nextMerge! });
    const nextModel = buildDesignPropertyPanelModel(next);

    expect(next.workflow.nodes.find(node => node.id === "merge-1")?.data).toMatchObject({
      mergeBehavior: { strategy: "firstArrived" },
      documentation: "merge doc",
    });
    expect(nextModel.selectedObject).toMatchObject({
      mergeBehavior: { strategy: "firstArrived" },
      documentation: "merge doc",
    });
  });

  it("hydrates and persists tryCatch branch keys in design mode", () => {
    const schema = createMicroflowDesignSchema({ id: "mf-design-trycatch", name: "MF_DesignTryCatch", moduleId: "module-a" });
    const startNode = schema.workflow.nodes.find(node => node.id === "start")!;
    const tryCatchSchema = {
      ...schema,
      workflow: {
        ...schema.workflow,
        nodes: [
          {
            ...startNode,
            id: "try-catch-1",
            type: "tryCatch",
            data: {
              objectId: "try-catch-1",
              objectKind: "tryCatch",
              title: "Try Catch",
              officialType: "Microflows$TryCatch",
              collectionId: "root-collection",
              documentation: "try doc",
              disabled: false,
              tryBranchKey: "try-main",
              catchBranchKey: "catch-main",
              finallyBranchKey: "finally-main",
              errorVariableName: "$caughtError",
            },
            meta: { nodeDTOType: "tryCatch", collectionId: "root-collection", position: { x: 320, y: 180 }, size: { width: 200, height: 86 } },
          },
        ],
        edges: [],
      },
      editor: {
        ...schema.editor,
        selection: { objectId: "try-catch-1", objectIds: ["try-catch-1"], flowIds: [], mode: "single" },
        selectedObjectId: "try-catch-1",
      },
    } as MicroflowDesignSchema;

    const model = buildDesignPropertyPanelModel(tryCatchSchema);
    expect(model.selectedObject).toMatchObject({
      tryBranchKey: "try-main",
      catchBranchKey: "catch-main",
      finallyBranchKey: "finally-main",
      errorVariableName: "$caughtError",
    });

    const nextObject = {
      ...model.selectedObject!,
      tryBranchKey: "try-updated",
      catchBranchKey: "catch-updated",
      finallyBranchKey: undefined,
      errorVariableName: "$updatedError",
    } as typeof model.selectedObject;

    const next = applyDesignObjectPatch(tryCatchSchema, "try-catch-1", { object: nextObject! });
    const nextModel = buildDesignPropertyPanelModel(next);

    expect(next.workflow.nodes.find(node => node.id === "try-catch-1")?.data).toMatchObject({
      tryBranchKey: "try-updated",
      catchBranchKey: "catch-updated",
      errorVariableName: "$updatedError",
    });
    expect(next.workflow.nodes.find(node => node.id === "try-catch-1")?.data).not.toHaveProperty("finallyBranchKey", "finally-main");
    expect(nextModel.selectedObject).toMatchObject({
      tryBranchKey: "try-updated",
      catchBranchKey: "catch-updated",
      errorVariableName: "$updatedError",
    });
    expect(nextModel.selectedObject).not.toHaveProperty("finallyBranchKey", "finally-main");
  });

  it("hydrates and persists errorHandler policy fields in design mode", () => {
    const schema = createMicroflowDesignSchema({ id: "mf-design-errorhandler", name: "MF_DesignErrorHandler", moduleId: "module-a" });
    const startNode = schema.workflow.nodes.find(node => node.id === "start")!;
    const errorHandlerSchema = {
      ...schema,
      workflow: {
        ...schema.workflow,
        nodes: [
          {
            ...startNode,
            id: "error-handler-1",
            type: "errorHandler",
            data: {
              objectId: "error-handler-1",
              objectKind: "errorHandler",
              title: "Error Handler",
              officialType: "Microflows$ErrorHandler",
              collectionId: "root-collection",
              documentation: "error doc",
              disabled: false,
              policy: "custom",
              customHandlerVariable: "capturedError",
              continueOnError: true,
            },
            meta: { nodeDTOType: "errorHandler", collectionId: "root-collection", position: { x: 420, y: 180 }, size: { width: 196, height: 80 } },
          },
        ],
        edges: [],
      },
      editor: {
        ...schema.editor,
        selection: { objectId: "error-handler-1", objectIds: ["error-handler-1"], flowIds: [], mode: "single" },
        selectedObjectId: "error-handler-1",
      },
    } as MicroflowDesignSchema;

    const model = buildDesignPropertyPanelModel(errorHandlerSchema);
    expect(model.selectedObject).toMatchObject({
      policy: "custom",
      customHandlerVariable: "capturedError",
      continueOnError: true,
    });

    const nextObject = {
      ...model.selectedObject!,
      policy: "continue",
      customHandlerVariable: undefined,
      continueOnError: false,
    } as typeof model.selectedObject;

    const next = applyDesignObjectPatch(errorHandlerSchema, "error-handler-1", { object: nextObject! });
    const nextModel = buildDesignPropertyPanelModel(next);

    expect(next.workflow.nodes.find(node => node.id === "error-handler-1")?.data).toMatchObject({
      policy: "continue",
      continueOnError: false,
    });
    expect(next.workflow.nodes.find(node => node.id === "error-handler-1")?.data).not.toHaveProperty("customHandlerVariable", "capturedError");
    expect(nextModel.selectedObject).toMatchObject({
      policy: "continue",
      continueOnError: false,
    });
    expect(nextModel.selectedObject).not.toHaveProperty("customHandlerVariable", "capturedError");
  });

  it("hydrates and persists inheritance split entity configuration in design mode", () => {
    const schema = createMicroflowDesignSchema({ id: "mf-design-inheritance", name: "MF_DesignInheritance", moduleId: "module-a" });
    const startNode = schema.workflow.nodes.find(node => node.id === "start")!;
    const inheritanceSchema = {
      ...schema,
      workflow: {
        ...schema.workflow,
        nodes: [
          {
            ...startNode,
            id: "inheritance-1",
            type: "inheritanceSplit",
            data: {
              objectId: "inheritance-1",
              objectKind: "inheritanceSplit",
              title: "Object Type Decision",
              officialType: "Microflows$InheritanceSplit",
              collectionId: "root-collection",
              documentation: "",
              disabled: false,
              errorHandlingType: "rollback",
              inputObjectVariableName: "$member",
              generalizedEntityQualifiedName: "University.Member",
              allowedSpecializations: ["University.Student", "University.Teacher"],
              entity: {
                generalizedEntityQualifiedName: "University.Member",
                allowedSpecializations: ["University.Student", "University.Teacher"],
              },
            },
            meta: { nodeDTOType: "inheritanceSplit", collectionId: "root-collection", position: { x: 360, y: 180 }, size: { width: 44, height: 44 } },
          },
        ],
        edges: [],
      },
      editor: {
        ...schema.editor,
        selection: { objectId: "inheritance-1", objectIds: ["inheritance-1"], flowIds: [], mode: "single" },
        selectedObjectId: "inheritance-1",
      },
    } as MicroflowDesignSchema;

    const model = buildDesignPropertyPanelModel(inheritanceSchema);
    expect(model.selectedObject).toMatchObject({
      inputObjectVariableName: "$member",
      generalizedEntityQualifiedName: "University.Member",
      allowedSpecializations: ["University.Student", "University.Teacher"],
    });

    const nextObject = {
      ...model.selectedObject!,
      inputObjectVariableName: "$employee",
      allowedSpecializations: ["University.Employee"],
      entity: {
        generalizedEntityQualifiedName: "University.Member",
        allowedSpecializations: ["University.Employee"],
      },
      errorHandlingType: "customWithRollback",
    } as typeof model.selectedObject;

    const next = applyDesignObjectPatch(inheritanceSchema, "inheritance-1", { object: nextObject! });
    const nextModel = buildDesignPropertyPanelModel(next);

    expect(next.workflow.nodes.find(node => node.id === "inheritance-1")?.data).toMatchObject({
      inputObjectVariableName: "$employee",
      allowedSpecializations: ["University.Employee"],
      entity: {
        generalizedEntityQualifiedName: "University.Member",
        allowedSpecializations: ["University.Employee"],
      },
      errorHandlingType: "customWithRollback",
    });
    expect(nextModel.selectedObject).toMatchObject({
      inputObjectVariableName: "$employee",
      allowedSpecializations: ["University.Employee"],
      errorHandlingType: "customWithRollback",
    });
  });

  it("creates native action nodes with complete action defaults", () => {
    const createVariable = defaultMicroflowNodePanelRegistry.find(item => item.actionKind === "createVariable")!;

    const node = createWorkflowNodeFromPanelItem(createVariable, { x: 120, y: 80 }, []);
    const portIds = node.meta?.defaultPorts?.map(port => port.portID);

    expect(node.type).toBe("actionActivity");
    expect(node.data).toMatchObject({
      objectKind: "actionActivity",
      actionKind: "createVariable",
      action: {
        id: `action-${node.id}`,
        kind: "createVariable",
        variableName: "newVariable",
      },
    });
    expect(portIds).toEqual(["in", "out"]);
    expect(portIds).not.toContain("error");
  });

  it("creates native action nodes with error ports only when the action supports them", () => {
    const retrieve = defaultMicroflowNodePanelRegistry.find(item => item.actionKind === "retrieve")!;
    const logMessage = defaultMicroflowNodePanelRegistry.find(item => item.actionKind === "logMessage")!;

    const retrieveNode = createWorkflowNodeFromPanelItem(retrieve, { x: 120, y: 80 }, []);
    const logNode = createWorkflowNodeFromPanelItem(logMessage, { x: 240, y: 80 }, [retrieveNode.id]);

    expect(retrieveNode.meta?.defaultPorts?.map(port => port.portID)).toEqual(["in", "out", "error"]);
    expect(logNode.meta?.defaultPorts?.map(port => port.portID)).toEqual(["in", "out"]);
  });

  it("hydrates malformed native action nodes instead of crashing property model", () => {
    const schema = createMicroflowDesignSchema({ id: "mf-malformed", name: "MF_Malformed", moduleId: "module-a" });
    const malformed = {
      ...schema.workflow.nodes[0],
      id: "malformed-action",
      type: "actionActivity",
      data: { objectId: "malformed-action", objectKind: "actionActivity", title: "Malformed Action" },
    };
    const actionSchema = {
      ...schema,
      workflow: { ...schema.workflow, nodes: [malformed], edges: [] },
      editor: { ...schema.editor, selection: { objectId: "malformed-action", objectIds: ["malformed-action"], flowIds: [], mode: "single" }, selectedObjectId: "malformed-action" },
    } as MicroflowDesignSchema;

    const model = buildDesignPropertyPanelModel(actionSchema);

    expect((model.selectedObject as MicroflowActionActivity).action).toMatchObject({
      id: "action-malformed-action",
      kind: "logMessage",
    });
  });

  it("syncs schema-level parameter renames back into parameter nodes", () => {
    const schema = createMicroflowDesignSchema({
      id: "mf-parameter-sync",
      name: "MF_ParameterSync",
      moduleId: "module-a",
      parameters: [{ id: "param-amount", stableId: "param-amount", name: "parameter", dataType: { kind: "string" }, type: { kind: "primitive", name: "string" }, required: true }],
      workflow: {
        nodes: [
          {
            id: "parameter-node",
            type: "parameterObject",
            data: {
              objectId: "parameter-node",
              objectKind: "parameterObject",
              title: "parameter",
              parameterId: "param-amount",
              parameterName: "parameter",
            },
            meta: { position: { x: 120, y: 80 } },
          },
        ],
        edges: [],
      } as never,
    });

    const next = applyDesignDocumentSchema(schema, {
      ...buildDesignPropertyPanelModel(schema).authoringSchema,
      parameters: [{ id: "param-amount", stableId: "param-amount", name: "amount", dataType: { kind: "string" }, type: { kind: "primitive", name: "string" }, required: true }],
    });

    expect(next.parameters[0]?.name).toBe("amount");
    expect(next.workflow.nodes[0]?.data).toMatchObject({
      title: "amount",
      parameterName: "amount",
    });
  });

  it("hydrates end event returnValue from design node data into authoring model", () => {
    const schema = createMicroflowDesignSchema({
      id: "mf-end-return-hydrate",
      name: "MF_EndReturnHydrate",
      moduleId: "module-a",
      returnType: { kind: "string" },
      workflow: {
        nodes: [
          {
            id: "end-node",
            type: "endEvent",
            data: {
              objectId: "end-node",
              objectKind: "endEvent",
              title: "End",
              returnValue: {
                raw: "$amount",
                inferredType: { kind: "string" },
                references: { variables: ["$amount"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
                diagnostics: [],
              },
            },
            meta: { position: { x: 240, y: 120 } },
          },
        ],
        edges: [],
      } as never,
    });

    const model = buildDesignPropertyPanelModel(schema);
    const endNode = model.authoringSchema.objectCollection.objects.find(object => object.id === "end-node");

    expect(endNode?.kind === "endEvent" ? endNode.returnValue?.raw : undefined).toBe("$amount");
  });

  it("deletes parameter node with stale schema parameter cleanup and reflow", () => {
    const schema = createMicroflowDesignSchema({
      id: "mf-parameter-delete-reflow",
      name: "MF_ParameterDeleteReflow",
      moduleId: "module-a",
      parameters: [
        { id: "param-a", stableId: "param-a", name: "A", dataType: { kind: "string" }, type: { kind: "primitive", name: "string" }, required: true },
        { id: "param-b", stableId: "param-b", name: "B", dataType: { kind: "string" }, type: { kind: "primitive", name: "string" }, required: true },
      ],
      workflow: {
        nodes: [
          {
            id: "start",
            type: "startEvent",
            data: { objectId: "start", objectKind: "startEvent", title: "Start" },
            meta: { position: { x: 400, y: 240 } },
          },
          {
            id: "param-node-a",
            type: "parameterObject",
            data: { objectId: "param-node-a", objectKind: "parameterObject", title: "A", parameterId: "param-a", parameterName: "A" },
            meta: { position: { x: 320, y: 120 } },
          },
          {
            id: "param-node-b",
            type: "parameterObject",
            data: { objectId: "param-node-b", objectKind: "parameterObject", title: "B", parameterId: "param-b", parameterName: "B" },
            meta: { position: { x: 520, y: 120 } },
          },
        ],
        edges: [],
      } as never,
    });

    const next = deleteDesignObject(schema, "param-node-b");

    expect(next.parameters.map(parameter => parameter.id)).toEqual(["param-a"]);
    const remaining = next.workflow.nodes.find(node => node.id === "param-node-a");
    expect(remaining?.meta?.position).toEqual({ x: 400, y: 144 });
  });

  it("duplicates parameter node and keeps root parameter row aligned", () => {
    const schema = createMicroflowDesignSchema({
      id: "mf-parameter-duplicate-reflow",
      name: "MF_ParameterDuplicateReflow",
      moduleId: "module-a",
      parameters: [
        { id: "param-a", stableId: "param-a", name: "A", dataType: { kind: "string" }, type: { kind: "primitive", name: "string" }, required: true },
        { id: "param-b", stableId: "param-b", name: "B", dataType: { kind: "string" }, type: { kind: "primitive", name: "string" }, required: true },
      ],
      workflow: {
        nodes: [
          {
            id: "start",
            type: "startEvent",
            data: { objectId: "start", objectKind: "startEvent", title: "Start" },
            meta: { position: { x: 400, y: 240 } },
          },
          {
            id: "param-node-a",
            type: "parameterObject",
            data: { objectId: "param-node-a", objectKind: "parameterObject", title: "A", parameterId: "param-a", parameterName: "A" },
            meta: { position: { x: 320, y: 120 } },
          },
          {
            id: "param-node-b",
            type: "parameterObject",
            data: { objectId: "param-node-b", objectKind: "parameterObject", title: "B", parameterId: "param-b", parameterName: "B" },
            meta: { position: { x: 520, y: 120 } },
          },
        ],
        edges: [],
      } as never,
    });

    const next = duplicateDesignObject(schema, "param-node-a");

    const parameterNodes = next.workflow.nodes
      .filter(node => node.type === "parameterObject")
      .map(node => Number(node.meta?.position?.x ?? 0))
      .sort((a, b) => a - b);
    expect(parameterNodes).toEqual([280, 400, 520]);
  });

  it("sets parameter as return value and syncs end node data back to design schema", () => {
    const schema = createMicroflowDesignSchema({
      id: "mf-parameter-return",
      name: "MF_ParameterReturn",
      moduleId: "module-a",
      parameters: [{ id: "param-amount", stableId: "param-amount", name: "amount", dataType: { kind: "decimal" }, type: { kind: "primitive", name: "decimal" }, required: true }],
      workflow: {
        nodes: [
          {
            id: "parameter-node",
            type: "parameterObject",
            data: {
              objectId: "parameter-node",
              objectKind: "parameterObject",
              title: "amount",
              parameterId: "param-amount",
              parameterName: "amount",
            },
            meta: { position: { x: 120, y: 80 } },
          },
          {
            id: "end-node",
            type: "endEvent",
            data: {
              objectId: "end-node",
              objectKind: "endEvent",
              title: "End",
            },
            meta: { position: { x: 320, y: 80 } },
          },
        ],
        edges: [],
      } as never,
    });

    const next = setDesignParameterAsReturnValue(schema, "param-amount");
    const endNode = next.workflow.nodes.find(node => node.id === "end-node");
    const endData = endNode?.data as { returnValue?: { raw?: string } } | undefined;

    expect(next.returnType).toEqual({ kind: "decimal" });
    expect(next.returnVariableName).toBe("amount");
    expect(endData?.returnValue?.raw).toBe("$amount");
  });
});

