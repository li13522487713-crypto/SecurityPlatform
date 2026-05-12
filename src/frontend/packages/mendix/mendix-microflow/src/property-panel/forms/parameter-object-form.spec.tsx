// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { ParameterObjectForm } from "./parameter-object-form";

vi.mock("@douyinfe/semi-ui", () => ({
  Input: ({ value, onChange, disabled }: any) => <input value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  Switch: ({ checked, onChange, disabled }: any) => <input type="checkbox" checked={Boolean(checked)} disabled={disabled} onChange={event => onChange?.(event.currentTarget.checked)} />,
  TextArea: ({ value, onChange, disabled }: any) => <textarea value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  Tooltip: ({ children }: any) => <>{children}</>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
  },
}));

vi.mock("../selectors", () => ({
  DataTypeSelector: ({ value }: any) => <div data-testid="data-type-selector">{value?.kind}</div>,
}));

vi.mock("../common", async () => {
  const actual = await vi.importActual<any>("../common");
  return {
    ...actual,
    FieldError: ({ issues }: any) => <span data-testid="field-error">{issues?.length ?? 0}</span>,
  };
});

vi.mock("../panel-shared", () => ({
  expression: (raw = "", inferredType?: unknown) => ({ raw, inferredType, references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] }),
  Field: ({ label, children, issues }: any) => <section data-testid={`field-${String(label)}`} data-issues={issues?.length ?? 0}>{children}</section>,
}));

afterEach(() => cleanup());

describe("ParameterObjectForm", () => {
  it("uses Optional/Documentation semantics and shows entity for object parameters", () => {
    const onSchemaChange = vi.fn();
    render(
      <ParameterObjectForm
        props={{
          readonly: false,
          onSchemaChange,
          schema: {
            parameters: [
              {
                id: "param-order",
                stableId: "param-order",
                name: "Order",
                dataType: { kind: "object", entityQualifiedName: "Sales.Order" },
                required: true,
                documentation: "Order input",
              },
            ],
            objectCollection: {
              id: "root",
              officialType: "Microflows$MicroflowObjectCollection",
              objects: [
                {
                  id: "param-node",
                  kind: "parameterObject",
                  parameterId: "param-order",
                  parameterName: "Order",
                },
              ],
              flows: [],
            },
          } as any,
        } as any}
        object={{
          id: "param-node",
          kind: "parameterObject",
          parameterId: "param-order",
          parameterName: "Order",
        } as any}
        issues={[] as any}
        parameter={{
          id: "param-order",
          stableId: "param-order",
          name: "Order",
          dataType: { kind: "object", entityQualifiedName: "Sales.Order" },
          required: true,
          documentation: "Order input",
        }}
      />,
    );

    expect(screen.getByTestId("field-Optional")).toBeTruthy();
    expect(screen.getByTestId("field-Documentation")).toBeTruthy();
    expect(screen.getByTestId("field-Entity")).toBeTruthy();
    expect(screen.getByDisplayValue("Sales.Order")).toBeTruthy();

    const optionalSwitch = screen.getByTestId("field-Optional").querySelector("input[type='checkbox']") as HTMLInputElement;
    fireEvent.click(optionalSwitch);

    expect(onSchemaChange).toHaveBeenCalledTimes(1);
    expect(onSchemaChange.mock.calls[0]?.[0]?.parameters?.[0]?.required).toBe(false);
    expect(onSchemaChange.mock.calls[0]?.[1]).toBe("updateParameter");

    const documentation = screen.getByTestId("field-Documentation").querySelector("textarea") as HTMLTextAreaElement;
    fireEvent.change(documentation, { target: { value: "Updated docs" } });

    expect(onSchemaChange).toHaveBeenCalledTimes(2);
    expect(onSchemaChange.mock.calls[1]?.[0]?.parameters?.[0]?.documentation).toBe("Updated docs");
    expect(onSchemaChange.mock.calls[1]?.[0]?.parameters?.[0]?.description).toBe("Updated docs");
    expect(screen.getByText("Parameter rename rewrites parameter-scoped expressions and direct variable reference fields; unrelated shadowed loop/local variables stay unchanged.")).toBeTruthy();
  });

  it("shows list item type details for list parameters", () => {
    render(
      <ParameterObjectForm
        props={{
          readonly: false,
          schema: {
            parameters: [
              {
                id: "param-order-list",
                stableId: "param-order-list",
                name: "OrderList",
                dataType: { kind: "list", itemType: { kind: "object", entityQualifiedName: "Sales.Order" } },
                required: false,
              },
            ],
            objectCollection: {
              id: "root",
              officialType: "Microflows$MicroflowObjectCollection",
              objects: [
                {
                  id: "param-node-list",
                  kind: "parameterObject",
                  parameterId: "param-order-list",
                  parameterName: "OrderList",
                },
              ],
              flows: [],
            },
          } as any,
        } as any}
        object={{
          id: "param-node-list",
          kind: "parameterObject",
          parameterId: "param-order-list",
          parameterName: "OrderList",
        } as any}
        issues={[] as any}
        parameter={{
          id: "param-order-list",
          stableId: "param-order-list",
          name: "OrderList",
          dataType: { kind: "list", itemType: { kind: "object", entityQualifiedName: "Sales.Order" } },
          required: false,
        }}
      />,
    );

    expect(screen.getByDisplayValue("Sales.Order")).toBeTruthy();
  });

  it("wires default value expression issues into the field", () => {
    render(
      <ParameterObjectForm
        props={{
          readonly: false,
          schema: {
            parameters: [
              {
                id: "param-default",
                stableId: "param-default",
                name: "Amount",
                dataType: { kind: "decimal" },
                required: false,
                defaultValue: { raw: "$missingDefault" },
              },
            ],
            objectCollection: {
              id: "root",
              officialType: "Microflows$MicroflowObjectCollection",
              objects: [
                {
                  id: "param-node-default",
                  kind: "parameterObject",
                  parameterId: "param-default",
                  parameterName: "Amount",
                },
              ],
              flows: [],
            },
          } as any,
        } as any}
        object={{
          id: "param-node-default",
          kind: "parameterObject",
          parameterId: "param-default",
          parameterName: "Amount",
        } as any}
        issues={[{
          code: "MF_EXPR_UNKNOWN_VARIABLE",
          severity: "error",
          fieldPath: "parameters.param-default.defaultValue",
          objectId: "param-node-default",
          parameterId: "param-default",
          message: "Variable \"$missingDefault\" is not available in this context.",
        }] as any}
        parameter={{
          id: "param-default",
          stableId: "param-default",
          name: "Amount",
          dataType: { kind: "decimal" },
          required: false,
          defaultValue: { raw: "$missingDefault" },
        } as any}
      />,
    );

    expect(screen.getByTestId("field-Default Value Expression").getAttribute("data-issues")).toBe("1");
  });
});
