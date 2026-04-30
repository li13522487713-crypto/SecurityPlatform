import { describe, expect, it } from "vitest";

import {
  createMicroflowDesignSchema,
  createMicroflowWorkflowEdge,
} from "../flowgram/flowgram-native-schema";
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
      propertyObject: expect.objectContaining({ caption: "Start Renamed" }),
    });
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
      propertyFlow: expect.objectContaining({ id: "flow-start-end" }),
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
});
