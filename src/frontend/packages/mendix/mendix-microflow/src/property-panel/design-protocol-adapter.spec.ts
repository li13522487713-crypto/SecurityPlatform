import { describe, expect, it } from "vitest";

import {
  createMicroflowDesignSchema,
  createMicroflowWorkflowEdge,
  createWorkflowNodeFromPanelItem,
} from "../flowgram/flowgram-native-schema";
import { defaultMicroflowNodePanelRegistry } from "../node-registry";
import type { MicroflowActionActivity, MicroflowDesignSchema, MicroflowSequenceFlow, MicroflowWorkflowEdgeJSON } from "../schema";
import {
  applyDesignFlowPatch,
  applyDesignObjectPatch,
  buildDesignPropertyPanelModel,
  deleteDesignFlow,
  deleteDesignObject,
} from "./design-protocol-adapter";

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
});
