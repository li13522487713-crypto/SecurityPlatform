// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { MicroflowActionActivity, MicroflowAuthoringSchema } from "../../schema";
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
});

function schema(): MicroflowAuthoringSchema {
  return {
    schemaVersion: "1.0.0",
    id: "mf",
    name: "MF",
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: { objects: [] },
    flows: [],
    variables: {
      localVariables: {
        orderId: { name: "orderId", dataType: { kind: "string" }, source: { kind: "localVariable", objectId: "var", actionId: "var-action" }, scope: {} },
      },
    },
    editor: { selection: {}, viewport: { x: 0, y: 0, zoom: 1 } },
  } as MicroflowAuthoringSchema;
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
  } as MicroflowActionActivity;
}

function callMicroflowObject(): MicroflowActionActivity {
  return activity("callMicroflow", {
    targetMicroflowId: "",
    parameterMappings: [],
    returnValue: { storeResult: false },
    callMode: "sync",
  });
}

function restCallObject(): MicroflowActionActivity {
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
      handling: { kind: "ignore" },
      statusCodeVariableName: "",
      headersVariableName: "",
    },
  });
}
