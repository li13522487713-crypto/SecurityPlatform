// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { MicroflowActionActivity, MicroflowAuthoringSchema } from "../../schema";
import { createSequenceFlow } from "../../adapters";
import { ActionActivityForm } from "./action-activity-form";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick }: any) => <button type="button" onClick={onClick}>{children}</button>,
  Input: ({ value, onChange, disabled, placeholder }: any) => <input placeholder={placeholder} value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  InputNumber: ({ value, onChange, disabled }: any) => <input value={value ?? 0} disabled={disabled} onChange={event => onChange?.(Number(event.currentTarget.value))} />,
  Select: ({ value, onChange, optionList, disabled }: any) => (
    <select value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
      {(optionList ?? []).map((option: any) => <option key={option.value} value={option.value}>{option.label}</option>)}
    </select>
  ),
  Space: ({ children }: any) => <div>{children}</div>,
  Switch: ({ checked, onChange, disabled }: any) => <input type="checkbox" checked={Boolean(checked)} disabled={disabled} onChange={event => onChange?.(event.currentTarget.checked)} />,
  Tag: ({ children }: any) => <span>{children}</span>,
  TextArea: ({ value, onChange, disabled }: any) => <textarea value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  Tooltip: ({ children }: any) => <>{children}</>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
    Title: ({ children }: any) => <h3>{children}</h3>,
  },
}));

vi.mock("../expression", () => ({
  ExpressionEditor: ({ value, onChange, readonly }: any) => <input aria-label="expression-editor" value={value?.raw ?? ""} disabled={readonly} onChange={event => onChange?.({ ...(value ?? {}), raw: event.currentTarget.value })} />,
}));

vi.mock("../../metadata", async () => {
  const actual = await vi.importActual<any>("../../metadata");
  return {
    ...actual,
    useMicroflowMetadataCatalog: () => ({
      entities: [],
      enumerations: [],
      microflows: [{
        id: "mf-target",
        name: "ApproveOrder",
        displayName: "Approve Order",
        qualifiedName: "Sales.ApproveOrder",
        moduleName: "Sales",
        parameters: [{ name: "orderId", type: { kind: "string" }, required: true }],
        returnType: { kind: "boolean" },
        status: "published",
      }],
    }),
    useMetadataStatus: () => ({ version: "test", loading: false, error: undefined }),
  };
});

vi.mock("../selectors", async () => {
  const actual = await vi.importActual<any>("../selectors");
  return {
    ...actual,
    MicroflowSelector: ({ value, onChange, disabled }: any) => (
      <select data-testid="microflow-selector" value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
        <option value="">(none)</option>
        <option value="mf-target">Sales.ApproveOrder</option>
      </select>
    ),
    VariableSelector: ({ value, onChange, disabled }: any) => (
      <select data-testid="variable-selector" value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
        <option value="">(none)</option>
        <option value="orderId">orderId</option>
        <option value="result">result</option>
      </select>
    ),
  };
});

afterEach(() => cleanup());

describe("ActionActivityForm callMicroflow/restCall", () => {
  it("patches callMicroflow target from metadata and generates signature UI", () => {
    const onPatch = vi.fn();
    render(<ActionActivityForm schema={schema()} object={callMicroflowObject()} issues={[]} onPatch={onPatch} />);

    fireEvent.change(screen.getByTestId("microflow-selector"), { target: { value: "mf-target" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({
          targetMicroflowId: "mf-target",
          targetMicroflowQualifiedName: "Sales.ApproveOrder",
        }),
      }),
    }));
  });

  it("patches restCall method, url expression and response variables", () => {
    const onPatch = vi.fn();
    render(<ActionActivityForm schema={schema()} object={restCallObject()} issues={[]} onPatch={onPatch} />);

    fireEvent.change(screen.getByDisplayValue("GET"), { target: { value: "POST" } });
    fireEvent.change(screen.getAllByLabelText("expression-editor")[0], { target: { value: "'https://example.com/orders'" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({
          request: expect.objectContaining({ method: "POST" }),
        }),
      }),
    }));
    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({
          request: expect.objectContaining({ urlExpression: expect.objectContaining({ raw: "'https://example.com/orders'" }) }),
        }),
      }),
    }));
  });

  it("routes callMicroflow return variable rename through schema-level rewrite when onSchemaChange is available", () => {
    const onPatch = vi.fn();
    const onSchemaChange = vi.fn();
    render(
      <ActionActivityForm
        schema={schema(
          [callMicroflowObject({ storeResult: true, outputVariableName: "result" }), downstreamChangeVariableObject("result", "$result")],
          [createSequenceFlow({ originObjectId: "callMicroflow-activity", destinationObjectId: "downstream-change-variable-activity" })],
        )}
        object={callMicroflowObject({ storeResult: true, outputVariableName: "result" })}
        issues={[]}
        onPatch={onPatch}
        onSchemaChange={onSchemaChange}
      />,
    );

    const returnNameInput = screen.getAllByDisplayValue("result").find(element => element.tagName === "INPUT");
    if (!returnNameInput) {
      throw new Error("Expected return variable name input.");
    }
    fireEvent.change(returnNameInput, { target: { value: "finalResult" } });

    expect(onPatch).not.toHaveBeenCalled();
    expect(onSchemaChange).toHaveBeenCalledTimes(1);
    const [nextSchema, reason] = onSchemaChange.mock.calls[0] ?? [];
    const changed = nextSchema?.objectCollection?.objects?.find((item: any) => item.id === "downstream-change-variable-activity");
    expect(reason).toBe("renameActionOutputVariable");
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.targetVariableName : undefined).toBe("finalResult");
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.newValueExpression.raw : undefined).toBe("$finalResult");
  });

  it("routes rest response variable rename through schema-level rewrite when onSchemaChange is available", () => {
    const onPatch = vi.fn();
    const onSchemaChange = vi.fn();
    render(
      <ActionActivityForm
        schema={schema(
          [restCallObject({ handling: { kind: "json", outputVariableName: "response" } }), downstreamChangeVariableObject("response", "$response")],
          [createSequenceFlow({ originObjectId: "restCall-activity", destinationObjectId: "downstream-change-variable-activity" })],
        )}
        object={restCallObject({ handling: { kind: "json", outputVariableName: "response" } })}
        issues={[]}
        onPatch={onPatch}
        onSchemaChange={onSchemaChange}
      />,
    );

    fireEvent.change(screen.getAllByDisplayValue("response")[0], { target: { value: "payload" } });

    expect(onPatch).not.toHaveBeenCalled();
    expect(onSchemaChange).toHaveBeenCalledTimes(1);
    const [nextSchema, reason] = onSchemaChange.mock.calls[0] ?? [];
    const changed = nextSchema?.objectCollection?.objects?.find((item: any) => item.id === "downstream-change-variable-activity");
    expect(reason).toBe("renameActionOutputVariable");
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.targetVariableName : undefined).toBe("payload");
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.newValueExpression.raw : undefined).toBe("$payload");
  });
});

function schema(objects: MicroflowActionActivity[] = [], flows: unknown[] = []): MicroflowAuthoringSchema {
  return {
    schemaVersion: "1.0.0",
    id: "mf",
    name: "MF",
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: { objects },
    flows,
    variables: {
      localVariables: {
        orderId: { name: "orderId", dataType: { kind: "string" }, source: { kind: "localVariable", objectId: "var", actionId: "var-action" }, scope: {} },
      },
    },
    editor: { selection: {}, viewport: { x: 0, y: 0, zoom: 1 } },
  } as unknown as MicroflowAuthoringSchema;
}

function activity(kind: string, patch: Record<string, unknown>): MicroflowActionActivity {
  return {
    id: `${kind}-activity`,
    stableId: `${kind}-activity`,
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: kind,
    documentation: "",
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 160, height: 80 },
    ports: [],
    autoGenerateCaption: false,
    backgroundColor: "default",
    action: {
      id: `${kind}-action`,
      officialType: kind === "callMicroflow" ? "Microflows$MicroflowCallAction" : "Microflows$RestCallAction",
      kind,
      caption: kind,
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: kind === "callMicroflow" ? "call" : "integration", iconKey: kind, availability: "supported" },
      ...patch,
    } as never,
  } as unknown as MicroflowActionActivity;
}

function callMicroflowObject(options?: { storeResult?: boolean; outputVariableName?: string }): MicroflowActionActivity {
  return activity("callMicroflow", {
    targetMicroflowId: "",
    parameterMappings: [],
    returnValue: { storeResult: options?.storeResult ?? false, outputVariableName: options?.outputVariableName, resultVariableName: options?.outputVariableName },
    callMode: "sync",
  });
}

function restCallObject(options?: { handling?: { kind: "ignore" } | { kind: "string" | "json" | "importMapping"; outputVariableName: string; importMappingQualifiedName?: string } }): MicroflowActionActivity {
  return activity("restCall", {
    request: {
      method: "GET",
      urlExpression: { raw: "", inferredType: { kind: "string" }, referencedVariables: [], references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
      headers: [],
      queryParameters: [],
      body: { kind: "none" },
    },
    timeoutSeconds: 30,
    response: {
      handling: options?.handling ?? { kind: "ignore" },
      statusCodeVariableName: "",
      headersVariableName: "",
    },
  });
}

function downstreamChangeVariableObject(targetVariableName: string, expressionRaw: string): MicroflowActionActivity {
  return {
    id: "downstream-change-variable-activity",
    stableId: "downstream-change-variable-activity",
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: "changeVariable",
    documentation: "",
    relativeMiddlePoint: { x: 200, y: 0 },
    size: { width: 160, height: 80 },
    ports: [],
    autoGenerateCaption: false,
    backgroundColor: "default",
    action: {
      id: "downstream-change-variable-action",
      officialType: "Microflows$ChangeVariableAction",
      kind: "changeVariable",
      caption: "changeVariable",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "variable", iconKey: "changeVariable", availability: "supported" },
      targetVariableName,
      newValueExpression: { raw: expressionRaw, inferredType: { kind: "string" }, referencedVariables: [expressionRaw], references: { variables: [expressionRaw], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
    } as never,
  } as unknown as MicroflowActionActivity;
}
