import { describe, expect, it } from "vitest";
import type { MicroflowAuthoringSchema, MicroflowObject } from "../../schema";
import { sampleMicroflowSchema } from "../../schema/sample";
import { authoringToFlowGram } from "./authoring-to-flowgram";

describe("authoringToFlowGram background color projection", () => {
  function activitySubtitleFor(action: Record<string, unknown>) {
    const schema = structuredClone(sampleMicroflowSchema) as MicroflowAuthoringSchema;
    const object = schema.objectCollection.objects.find(
      item => item.id === "change-order",
    ) as MicroflowObject | undefined;
    if (!object || object.kind !== "actionActivity") {
      throw new Error("Expected sample action activity change-order.");
    }
    object.caption = String(action.caption ?? object.caption ?? "Action");
    object.action = {
      ...object.action,
      ...action,
      caption: String(action.caption ?? object.action.caption ?? object.caption ?? "Action"),
      documentation: typeof action.documentation === "string" ? action.documentation : object.action.documentation,
      editor: {
        ...object.action.editor,
        ...(typeof action.editor === "object" && action.editor ? action.editor as Record<string, unknown> : {}),
      },
    } as never;

    const workflow = authoringToFlowGram(schema);
    const node = workflow.nodes.find(item => {
      const data = item.data as { objectId?: string; subtitle?: string } | undefined;
      return data?.objectId === "change-order";
    });
    return (node?.data as { subtitle?: string } | undefined)?.subtitle;
  }

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

  it("projects errorHandlingType for decision nodes", () => {
    const schema = structuredClone(sampleMicroflowSchema) as MicroflowAuthoringSchema;
    const object = schema.objectCollection.objects.find(
      item => item.id === "decision-processable",
    ) as MicroflowObject | undefined;
    if (!object || object.kind !== "exclusiveSplit") {
      throw new Error("Expected sample exclusive split decision-processable.");
    }
    object.errorHandlingType = "continue";

    const workflow = authoringToFlowGram(schema);
    const node = workflow.nodes.find(item => {
      const data = item.data as { objectId?: string; errorHandlingType?: string } | undefined;
      return data?.objectId === "decision-processable";
    });

    expect((node?.data as { errorHandlingType?: string } | undefined)?.errorHandlingType).toBe("continue");
  });

  it("projects backgroundColor for decision nodes", () => {
    const schema = structuredClone(sampleMicroflowSchema) as MicroflowAuthoringSchema;
    const object = schema.objectCollection.objects.find(
      item => item.id === "decision-processable",
    ) as MicroflowObject | undefined;
    if (!object || object.kind !== "exclusiveSplit") {
      throw new Error("Expected sample exclusive split decision-processable.");
    }
    object.backgroundColor = "purple";

    const workflow = authoringToFlowGram(schema);
    const node = workflow.nodes.find(item => {
      const data = item.data as { objectId?: string; backgroundColor?: string } | undefined;
      return data?.objectId === "decision-processable";
    });

    expect((node?.data as { backgroundColor?: string } | undefined)?.backgroundColor).toBe("purple");
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

  it("projects end event returnValue into flowgram node data", () => {
    const schema = structuredClone(sampleMicroflowSchema) as MicroflowAuthoringSchema;
    const end = schema.objectCollection.objects.find(
      item => item.id === "end-success",
    ) as MicroflowObject | undefined;
    if (!end || end.kind !== "endEvent") {
      throw new Error("Expected sample end event end-success.");
    }
    end.returnValue = {
      raw: "$member",
      inferredType: { kind: "object", entityQualifiedName: "University.Member" },
      references: { variables: ["$member"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
      diagnostics: [],
    };

    const workflow = authoringToFlowGram(schema);
    const node = workflow.nodes.find(item => {
      const data = item.data as { objectId?: string; returnValue?: { raw?: string } } | undefined;
      return data?.objectId === "end-success";
    });

    expect((node?.data as { returnValue?: { raw?: string } } | undefined)?.returnValue?.raw).toBe("$member");
  });

  it("projects errorHandler policy and custom variable into flowgram node data", () => {
    const schema = structuredClone(sampleMicroflowSchema) as MicroflowAuthoringSchema;
    schema.objectCollection.objects.push({
      id: "error-handler-inline",
      stableId: "error-handler-inline",
      kind: "errorHandler",
      officialType: "Microflows$ErrorHandler",
      caption: "Error Handler",
      documentation: "",
      relativeMiddlePoint: { x: 0, y: 0 },
      size: { width: 160, height: 80 },
      ports: [],
      autoGenerateCaption: false,
      backgroundColor: "default",
      editor: { iconKey: "errorHandler" },
      policy: "custom",
      customHandlerVariable: "capturedError",
      continueOnError: true,
    } as MicroflowObject);

    const workflow = authoringToFlowGram(schema);
    const node = workflow.nodes.find(item => {
      const data = item.data as { objectId?: string; policy?: string; customHandlerVariable?: string; continueOnError?: boolean } | undefined;
      return data?.objectId === "error-handler-inline";
    });

    expect((node?.data as { policy?: string } | undefined)?.policy).toBe("custom");
    expect((node?.data as { customHandlerVariable?: string } | undefined)?.customHandlerVariable).toBe("capturedError");
    expect((node?.data as { continueOnError?: boolean } | undefined)?.continueOnError).toBe(true);
  });

  it("projects tryCatch branch keys into flowgram node data", () => {
    const schema = structuredClone(sampleMicroflowSchema) as MicroflowAuthoringSchema;
    schema.objectCollection.objects.push({
      id: "try-catch-inline",
      stableId: "try-catch-inline",
      kind: "tryCatch",
      officialType: "Microflows$TryCatch",
      caption: "Try Catch",
      documentation: "",
      relativeMiddlePoint: { x: 0, y: 0 },
      size: { width: 200, height: 86 },
      ports: [],
      editor: { iconKey: "tryCatch" },
      tryBranchKey: "try-main",
      catchBranchKey: "catch-main",
      finallyBranchKey: "finally-main",
      errorVariableName: "$latestError",
    } as MicroflowObject);

    const workflow = authoringToFlowGram(schema);
    const node = workflow.nodes.find(item => {
      const data = item.data as {
        objectId?: string;
        tryBranchKey?: string;
        catchBranchKey?: string;
        finallyBranchKey?: string;
        errorVariableName?: string;
      } | undefined;
      return data?.objectId === "try-catch-inline";
    });

    expect((node?.data as { tryBranchKey?: string } | undefined)?.tryBranchKey).toBe("try-main");
    expect((node?.data as { catchBranchKey?: string } | undefined)?.catchBranchKey).toBe("catch-main");
    expect((node?.data as { finallyBranchKey?: string } | undefined)?.finallyBranchKey).toBe("finally-main");
    expect((node?.data as { errorVariableName?: string } | undefined)?.errorVariableName).toBe("$latestError");
  });

  it("includes createList outputs in action subtitles", () => {
    const subtitle = activitySubtitleFor({
      kind: "createList",
      officialType: "Microflows$CreateListAction",
      outputListVariableName: "orderList",
      listVariableName: "orderList",
    });

    expect(subtitle).toContain("out: orderList");
  });

  it("includes generic alias outputs in action subtitles", () => {
    const generateDocumentSubtitle = activitySubtitleFor({
      kind: "generateDocument",
      officialType: "Microflows$GenerateDocumentAction",
      outputFileDocumentVariableName: "invoiceDoc",
    });
    const callWorkflowSubtitle = activitySubtitleFor({
      kind: "callWorkflow",
      officialType: "Microflows$CallWorkflowAction",
      outputWorkflowVariableName: "workflowInstance",
    });
    const externalActionSubtitle = activitySubtitleFor({
      kind: "callExternalAction",
      officialType: "Microflows$CallExternalAction",
      returnVariableName: "crmResult",
    });

    expect(generateDocumentSubtitle).toContain("out: invoiceDoc");
    expect(callWorkflowSubtitle).toContain("out: workflowInstance");
    expect(externalActionSubtitle).toContain("out: crmResult");
  });

  it("includes nested returnValue outputs for Java-like actions in action subtitles", () => {
    const subtitle = activitySubtitleFor({
      kind: "callJavaAction",
      officialType: "Microflows$CallJavaAction",
      returnValue: {
        outputVariableName: "javaResult",
        resultVariableName: "javaResult",
      },
    });

    expect(subtitle).toContain("out: javaResult");
  });
});
