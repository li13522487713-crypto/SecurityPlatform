import { describe, expect, it } from "vitest";
import type { MetadataMicroflowRef } from "../../metadata";
import type { MicroflowCallMicroflowAction, MicroflowDataType, MicroflowExpression } from "../../schema";
import {
  clearCallMicroflowTarget,
  getCallMicroflowReferenceDescriptor,
  rebuildCallMicroflowMappings,
  updateCallMicroflowReturnBinding,
  updateCallMicroflowTarget,
} from "../utils/call-microflow-config";

function expression(raw = "", inferredType?: MicroflowDataType): MicroflowExpression {
  return {
    raw,
    inferredType,
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: [],
  };
}

function action(overrides: Partial<MicroflowCallMicroflowAction> = {}): MicroflowCallMicroflowAction {
  return {
    id: "action-call-1",
    kind: "callMicroflow",
    officialType: "Microflows$MicroflowCallAction",
    errorHandlingType: "rollback",
    editor: { category: "call", iconKey: "callMicroflow", availability: "supported" },
    targetMicroflowId: "",
    targetMicroflowName: "",
    targetMicroflowQualifiedName: "",
    parameterMappings: [],
    returnValue: { storeResult: false },
    callMode: "sync",
    ...overrides,
  };
}

const target: MetadataMicroflowRef = {
  id: "mf-validate-purchase-request",
  name: "MF_ValidatePurchaseRequest",
  displayName: "Validate Purchase Request",
  qualifiedName: "Procurement.MF_ValidatePurchaseRequest",
  moduleId: "procurement-module",
  moduleName: "Procurement",
  version: "0.1.0",
  schemaId: "schema-b",
  status: "draft",
  parameters: [
    { id: "param-amount", name: "amount", type: { kind: "decimal" }, required: true, order: 0 },
    { id: "param-user", name: "userName", type: { kind: "string" }, required: true, order: 1 },
  ],
  returnType: { kind: "boolean" },
};

describe("Call Microflow Stage 15 config helpers", () => {
  it("writes stable target id, name and qualified name", () => {
    const next = updateCallMicroflowTarget(action(), target);

    expect(next.targetMicroflowId).toBe(target.id);
    expect(next.targetMicroflowName).toBe(target.name);
    expect(next.targetMicroflowDisplayName).toBe(target.displayName);
    expect(next.targetMicroflowQualifiedName).toBe(target.qualifiedName);
    expect(next.targetModuleId).toBe(target.moduleId);
    expect(next.parameterMappings.map(mapping => mapping.parameterName)).toEqual(["amount", "userName"]);
    expect(next.parameterMappings.map(mapping => mapping.targetParameterId)).toEqual(["param-amount", "param-user"]);
  });

  it("preserves same-name parameter mappings when the target reloads", () => {
    const mappings = rebuildCallMicroflowMappings(target.parameters, [
      {
        parameterName: "amount",
        parameterType: { kind: "decimal" },
        argumentExpression: expression("purchaseAmount", { kind: "decimal" }),
        sourceVariableName: "purchaseAmount",
      },
      {
        parameterName: "stale",
        parameterType: { kind: "string" },
        argumentExpression: expression("oldValue", { kind: "string" }),
      },
    ]);

    expect(mappings).toHaveLength(2);
    expect(mappings[0]?.argumentExpression.raw).toBe("purchaseAmount");
    expect(mappings[0]?.sourceVariableName).toBe("purchaseAmount");
    expect(mappings[1]?.argumentExpression.raw).toBe("");
  });

  it("clears mappings and return binding when target is cleared", () => {
    const next = clearCallMicroflowTarget(action({
      targetMicroflowId: target.id,
      targetMicroflowName: target.name,
      targetMicroflowDisplayName: target.displayName,
      targetMicroflowQualifiedName: target.qualifiedName,
      targetModuleId: target.moduleId,
      parameterMappings: [{ parameterName: "amount", parameterType: { kind: "decimal" }, argumentExpression: expression("amount") }],
      returnValue: { storeResult: true, outputVariableName: "isValid", dataType: { kind: "boolean" } },
    }));

    expect(next.targetMicroflowId).toBe("");
    expect(next.targetMicroflowName).toBe("");
    expect(next.targetMicroflowDisplayName).toBe("");
    expect(next.targetMicroflowQualifiedName).toBe("");
    expect(next.targetModuleId).toBe("");
    expect(next.parameterMappings).toEqual([]);
    expect(next.returnValue).toEqual({ storeResult: false });
  });

  it("disables return binding for void targets", () => {
    const next = updateCallMicroflowTarget(action({
      returnValue: { storeResult: true, outputVariableName: "result", dataType: { kind: "boolean" } },
    }), { ...target, returnType: { kind: "void" } });

    expect(next.returnValue.storeResult).toBe(false);
    expect(next.returnValue.outputVariableName).toBeUndefined();
  });

  it("uses returnValue.outputVariableName as the persisted return binding", () => {
    const next = updateCallMicroflowReturnBinding(action(), "validationResult");

    expect(next.returnValue).toMatchObject({
      storeResult: true,
      outputVariableName: "validationResult",
    });
  });

  it("exposes fields needed by backend reference scanner", () => {
    const descriptor = getCallMicroflowReferenceDescriptor(updateCallMicroflowTarget(action(), target));

    expect(descriptor).toEqual({
      actionId: "action-call-1",
      targetMicroflowId: target.id,
      targetMicroflowQualifiedName: target.qualifiedName,
    });
  });
});
