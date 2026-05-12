// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { LoopNodeForm } from "./loop-node-form";

vi.mock("@douyinfe/semi-ui", () => ({
  Input: ({ value, onChange, disabled, placeholder }: any) => <input value={value ?? ""} disabled={disabled} placeholder={placeholder} onChange={event => onChange?.(event.currentTarget.value)} />,
  Select: ({ value, onChange, disabled, optionList, multiple, filter, showClear, placeholder }: any) => (
    <select
      value={value ?? ""}
      disabled={disabled}
      multiple={Boolean(multiple)}
      data-show-clear={showClear ? "true" : "false"}
      aria-label={placeholder}
      onChange={event => onChange?.(multiple ? [...event.currentTarget.selectedOptions].map(option => option.value) : event.currentTarget.value)}
    >
      {(optionList ?? []).map((option: any) => <option key={option.value} value={option.value} disabled={option.disabled}>{option.label ?? option.value}</option>)}
    </select>
  ),
  Space: ({ children }: any) => <div>{children}</div>,
  TextArea: ({ value, disabled }: any) => <textarea value={value ?? ""} disabled={disabled} readOnly />,
  Tooltip: ({ children }: any) => <>{children}</>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
  },
}));

vi.mock("../expression", () => ({
  ExpressionEditor: ({ value, readonly }: any) => <input aria-label="expression-editor" value={value?.raw ?? ""} disabled={readonly} readOnly />,
}));

vi.mock("../selectors", () => ({
  DataTypeSelector: () => null,
  VariableSelector: () => null,
}));

vi.mock("../panel-shared", () => ({
  expression: (raw = "", inferredType?: unknown) => ({ raw, inferredType, references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] }),
  Field: ({ label, children }: any) => <section data-testid={`field-${String(label)}`}>{children}</section>,
}));

vi.mock("../common", async () => {
  const actual = await vi.importActual<any>("../common");
  return {
    ...actual,
    FieldError: ({ issues }: any) => <>{(issues ?? []).map((issue: any) => <span key={issue.code ?? issue.message}>{issue.message ?? issue.code}</span>)}</>,
    FieldRow: ({ label, children }: any) => <section data-testid={`field-${String(label)}`}>{children}</section>,
    VariableNameInput: ({ value, onChange }: any) => <input aria-label="variable-name-input" value={value ?? ""} onChange={event => onChange?.(event.currentTarget.value)} />,
  };
});

afterEach(() => cleanup());

describe("LoopNodeForm error handling", () => {
  it("uses official error handling labels and continue guidance for loop nodes", () => {
    render(
      <LoopNodeForm
        props={{
          readonly: false,
          schema: {
            parameters: [],
            objectCollection: {
              id: "root",
              officialType: "Microflows$MicroflowObjectCollection",
              objects: [
                {
                  id: "loop-1",
                  kind: "loopedActivity",
                  errorHandlingType: "continue",
                  objectCollection: { id: "loop-1-body", officialType: "Microflows$MicroflowObjectCollection", objects: [], flows: [] },
                  loopSource: {
                    kind: "iterableList",
                    officialType: "Microflows$IterableList",
                    listVariableName: "$OrderList",
                    iteratorVariableName: "IteratorOrder",
                    currentIndexVariableName: "$currentIndex",
                  },
                },
              ],
              flows: [],
            },
            flows: [],
          } as any,
        } as any}
        object={{
          id: "loop-1",
          kind: "loopedActivity",
          errorHandlingType: "continue",
          objectCollection: { id: "loop-1-body", officialType: "Microflows$MicroflowObjectCollection", objects: [], flows: [] },
          loopSource: {
            kind: "iterableList",
            officialType: "Microflows$IterableList",
            listVariableName: "$OrderList",
            iteratorVariableName: "IteratorOrder",
            currentIndexVariableName: "$currentIndex",
          },
        } as any}
        issues={[] as any}
        metadata={{} as any}
        variableIndex={{ all: [], byName: {}, listOutputs: {} } as any}
        patch={vi.fn()}
      />,
    );

    const select = screen.getByDisplayValue("Continue") as HTMLSelectElement;
    const optionLabels = [...select.options].map(option => option.textContent);
    expect(optionLabels).toEqual([
      "Rollback",
      "Custom with Rollback",
      "Custom without Rollback",
      "Continue",
    ]);
    const continueOption = [...select.options].find(option => option.value === "continue");
    expect(continueOption?.disabled).toBe(false);
    expect(screen.getByText("发生错误后忽略当前错误并继续 Loop 后续执行；可通过 $latestError 检查错误详情。")).toBeTruthy();
  });

  it("renames iterator variables through schema change and rewrites loop-scoped expressions", () => {
    const onSchemaChange = vi.fn();
    render(
      <LoopNodeForm
        props={{
          readonly: false,
          onSchemaChange,
          schema: {
            parameters: [],
            objectCollection: {
              id: "root",
              officialType: "Microflows$MicroflowObjectCollection",
              objects: [
                {
                  id: "loop-1",
                  kind: "loopedActivity",
                  errorHandlingType: "rollback",
                  objectCollection: {
                    id: "loop-1-body",
                    officialType: "Microflows$MicroflowObjectCollection",
                    objects: [
                      {
                        id: "end-1",
                        kind: "endEvent",
                        returnValue: { raw: "$item/Name", references: { variables: ["$item"], entities: [], attributes: ["Name"], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
                      },
                    ],
                    flows: [],
                  },
                  loopSource: {
                    kind: "iterableList",
                    officialType: "Microflows$IterableList",
                    listVariableName: "$OrderList",
                    iteratorVariableName: "item",
                    currentIndexVariableName: "$currentIndex",
                  },
                },
              ],
              flows: [],
            },
            flows: [],
          } as any,
        } as any}
        object={{
          id: "loop-1",
          kind: "loopedActivity",
          errorHandlingType: "rollback",
          objectCollection: {
            id: "loop-1-body",
            officialType: "Microflows$MicroflowObjectCollection",
            objects: [
              {
                id: "end-1",
                kind: "endEvent",
                returnValue: { raw: "$item/Name", references: { variables: ["$item"], entities: [], attributes: ["Name"], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
              },
            ],
            flows: [],
          },
          loopSource: {
            kind: "iterableList",
            officialType: "Microflows$IterableList",
            listVariableName: "$OrderList",
            iteratorVariableName: "item",
            currentIndexVariableName: "$currentIndex",
          },
        } as any}
        issues={[] as any}
        metadata={{} as any}
        variableIndex={{ all: [], byName: {}, listOutputs: {} } as any}
        patch={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByLabelText("variable-name-input"), { target: { value: "orderItem" } });

    expect(onSchemaChange).toHaveBeenCalledTimes(1);
    const [nextSchema, reason] = onSchemaChange.mock.calls[0] ?? [];
    expect(reason).toBe("renameLoopIteratorVariable");
    expect(nextSchema.objectCollection.objects[0].loopSource.iteratorVariableName).toBe("orderItem");
    expect(nextSchema.objectCollection.objects[0].objectCollection.objects[0].returnValue.raw).toBe("$orderItem/Name");
  });
});
