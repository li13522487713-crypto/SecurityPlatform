// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { MicroflowActionActivity, MicroflowAuthoringSchema } from "../../schema";
import { GenericActionFields } from "./generic-action-fields-form";

vi.mock("@douyinfe/semi-ui", () => ({
  Input: ({ value, onChange, disabled }: any) => <input value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  Select: ({ value, onChange, optionList, disabled }: any) => (
    <select value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
      {(optionList ?? []).map((option: any) => <option key={option.value} value={option.value}>{option.label}</option>)}
    </select>
  ),
  Space: ({ children }: any) => <div>{children}</div>,
  Switch: ({ checked, onChange, disabled }: any) => <input type="checkbox" checked={Boolean(checked)} disabled={disabled} onChange={event => onChange?.(event.currentTarget.checked)} />,
  Tag: ({ children }: any) => <span>{children}</span>,
  TextArea: ({ value, onChange, disabled }: any) => <textarea value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
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
    useMicroflowMetadataCatalog: () => ({ entities: [], enumerations: [], microflows: [] }),
    useMetadataStatus: () => ({ version: "test", loading: false, error: undefined }),
  };
});

afterEach(() => cleanup());

describe("GenericActionFields connector-backed forms", () => {
  it("shows capability missing publish blocker and patches WebService fields", () => {
    const onPatch = vi.fn();
    render(<GenericActionFields schema={schema()} object={connectorObject("webServiceCall")} issues={[]} onPatch={onPatch} />);

    expect(screen.getByText("Capability Missing")).toBeTruthy();
    expect(screen.getByText(/publish must block/i)).toBeTruthy();
    fireEvent.change(screen.getAllByDisplayValue("")[0], { target: { value: "SalesOrderService" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ webServiceQualifiedName: "SalesOrderService" }),
      }),
    }));
  });

  it("patches external action fields", () => {
    const onPatch = vi.fn();
    render(<GenericActionFields schema={schema()} object={connectorObject("callExternalAction")} issues={[]} onPatch={onPatch} />);

    fireEvent.change(screen.getAllByDisplayValue("")[0], { target: { value: "crm" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ consumedServiceQualifiedName: "crm" }),
      }),
    }));
  });

  it("patches external object fields", () => {
    const onPatch = vi.fn();
    render(<GenericActionFields schema={schema()} object={connectorObject("sendExternalObject")} issues={[]} onPatch={onPatch} />);

    fireEvent.change(screen.getAllByDisplayValue("")[0], { target: { value: "externalOrder" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ externalObjectVariableName: "externalOrder" }),
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
    editor: { selection: {}, viewport: { x: 0, y: 0, zoom: 1 } },
  } as MicroflowAuthoringSchema;
}

function connectorObject(kind: string): MicroflowActionActivity {
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
      officialType: "Microflows$GenericAction",
      kind,
      caption: kind,
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "integration", iconKey: kind, availability: "requiresConnector", availabilityReason: "需要连接器" },
    } as never,
  } as MicroflowActionActivity;
}
