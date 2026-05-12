import { describe, expect, it, vi } from "vitest";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: () => null,
  Input: () => null,
  Select: () => null,
  TextArea: () => null,
  Typography: {
    Text: () => null,
  },
}));

vi.mock("../../property-panel/selectors/VariableSelector", () => ({
  VariableSelector: () => null,
}));

vi.mock("./useInlineEditorDraft", () => ({
  useInlineEditorDraft: () => ({
    draft: {},
    fieldErrors: {},
    isDraftValid: () => true,
    updateField: () => undefined,
  }),
}));

vi.mock("./useFlowGramMicroflowContext", () => ({
  useFlowGramMicroflowContext: () => ({ readonly: false }),
}));

import { createSequenceFlow } from "../../adapters";
import type { MicroflowActionActivity, MicroflowAuthoringSchema, MicroflowErrorHandler, MicroflowTryCatch } from "../../schema";
import type { FlowGramMicroflowNodeData } from "../FlowGramMicroflowTypes";
import { applyDraft, buildInitialDraft } from "./MicroflowInlineNodeEditor";

function schemaWith(objects: MicroflowActionActivity[], flows: unknown[] = []): MicroflowAuthoringSchema {
  return {
    schemaVersion: "1.0.0",
    id: "mf-inline",
    name: "Inline MF",
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: { objects },
    flows,
    editor: { selection: {}, viewport: { x: 0, y: 0, zoom: 1 } },
  } as unknown as MicroflowAuthoringSchema;
}

function restCallObject(outputVariableName = "response"): MicroflowActionActivity {
  return {
    id: "restCall-activity",
    stableId: "restCall-activity",
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: "restCall",
    documentation: "",
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 160, height: 80 },
    ports: [],
    autoGenerateCaption: false,
    backgroundColor: "default",
    action: {
      id: "restCall-action",
      officialType: "Microflows$RestCallAction",
      kind: "restCall",
      caption: "restCall",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "integration", iconKey: "restCall", availability: "supported" },
      request: {
        method: "GET",
        urlExpression: { raw: "/api/orders", inferredType: { kind: "string" }, references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
        headers: [],
        queryParameters: [],
        body: { kind: "none" },
      },
      response: {
        handling: { kind: "json", outputVariableName },
      },
      timeoutSeconds: 30,
    } as never,
  } as unknown as MicroflowActionActivity;
}

function callMicroflowObject(outputVariableName = "result"): MicroflowActionActivity {
  return {
    id: "callMicroflow-activity",
    stableId: "callMicroflow-activity",
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: "callMicroflow",
    documentation: "",
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 160, height: 80 },
    ports: [],
    autoGenerateCaption: false,
    backgroundColor: "default",
    action: {
      id: "callMicroflow-action",
      officialType: "Microflows$MicroflowCallAction",
      kind: "callMicroflow",
      caption: "callMicroflow",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "call", iconKey: "callMicroflow", availability: "supported" },
      targetMicroflowId: "MF_Target",
      parameterMappings: [],
      returnValue: {
        storeResult: true,
        outputVariableName,
        resultVariableName: outputVariableName,
        dataType: { kind: "string" },
      },
      callMode: "sync",
    } as never,
  } as unknown as MicroflowActionActivity;
}

function createVariableObject(): MicroflowActionActivity {
  return {
    id: "createVariable-activity",
    stableId: "createVariable-activity",
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: "createVariable",
    documentation: "",
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 160, height: 80 },
    ports: [],
    autoGenerateCaption: false,
    backgroundColor: "default",
    action: {
      id: "createVariable-action",
      officialType: "Microflows$CreateVariableAction",
      kind: "createVariable",
      caption: "createVariable",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "variable", iconKey: "createVariable", availability: "supported" },
      variableName: "discount",
      dataType: { kind: "decimal" },
      initialValue: {
        raw: "$seedDiscount",
        inferredType: { kind: "decimal" },
        references: { variables: ["$seedDiscount"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
        diagnostics: [],
      },
      readonly: false,
    } as never,
  } as unknown as MicroflowActionActivity;
}

function changeVariableObject(targetVariableName = "response", expressionRaw = "$response"): MicroflowActionActivity {
  return {
    id: "changeVariable-activity",
    stableId: "changeVariable-activity",
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: "changeVariable",
    documentation: "",
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 160, height: 80 },
    ports: [],
    autoGenerateCaption: false,
    backgroundColor: "default",
    action: {
      id: "changeVariable-action",
      officialType: "Microflows$ChangeVariableAction",
      kind: "changeVariable",
      caption: "changeVariable",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "variable", iconKey: "changeVariable", availability: "supported" },
      targetVariableName,
      newValueExpression: {
        raw: expressionRaw,
        inferredType: { kind: "string" },
        references: { variables: [expressionRaw], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
        diagnostics: [],
      },
    } as never,
  } as unknown as MicroflowActionActivity;
}

function nodeData(action: MicroflowActionActivity["action"], objectId: string, actionKind: string): FlowGramMicroflowNodeData {
  return {
    objectId,
    objectKind: "actionActivity",
    collectionId: "nodes",
    title: actionKind,
    validationState: "valid",
    issueCount: 0,
    actionKind: actionKind as never,
    action,
    officialType: "Microflows$ActionActivity",
    disabled: false,
  } as never;
}

function errorHandlerObject(): MicroflowErrorHandler {
  return {
    id: "error-handler-object",
    stableId: "error-handler-object",
    kind: "errorHandler",
    officialType: "Microflows$ErrorHandler",
    caption: "errorHandler",
    documentation: "",
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 160, height: 80 },
    ports: [],
    autoGenerateCaption: false,
    backgroundColor: "default",
    policy: "custom",
    customHandlerVariable: "capturedError",
    continueOnError: true,
  } as unknown as MicroflowErrorHandler;
}

function tryCatchObject(): MicroflowTryCatch {
  return {
    id: "try-catch-object",
    stableId: "try-catch-object",
    kind: "tryCatch",
    officialType: "Microflows$TryCatch",
    caption: "tryCatch",
    documentation: "",
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 200, height: 86 },
    ports: [],
    editor: { iconKey: "tryCatch" },
    tryBranchKey: "try-main",
    catchBranchKey: "catch-main",
    finallyBranchKey: "finally-main",
    errorVariableName: "$latestError",
  } as unknown as MicroflowTryCatch;
}

describe("MicroflowInlineNodeEditor schema helpers", () => {
  it("builds drafts from current create/change/rest/callMicroflow schema fields", () => {
    expect(buildInitialDraft(nodeData(createVariableObject().action, "createVariable-activity", "createVariable"))).toMatchObject({
      variableName: "discount",
      initialValue: "seedDiscount",
    });
    expect(buildInitialDraft(nodeData(changeVariableObject("approvalLevel", "$approvalLevel").action, "changeVariable-activity", "changeVariable"))).toMatchObject({
      changeVariableName: "approvalLevel",
      newValue: "approvalLevel",
    });
    expect(buildInitialDraft(nodeData(restCallObject("payload").action, "restCall-activity", "restCall"))).toMatchObject({
      responseVariableName: "payload",
    });
    expect(buildInitialDraft(nodeData(callMicroflowObject("riskScore").action, "callMicroflow-activity", "callMicroflow"))).toMatchObject({
      returnVariableName: "riskScore",
    });
    expect(buildInitialDraft({
      objectId: "error-handler-object",
      objectKind: "errorHandler",
      collectionId: "nodes",
      title: "errorHandler",
      validationState: "valid",
      issueCount: 0,
      officialType: "Microflows$ErrorHandler",
      disabled: false,
      policy: "custom",
      customHandlerVariable: "capturedError",
      continueOnError: true,
    } as never)).toMatchObject({
      policy: "custom",
      customHandlerVariable: "capturedError",
      continueOnError: true,
    });
    expect(buildInitialDraft({
      objectId: "try-catch-object",
      objectKind: "tryCatch",
      collectionId: "nodes",
      title: "tryCatch",
      validationState: "valid",
      issueCount: 0,
      officialType: "Microflows$TryCatch",
      disabled: false,
      tryBranchKey: "try-main",
      catchBranchKey: "catch-main",
      finallyBranchKey: "finally-main",
      errorVariableName: "$latestError",
    } as never)).toMatchObject({
      tryBranchKey: "try-main",
      catchBranchKey: "catch-main",
      finallyBranchKey: "finally-main",
      errorVariableName: "$latestError",
    });
  });

  it("rewrites downstream references when inline-editing restCall output variables", () => {
    const restCall = restCallObject("response");
    const changeVariable = changeVariableObject("response", "$response");
    const schema = schemaWith(
      [restCall, changeVariable],
      [createSequenceFlow({ originObjectId: restCall.id, destinationObjectId: changeVariable.id })],
    );

    const next = applyDraft(schema, restCall.id, nodeData(restCall.action, restCall.id, "restCall"), { responseVariableName: "payload" });
    const changed = next.objectCollection.objects.find(object => object.id === changeVariable.id);
    const source = next.objectCollection.objects.find(object => object.id === restCall.id);

    expect(source?.kind === "actionActivity" && source.action.kind === "restCall" && source.action.response.handling.kind !== "ignore"
      ? source.action.response.handling.outputVariableName
      : undefined).toBe("payload");
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.targetVariableName : undefined).toBe("payload");
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.newValueExpression.raw : undefined).toBe("$payload");
  });

  it("rewrites downstream references when inline-editing callMicroflow return variables", () => {
    const callMicroflow = callMicroflowObject("result");
    const changeVariable = changeVariableObject("result", "$result");
    const schema = schemaWith(
      [callMicroflow, changeVariable],
      [createSequenceFlow({ originObjectId: callMicroflow.id, destinationObjectId: changeVariable.id })],
    );

    const next = applyDraft(schema, callMicroflow.id, nodeData(callMicroflow.action, callMicroflow.id, "callMicroflow"), { returnVariableName: "finalResult" });
    const changed = next.objectCollection.objects.find(object => object.id === changeVariable.id);
    const source = next.objectCollection.objects.find(object => object.id === callMicroflow.id);

    expect(source?.kind === "actionActivity" && source.action.kind === "callMicroflow" ? source.action.returnValue.outputVariableName : undefined).toBe("finalResult");
    expect(source?.kind === "actionActivity" && source.action.kind === "callMicroflow" ? source.action.returnValue.resultVariableName : undefined).toBe("finalResult");
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.targetVariableName : undefined).toBe("finalResult");
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.newValueExpression.raw : undefined).toBe("$finalResult");
  });

  it("writes modern create/change variable fields instead of legacy inline bridge fields", () => {
    const createVariable = createVariableObject();
    const changeVariable = changeVariableObject("approvalLevel", "$approvalLevel");
    const schema = schemaWith([createVariable, changeVariable]);

    const afterCreate = applyDraft(schema, createVariable.id, nodeData(createVariable.action, createVariable.id, "createVariable"), {
      variableName: "finalDiscount",
      initialValue: "seedDiscount",
    });
    const created = afterCreate.objectCollection.objects.find(object => object.id === createVariable.id);
    expect(created?.kind === "actionActivity" && created.action.kind === "createVariable" ? created.action.variableName : undefined).toBe("finalDiscount");
    expect(created?.kind === "actionActivity" && created.action.kind === "createVariable" ? created.action.initialValue?.raw : undefined).toBe("$seedDiscount");

    const afterChange = applyDraft(schema, changeVariable.id, nodeData(changeVariable.action, changeVariable.id, "changeVariable"), {
      changeVariableName: "finalDiscount",
      newValue: "seedDiscount",
    });
    const changed = afterChange.objectCollection.objects.find(object => object.id === changeVariable.id);
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.targetVariableName : undefined).toBe("finalDiscount");
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.newValueExpression.raw : undefined).toBe("$seedDiscount");
  });

  it("updates errorHandler objects through inline draft helpers", () => {
    const errorHandler = errorHandlerObject();
    const schema = {
      ...schemaWith([]),
      objectCollection: { objects: [errorHandler] },
    } as unknown as MicroflowAuthoringSchema;

    const updated = applyDraft(schema, errorHandler.id, {
      objectId: errorHandler.id,
      objectKind: "errorHandler",
      collectionId: "nodes",
      title: "errorHandler",
      validationState: "valid",
      issueCount: 0,
      officialType: "Microflows$ErrorHandler",
      disabled: false,
      policy: errorHandler.policy,
      customHandlerVariable: errorHandler.customHandlerVariable,
      continueOnError: errorHandler.continueOnError,
    } as never, {
      policy: "continue",
      customHandlerVariable: "handledError",
      continueOnError: false,
    });
    const changed = updated.objectCollection.objects.find(item => item.id === errorHandler.id);

    expect(changed?.kind === "errorHandler" ? changed.policy : undefined).toBe("continue");
    expect(changed?.kind === "errorHandler" ? changed.customHandlerVariable : undefined).toBe("handledError");
    expect(changed?.kind === "errorHandler" ? changed.continueOnError : undefined).toBe(false);
  });

  it("updates tryCatch objects through inline draft helpers", () => {
    const tryCatch = tryCatchObject();
    const schema = {
      ...schemaWith([]),
      objectCollection: { objects: [tryCatch] },
    } as unknown as MicroflowAuthoringSchema;

    const updated = applyDraft(schema, tryCatch.id, {
      objectId: tryCatch.id,
      objectKind: "tryCatch",
      collectionId: "nodes",
      title: "tryCatch",
      validationState: "valid",
      issueCount: 0,
      officialType: "Microflows$TryCatch",
      disabled: false,
      tryBranchKey: tryCatch.tryBranchKey,
      catchBranchKey: tryCatch.catchBranchKey,
      finallyBranchKey: tryCatch.finallyBranchKey,
      errorVariableName: tryCatch.errorVariableName,
    } as never, {
      tryBranchKey: "try-updated",
      catchBranchKey: "catch-updated",
      finallyBranchKey: "",
      errorVariableName: "$capturedError",
    });
    const changed = updated.objectCollection.objects.find(item => item.id === tryCatch.id);

    expect(changed?.kind === "tryCatch" ? changed.tryBranchKey : undefined).toBe("try-updated");
    expect(changed?.kind === "tryCatch" ? changed.catchBranchKey : undefined).toBe("catch-updated");
    expect(changed?.kind === "tryCatch" ? changed.finallyBranchKey : undefined).toBeUndefined();
    expect(changed?.kind === "tryCatch" ? changed.errorVariableName : undefined).toBe("$capturedError");
  });
});
