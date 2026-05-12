// @vitest-environment jsdom

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { EventNodesForm } from "./event-nodes-form";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick, disabled }: any) => <button type="button" disabled={disabled} onClick={onClick}>{children}</button>,
  Input: ({ value, disabled }: any) => <input value={value ?? ""} disabled={disabled} readOnly />,
  Select: ({ value, onChange, optionList, disabled }: any) => (
    <select value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
      {(optionList ?? []).map((option: any) => <option key={option.value} value={option.value}>{option.label ?? option.value}</option>)}
    </select>
  ),
  Space: ({ children }: any) => <div>{children}</div>,
  Switch: ({ checked, onChange, disabled }: any) => <input type="checkbox" checked={Boolean(checked)} disabled={disabled} onChange={event => onChange?.(event.currentTarget.checked)} />,
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
}));

vi.mock("../panel-shared", () => ({
  expression: (raw = "", inferredType?: unknown) => ({ raw, inferredType, references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] }),
  Field: ({ label, children }: any) => <section data-testid={`field-${String(label)}`}>{children}</section>,
}));

vi.mock("../common", () => ({
  FieldError: ({ issues }: any) => <>{(issues ?? []).map((issue: any) => <span key={issue.code ?? issue.message}>{issue.message ?? issue.code}</span>)}</>,
}));

afterEach(() => cleanup());

describe("EventNodesForm start/end/control events", () => {
  it("shows start-event parameter editor", () => {
    render(
      <EventNodesForm
        props={{
          readonly: false,
          schema: {
            returnType: { kind: "void" },
            flows: [{ id: "flow-start-end", originObjectId: "start-1", destinationObjectId: "end-1", kind: "sequence" }],
            parameters: [{
              id: "param-customer",
              stableId: "param-customer",
              name: "Customer",
              dataType: { kind: "string" },
              type: { kind: "primitive", name: "string" },
              required: true,
            }],
            objectCollection: { objects: [{ id: "start-1", kind: "startEvent", trigger: { type: "manual" } }, { id: "end-1", kind: "endEvent" }] },
          } as any,
          onSchemaChange: vi.fn(),
        } as any}
        object={{
          id: "start-1",
          kind: "startEvent",
          trigger: { type: "manual" },
        } as any}
        issues={[] as any}
        metadata={{} as any}
        variableIndex={{} as any}
        patch={vi.fn()}
      />,
    );

    expect(screen.getByTestId("field-Input Parameters")).toBeTruthy();
    expect(screen.getByText("Customer [string]")).toBeTruthy();
    expect(screen.getByTestId("field-Parameter Name")).toBeTruthy();
    expect(screen.getByDisplayValue("Customer")).toBeTruthy();
    expect(screen.getByTestId("field-Data Type")).toBeTruthy();
    expect(screen.getByTestId("field-Required")).toBeTruthy();
  });

  it("surfaces end-event return editor", () => {
    render(
      <EventNodesForm
        props={{
          readonly: false,
          schema: {
            returnType: { kind: "string" },
            flows: [{ id: "flow-start-end", originObjectId: "start-1", destinationObjectId: "end-1", kind: "sequence" }],
            objectCollection: { objects: [{ id: "start-1", kind: "startEvent", trigger: { type: "manual" } }, { id: "end-1", kind: "endEvent", returnValue: { raw: "'done'" } }] },
          } as any,
        } as any}
        object={{
          id: "end-1",
          kind: "endEvent",
          returnValue: { raw: "'done'" },
        } as any}
        issues={[] as any}
        metadata={{} as any}
        variableIndex={{} as any}
        patch={vi.fn()}
      />,
    );

    expect(screen.getByTestId("field-Return Type")).toBeTruthy();
    expect(screen.getByTestId("field-Return Value")).toBeTruthy();
    expect(screen.getByLabelText("expression-editor")).toBeTruthy();
  });

  it("surfaces break-event validator issues together with loop warnings", () => {
    render(
      <EventNodesForm
        props={{
          readonly: false,
          schema: {
            returnType: { kind: "void" },
            flows: [],
            objectCollection: { objects: [{ id: "break-1", kind: "breakEvent" }] },
          } as any,
        } as any}
        object={{
          id: "break-1",
          kind: "breakEvent",
        } as any}
        issues={[
          { code: "MF_BREAK_OUTSIDE_LOOP", message: "Break/Continue must be inside LoopedActivity.objectCollection unless Break explicitly targets a Loop exit." },
        ] as any}
        metadata={{} as any}
        variableIndex={{} as any}
        patch={vi.fn()}
      />,
    );

    expect(screen.getByText("Break/Continue must be inside LoopedActivity.objectCollection unless Break explicitly targets a Loop exit.")).toBeTruthy();
    expect(screen.getByText("No Loop exists in the current microflow.")).toBeTruthy();
    expect(screen.getByText("This control event is not inside a Loop body. Full containment validation will be completed in Stage 20.")).toBeTruthy();
  });
});

describe("EventNodesForm error event", () => {
  it("shows error-event scope and rollback guidance", () => {
    render(
      <EventNodesForm
        props={{
          readonly: false,
          schema: {
            returnType: { kind: "void" },
            objectCollection: { objects: [{ id: "error-1", kind: "errorEvent" }] },
          } as any,
        } as any}
        object={{
          id: "error-1",
          kind: "errorEvent",
          error: {
            sourceVariableName: "$latestError",
            messageExpression: { raw: "$latestError/Message" },
          },
        } as any}
        issues={[] as any}
        metadata={{} as any}
        variableIndex={{} as any}
        patch={vi.fn()}
      />,
    );

    expect(screen.getByTestId("field-Incoming Flows")).toBeTruthy();
    expect(screen.getByTestId("field-Outgoing Flows")).toBeTruthy();
    expect(screen.getByDisplayValue("$latestError")).toBeTruthy();
    expect(screen.getByText("Error Event only works at the end of an error handler flow and typically rethrows $latestError to the caller.")).toBeTruthy();
    expect(screen.getByText("If provided, this message becomes the rethrown error summary. Triggering Error Event rolls back the transaction.")).toBeTruthy();
    expect(screen.getByText("Valid when reached only by an error handler SequenceFlow and used as a terminal rethrow node.")).toBeTruthy();
  });

  it("surfaces error-event legality issues", () => {
    render(
      <EventNodesForm
        props={{
          readonly: false,
          schema: {
            returnType: { kind: "void" },
            objectCollection: { objects: [{ id: "error-1", kind: "errorEvent" }] },
          } as any,
        } as any}
        object={{
          id: "error-1",
          kind: "errorEvent",
          error: {
            sourceVariableName: "$latestError",
            messageExpression: { raw: "" },
          },
        } as any}
        issues={[
          { code: "MF_ERROR_EVENT_OUT_OF_SCOPE", message: "ErrorEvent must be in error handler scope (requires incoming isErrorHandler SequenceFlow)." },
          { code: "MF_ERROR_EVENT_OUTGOING", message: "ErrorEvent cannot have outgoing flows." },
        ] as any}
        metadata={{} as any}
        variableIndex={{} as any}
        patch={vi.fn()}
      />,
    );

    expect(screen.getByText("ErrorEvent must be in error handler scope (requires incoming isErrorHandler SequenceFlow).")).toBeTruthy();
    expect(screen.getByText("ErrorEvent cannot have outgoing flows.")).toBeTruthy();
  });
});
