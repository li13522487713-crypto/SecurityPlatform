// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { MicroflowExpression } from "../../schema/types";
import { ExclusiveSplitForm } from "./exclusive-split-form";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick, disabled }: any) => <button type="button" disabled={disabled} onClick={onClick}>{children}</button>,
  Input: ({ value, onChange, placeholder, disabled }: any) => <input placeholder={placeholder} value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  Select: ({ value, onChange, optionList, disabled }: any) => (
    <select value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
      {(optionList ?? []).map((option: any) => <option key={option.value} value={option.value}>{option.label}</option>)}
    </select>
  ),
  Space: ({ children }: any) => <div>{children}</div>,
  Tag: ({ children }: any) => <span>{children}</span>,
  TextArea: ({ value, disabled }: any) => <textarea value={value ?? ""} disabled={disabled} readOnly />,
  Tooltip: ({ children }: any) => <>{children}</>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
  },
}));

vi.mock("../expression", () => ({
  ExpressionEditor: ({ value, onChange, readonly }: any) => (
    <input
      aria-label="expression-editor"
      value={value?.raw ?? ""}
      disabled={readonly}
      onChange={event => onChange?.({ ...(value ?? {}), raw: event.currentTarget.value })}
    />
  ),
}));

vi.mock("../selectors", () => ({
  EnumerationSelector: ({ value, onChange, disabled }: any) => (
    <select value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
      <option value="">(none)</option>
      <option value="Sales.Status">Sales.Status</option>
    </select>
  ),
  VariableSelector: ({ value, onChange, readonly }: any) => (
    <select data-testid="variable-selector" value={value ?? ""} disabled={readonly} onChange={event => onChange?.(event.currentTarget.value)}>
      <option value="">(none)</option>
      <option value="$order">$order</option>
    </select>
  ),
}));

afterEach(() => cleanup());

describe("ExclusiveSplitForm rule decision", () => {
  it("switches decision type from expression to rule", () => {
    const patch = vi.fn();
    render(
      <ExclusiveSplitForm
        props={baseProps()}
        object={expressionDecision() as any}
        issues={[] as any}
        metadata={metadata() as any}
        variableIndex={{ all: [] } as any}
        patch={patch}
      />,
    );

    fireEvent.change(screen.getByDisplayValue("expression"), { target: { value: "rule" } });

    expect(patch).toHaveBeenCalledWith(expect.objectContaining({
      splitCondition: expect.objectContaining({
        kind: "rule",
        ruleQualifiedName: "",
        resultType: "boolean",
        parameterMappings: [],
      }),
    }));
  });

  it("adds parameter mapping for rule decision", () => {
    const patch = vi.fn();
    render(
      <ExclusiveSplitForm
        props={baseProps()}
        object={ruleDecision([]) as any}
        issues={[] as any}
        metadata={metadata() as any}
        variableIndex={{ all: [] } as any}
        patch={patch}
      />,
    );

    fireEvent.click(screen.getByText("Add parameter mapping"));

    expect(patch).toHaveBeenCalledWith(expect.objectContaining({
      splitCondition: expect.objectContaining({
        parameterMappings: [
          expect.objectContaining({
            parameterName: "param1",
          }),
        ],
      }),
    }));
  });

  it("updates rule parameter mapping name, source variable and expression", () => {
    const patch = vi.fn();
    render(
      <ExclusiveSplitForm
        props={baseProps()}
        object={ruleDecision([
          {
            parameterName: "param1",
            targetParameterName: "param1",
            argumentExpression: expr(""),
            expression: expr(""),
          },
        ]) as any}
        issues={[] as any}
        metadata={metadata() as any}
        variableIndex={{ all: [] } as any}
        patch={patch}
      />,
    );

    fireEvent.change(screen.getByPlaceholderText("Parameter name"), { target: { value: "Order" } });
    fireEvent.change(screen.getByTestId("variable-selector"), { target: { value: "$order" } });
    fireEvent.change(screen.getByLabelText("expression-editor"), { target: { value: "$order/Amount > 100" } });

    expect(patch).toHaveBeenCalledWith(expect.objectContaining({
      splitCondition: expect.objectContaining({
        parameterMappings: [
          expect.objectContaining({
            parameterName: "Order",
          }),
        ],
      }),
    }));
    expect(patch).toHaveBeenCalledWith(expect.objectContaining({
      splitCondition: expect.objectContaining({
        parameterMappings: [
          expect.objectContaining({
            sourceVariableName: "$order",
          }),
        ],
      }),
    }));
    expect(patch).toHaveBeenCalledWith(expect.objectContaining({
      splitCondition: expect.objectContaining({
        parameterMappings: [
          expect.objectContaining({
            argumentExpression: expect.objectContaining({
              raw: "$order/Amount > 100",
            }),
          }),
        ],
      }),
    }));
  });
});

function expr(raw: string): MicroflowExpression {
  return {
    raw,
    text: raw,
    referencedVariables: [],
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: [],
  };
}

function expressionDecision() {
  return {
    id: "decision-1",
    kind: "exclusiveSplit",
    officialType: "Microflows$ExclusiveSplit",
    splitCondition: {
      kind: "expression",
      expression: expr("$a > 0"),
      resultType: "boolean",
    },
    errorHandlingType: "rollback",
    editor: {},
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 120, height: 80 },
    ports: [],
  };
}

function ruleDecision(parameterMappings: unknown[]) {
  return {
    id: "decision-1",
    kind: "exclusiveSplit",
    officialType: "Microflows$ExclusiveSplit",
    splitCondition: {
      kind: "rule",
      ruleQualifiedName: "Sales.Rule1",
      resultType: "boolean",
      parameterMappings,
    },
    errorHandlingType: "rollback",
    editor: {},
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 120, height: 80 },
    ports: [],
  };
}

function metadata() {
  return {
    entities: [],
    enumerations: [],
    microflows: [
      {
        id: "mf-rule",
        name: "Rule1",
        qualifiedName: "Sales.Rule1",
        moduleName: "Sales",
        parameters: [],
        returnType: { kind: "boolean" },
      },
    ],
  };
}

function baseProps() {
  return {
    readonly: false,
    schema: {
      objectCollection: { objects: [] },
      flows: [],
      parameters: [],
    },
  };
}

