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
    useMicroflowMetadataCatalog: () => ({
      entities: [{ qualifiedName: "Sales.Order", attributes: [{ qualifiedName: "Sales.Order.Amount", type: { kind: "decimal" } }], associations: [] }],
      enumerations: [],
      microflows: [],
    }),
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
      </select>
    ),
    AttributeSelector: ({ value, onChange, disabled }: any) => (
      <select data-testid="attribute-selector" value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
        <option value="">(none)</option>
        <option value="Sales.Order.Amount">Sales.Order.Amount</option>
      </select>
    ),
  };
});

afterEach(() => cleanup());

describe("ActionActivityForm aggregate/filter/sort", () => {
  it("patches aggregate emptyListBehavior", () => {
    const onPatch = vi.fn();
    render(<ActionActivityForm schema={schema()} object={aggregateObject()} issues={[]} onPatch={onPatch} />);

    expect(screen.getByText("Empty List Behavior")).toBeTruthy();
    fireEvent.change(screen.getByDisplayValue("zero"), { target: { value: "error" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ emptyListBehavior: "error" }),
      }),
    }));
  });

  it("filter operation keeps boolean expression editable", () => {
    const onPatch = vi.fn();
    render(<ActionActivityForm schema={schema()} object={listOperationObject({ operation: "filter" })} issues={[]} onPatch={onPatch} />);

    const filterExpression = screen.getAllByLabelText("expression-editor")[0];
    fireEvent.change(filterExpression, { target: { value: "$order/Amount > 0" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({
          filterExpression: expect.objectContaining({ raw: "$order/Amount > 0" }),
          expression: expect.objectContaining({ raw: "$order/Amount > 0" }),
        }),
      }),
    }));
  });

  it("sort operation patches sort key direction", () => {
    const onPatch = vi.fn();
    render(<ActionActivityForm schema={schema()} object={listOperationObject({ operation: "sort" })} issues={[]} onPatch={onPatch} />);

    expect(screen.getByText("Sort Direction")).toBeTruthy();
    fireEvent.change(screen.getByDisplayValue("asc"), { target: { value: "desc" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({
          sortKeys: [expect.objectContaining({ direction: "desc" })],
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
      officialType: kind === "aggregateList" ? "Microflows$AggregateListAction" : "Microflows$ListOperationAction",
      kind,
      caption: kind,
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: kind, availability: "supported" },
      ...patch,
    } as never,
  } as MicroflowActionActivity;
}

function aggregateObject(): MicroflowActionActivity {
  return activity("aggregateList", {
    listVariableName: "orders",
    aggregateFunction: "count",
    outputVariableName: "count",
    resultType: { kind: "integer" },
    emptyListBehavior: "zero",
  });
}

function listOperationObject(patch: Record<string, unknown>): MicroflowActionActivity {
  return activity("listOperation", {
    leftListVariableName: "orders",
    sourceListVariableName: "orders",
    outputVariableName: "out",
    outputListVariableName: "out",
    filterExpression: { raw: "", inferredType: { kind: "boolean" }, referencedVariables: [], references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
    sortExpression: { raw: "", referencedVariables: [], references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
    ...patch,
  });
}
