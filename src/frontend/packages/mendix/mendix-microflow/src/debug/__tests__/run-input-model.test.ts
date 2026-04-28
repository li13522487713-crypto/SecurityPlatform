import { describe, expect, it } from "vitest";

import { sampleMicroflowSchema, type MicroflowSchema } from "../../schema";
import {
  buildRunInputModel,
  buildRunRequest,
  coerceRunInputValue,
  shouldBlockRun,
  updateRunErrorForMicroflow,
  updateRunInputsForMicroflow,
  updateRunResultForMicroflow,
  validateRunInputs,
  type MicroflowRunPanelState,
} from "../run-input-model";

function schemaWithParameters(id = "MF_RUN"): MicroflowSchema {
  return {
    ...sampleMicroflowSchema,
    id,
    stableId: id,
    schemaVersion: "1.0.0",
    parameters: [
      { id: "param-amount", name: "amount", dataType: { kind: "decimal" }, required: true },
      { id: "param-user", name: "userName", dataType: { kind: "string" }, required: true },
      { id: "param-approved", name: "approved", dataType: { kind: "boolean" }, required: false },
      { id: "param-tags", name: "tags", dataType: { kind: "list", itemType: { kind: "string" } }, required: false },
    ],
  };
}

describe("Microflow Stage 21 run input model", () => {
  it("builds fields from schema-level parameters", () => {
    const model = buildRunInputModel(schemaWithParameters());

    expect(model.fields.map(field => field.parameter.name)).toEqual(["amount", "userName", "approved", "tags"]);
    expect(model.fields.map(field => field.controlKind)).toEqual(["number", "text", "boolean", "json"]);
  });

  it("blocks required missing inputs", () => {
    const model = buildRunInputModel(schemaWithParameters());
    const validation = validateRunInputs(model, { amount: "", userName: "" });

    expect(validation.valid).toBe(false);
    expect(validation.errors).toMatchObject({ amount: "必填参数不能为空", userName: "必填参数不能为空" });
  });

  it("blocks invalid number input", () => {
    const model = buildRunInputModel(schemaWithParameters());
    const validation = validateRunInputs(model, { amount: "abc", userName: "alice" });

    expect(validation.valid).toBe(false);
    expect(validation.errors.amount).toBe("请输入数字");
  });

  it("coerces number, boolean and JSON list values", () => {
    expect(coerceRunInputValue("100.5", { kind: "decimal" }).value).toBe(100.5);
    expect(coerceRunInputValue("true", { kind: "boolean" }).value).toBe(true);
    expect(coerceRunInputValue("[\"a\"]", { kind: "list", itemType: { kind: "string" } }).value).toEqual(["a"]);
  });

  it("builds real run request DTO with active microflow id and inputs", () => {
    const schema = schemaWithParameters("MF_ACTIVE");
    const request = buildRunRequest(schema, { amount: 100, userName: "alice" }, { maxSteps: 10 });

    expect(request.microflowId).toBe("MF_ACTIVE");
    expect(request.schema).toBeUndefined();
    expect(request.debug).toBe(true);
    expect(request.correlationId).toContain("mf-run-MF_ACTIVE-");
    expect(request.input).toEqual({ amount: 100, userName: "alice" });
    expect(request.options?.maxSteps).toBe(10);
  });

  it("blocks run for validation errors or input errors", () => {
    expect(shouldBlockRun([{ severity: "error" }], {}, false).reason).toBe("validation");
    expect(shouldBlockRun([], { amount: "请输入数字" }, false).reason).toBe("inputs");
    expect(shouldBlockRun([], {}, true, "blockUntilSaved").reason).toBe("dirty");
    expect(shouldBlockRun([], {}, true, "saveAndRun").blocked).toBe(false);
  });

  it("keeps A/B run inputs and results isolated", () => {
    const initial: MicroflowRunPanelState = {
      runInputsByMicroflowId: {},
      runResultByMicroflowId: {},
      runErrorByMicroflowId: {},
      activeRunIdByMicroflowId: {},
    };

    const withAInput = updateRunInputsForMicroflow(initial, "MF_A", { amount: 100 });
    const withBInput = updateRunInputsForMicroflow(withAInput, "MF_B", { amount: 200 });
    const withAResult = updateRunResultForMicroflow(withBInput, "MF_A", { status: "success" }, "run-a");
    const withBError = updateRunErrorForMicroflow(withAResult, "MF_B", "failed");

    expect(withBError.runInputsByMicroflowId.MF_A).toEqual({ amount: 100 });
    expect(withBError.runInputsByMicroflowId.MF_B).toEqual({ amount: 200 });
    expect(withBError.runResultByMicroflowId.MF_A).toEqual({ status: "success" });
    expect(withBError.activeRunIdByMicroflowId.MF_A).toBe("run-a");
    expect(withBError.runErrorByMicroflowId.MF_B).toBe("failed");
    expect(withBError.runResultByMicroflowId.MF_B).toBeUndefined();
  });
});
