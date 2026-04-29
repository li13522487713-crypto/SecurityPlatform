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
        <option value="order">order</option>
      </select>
    ),
  };
});

afterEach(() => cleanup());

describe("ActionActivityForm listOperation", () => {
  it("exposes all production operations", () => {
    render(<ActionActivityForm schema={schema()} object={listOperationObject()} issues={[]} onPatch={vi.fn()} />);

    const operationSelect = screen.getByDisplayValue("union");
    const options = [...operationSelect.querySelectorAll("option")].map(option => option.value);

    expect(options).toEqual(expect.arrayContaining([
      "union",
      "intersect",
      "subtract",
      "contains",
      "equals",
      "isEmpty",
      "head",
      "tail",
      "find",
      "first",
      "last",
      "distinct",
      "reverse",
      "size",
    ]));
  });

  it("patches second list aliases for set operations", () => {
    const onPatch = vi.fn();
    render(<ActionActivityForm schema={schema()} object={listOperationObject()} issues={[]} onPatch={onPatch} />);

    const selectors = screen.getAllByTestId("variable-selector");
    fireEvent.change(selectors[1], { target: { value: "otherOrders" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({
          rightListVariableName: "otherOrders",
          secondListVariable: "otherOrders",
          secondListVariableName: "otherOrders",
        }),
      }),
    }));
  });

  it("shows item variable for contains/find operations", () => {
    const onPatch = vi.fn();
    render(<ActionActivityForm schema={schema()} object={listOperationObject({ operation: "contains" })} issues={[]} onPatch={onPatch} />);

    expect(screen.getByText("Item Variable")).toBeTruthy();
    const selectors = screen.getAllByTestId("variable-selector");
    fireEvent.change(selectors[1], { target: { value: "order" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({
          itemVariable: "order",
          itemVariableName: "order",
          objectVariableName: "order",
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
      listOutputs: {
        orders: { name: "orders", dataType: { kind: "list", itemType: { kind: "object", entityQualifiedName: "Sales.Order" } }, source: { kind: "createList", objectId: "list", actionId: "list-action" }, scope: {} },
      },
      objectOutputs: {
        order: { name: "order", dataType: { kind: "object", entityQualifiedName: "Sales.Order" }, source: { kind: "createObject", objectId: "obj", actionId: "obj-action" }, scope: {} },
      },
    },
    editor: { selection: {}, viewport: { x: 0, y: 0, zoom: 1 } },
  } as MicroflowAuthoringSchema;
}

function listOperationObject(patch: Record<string, unknown> = {}): MicroflowActionActivity {
  return {
    id: "list-operation-activity",
    stableId: "list-operation-activity",
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: "List Operation",
    documentation: "",
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 160, height: 80 },
    ports: [],
    autoGenerateCaption: false,
    backgroundColor: "default",
    action: {
      id: "list-operation-action",
      officialType: "Microflows$ListOperationAction",
      kind: "listOperation",
      caption: "List Operation",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: "listOperation", availability: "supported" },
      leftListVariableName: "orders",
      sourceListVariableName: "orders",
      operation: "union",
      outputVariableName: "out",
      outputListVariableName: "out",
      ...patch,
    } as never,
  } as MicroflowActionActivity;
}
