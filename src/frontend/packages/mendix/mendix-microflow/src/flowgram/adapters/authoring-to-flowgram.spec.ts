import { describe, expect, it } from "vitest";
import type { MicroflowAuthoringSchema, MicroflowObject } from "../../schema";
import { sampleMicroflowSchema } from "../../schema/sample";
import { authoringToFlowGram } from "./authoring-to-flowgram";

describe("authoringToFlowGram background color projection", () => {
  it("projects action activity backgroundColor into flowgram node data", () => {
    const schema = structuredClone(sampleMicroflowSchema) as MicroflowAuthoringSchema;
    const object = schema.objectCollection.objects.find(
      item => item.id === "change-order",
    ) as MicroflowObject | undefined;
    if (!object || object.kind !== "actionActivity") {
      throw new Error("Expected sample action activity change-order.");
    }
    object.backgroundColor = "yellow";

    const workflow = authoringToFlowGram(schema);
    const node = workflow.nodes.find(item => {
      const data = item.data as { objectId?: string; backgroundColor?: string } | undefined;
      return data?.objectId === "change-order";
    });

    expect((node?.data as { backgroundColor?: string } | undefined)?.backgroundColor).toBe("yellow");
  });

  it("does not attach backgroundColor for non-action nodes", () => {
    const workflow = authoringToFlowGram(sampleMicroflowSchema);
    const startNode = workflow.nodes.find(item => {
      const data = item.data as { objectId?: string } | undefined;
      return data?.objectId === "start";
    });
    expect((startNode?.data as { backgroundColor?: string } | undefined)?.backgroundColor).toBeUndefined();
  });

  it("projects parameter type metadata for parameter nodes", () => {
    const workflow = authoringToFlowGram(sampleMicroflowSchema);
    const parameterNode = workflow.nodes.find(item => {
      const data = item.data as { objectId?: string } | undefined;
      return data?.objectId === "param-member";
    });
    const data = parameterNode?.data as { parameterKind?: string; parameterTypeLabel?: string; title?: string } | undefined;
    expect(data?.title).toBe("member");
    expect(data?.parameterKind).toBe("object");
    expect(data?.parameterTypeLabel).toBe("Member");
  });

  it("marks list parameters with list kind and readable type", () => {
    const schema = structuredClone(sampleMicroflowSchema) as MicroflowAuthoringSchema;
    const parameter = schema.parameters.find(item => item.id === "param-member");
    if (!parameter) {
      throw new Error("Expected sample parameter param-member.");
    }
    parameter.dataType = { kind: "list", itemType: { kind: "object", entityQualifiedName: "University.Member" } };
    const workflow = authoringToFlowGram(schema);
    const parameterNode = workflow.nodes.find(item => {
      const data = item.data as { objectId?: string } | undefined;
      return data?.objectId === "param-member";
    });
    const data = parameterNode?.data as { parameterKind?: string; parameterTypeLabel?: string } | undefined;
    expect(data?.parameterKind).toBe("list");
    expect(data?.parameterTypeLabel).toBe("List of Member");
  });
});
