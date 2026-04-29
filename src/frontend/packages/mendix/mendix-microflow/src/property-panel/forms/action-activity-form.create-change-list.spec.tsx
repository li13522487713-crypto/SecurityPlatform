// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { MicroflowActionActivity, MicroflowAuthoringSchema } from "../../schema";
import { ActionActivityForm } from "./action-activity-form";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick }: any) => <button type="button" onClick={onClick}>{children}</button>,
  Input: ({ value, onChange, disabled }: any) => <input value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
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

vi.mock("../selectors", async () => {
  const actual = await vi.importActual<any>("../selectors");
  return {
    ...actual,
    VariableSelector: ({ value, onChange, disabled }: any) => (
      <select data-testid="variable-selector" value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
        <option value="">(none)</option>
        <option value="orders">orders</option>
        <option value="otherOrders">otherOrders</option>
      </select>
    ),
    DataTypeSelector: ({ value, onChange, disabled }: any) => (
      <select data-testid="data-type-selector" value={value?.kind ?? "string"} disabled={disabled} onChange={event => onChange?.({ kind: event.currentTarget.value })}>
        <option value="string">string</option>
        <option value="integer">integer</option>
      </select>
    ),
  };
});

afterEach(() => cleanup());

describe("ActionActivityForm createList/changeList", () => {
  it("patches createList output and element type", () => {
    const onPatch = vi.fn();
    render(<ActionActivityForm schema={schema()} object={createListObject()} issues={[]} onPatch={onPatch} />);

    fireEvent.change(screen.getByDisplayValue("orders"), { target: { value: "newOrders" } });
    fireEvent.change(screen.getByTestId("data-type-selector"), { target: { value: "integer" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ outputListVariableName: "newOrders", listVariableName: "newOrders" }),
      }),
    }));
    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ elementType: { kind: "integer" }, itemType: { kind: "integer" } }),
      }),
    }));
  });

  it("patches changeList operation", () => {
    const onPatch = vi.fn();
    render(<ActionActivityForm schema={schema()} object={changeListObject()} issues={[]} onPatch={onPatch} />);

    fireEvent.change(screen.getByDisplayValue("add"), { target: { value: "addAll" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ operation: "addAll" }),
      }),
    }));
  });

  it("supports addAll/removeAll source list plus duplicate and mutation switches", () => {
    const onPatch = vi.fn();
    render(<ActionActivityForm schema={schema()} object={changeListObject({ operation: "addAll" })} issues={[]} onPatch={onPatch} />);

    const selectors = screen.getAllByTestId("variable-selector");
    fireEvent.change(selectors[1], { target: { value: "otherOrders" } });
    fireEvent.click(screen.getByText("Allow Duplicates").parentElement!.querySelector("input")!);
    fireEvent.click(screen.getByText("Mutate In Place").parentElement!.querySelector("input")!);

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ sourceListVariableName: "otherOrders", sourceListVariable: "otherOrders" }),
      }),
    }));
    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ allowDuplicates: true }),
      }),
    }));
    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ mutateInPlace: false }),
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
      listOutputs: {
        orders: { name: "orders", dataType: { kind: "list", itemType: { kind: "object", entityQualifiedName: "Sales.Order" } }, source: { kind: "createList", objectId: "list", actionId: "list-action" }, scope: {} },
        otherOrders: { name: "otherOrders", dataType: { kind: "list", itemType: { kind: "object", entityQualifiedName: "Sales.Order" } }, source: { kind: "createList", objectId: "list2", actionId: "list2-action" }, scope: {} },
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
      officialType: kind === "createList" ? "Microflows$CreateListAction" : "Microflows$ChangeListAction",
      kind,
      caption: kind,
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: kind, availability: "supported" },
      ...patch,
    } as never,
  } as MicroflowActionActivity;
}

function createListObject(): MicroflowActionActivity {
  return activity("createList", {
    outputListVariableName: "orders",
    listVariableName: "orders",
    elementType: { kind: "string" },
    itemType: { kind: "string" },
    listType: "mutable",
  });
}

function changeListObject(patch: Record<string, unknown> = {}): MicroflowActionActivity {
  return activity("changeList", {
    targetListVariableName: "orders",
    operation: "add",
    allowDuplicates: false,
    mutateInPlace: true,
    ...patch,
  });
}
