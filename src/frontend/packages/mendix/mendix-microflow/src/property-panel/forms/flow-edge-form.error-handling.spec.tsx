// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { MicroflowPropertyPanelProps } from "../types";
import { FlowEdgeForm, nextEdgeKindForErrorHandlerToggle } from "./flow-edge-form";

vi.mock("@douyinfe/semi-ui", () => ({
  Input: ({ value, onChange, disabled }: any) => <input value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  InputNumber: ({ value, disabled }: any) => <input type="number" value={value ?? 0} disabled={disabled} readOnly />,
  Select: ({ value, onChange, disabled, optionList }: any) => (
    <select value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
      {(optionList ?? []).map((option: any) => <option key={option.value} value={option.value}>{option.label ?? option.value}</option>)}
    </select>
  ),
  Switch: ({ checked, onChange, disabled }: any) => (
    <input type="checkbox" checked={Boolean(checked)} disabled={disabled} onChange={event => onChange?.(event.currentTarget.checked)} />
  ),
  TextArea: ({ value, onChange, disabled }: any) => <textarea value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  Tooltip: ({ children }: any) => <>{children}</>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
  },
}));

vi.mock("../../metadata", () => ({
  EMPTY_MICROFLOW_METADATA_CATALOG: {},
  useMetadataStatus: () => ({ loading: false, error: null }),
  useMicroflowMetadataCatalog: () => undefined,
}));

vi.mock("../../flowgram/adapters/flowgram-case-options", () => ({
  caseValueKey: () => "case",
  getCaseEditorKind: () => undefined,
  getCaseOptionsForSource: () => [],
}));

vi.mock("../../schema/utils", () => ({
  collectLoopObjects: () => [],
  getDecisionBranchConflicts: () => [],
  getLoopFlowKind: () => "none",
}));

vi.mock("../common", () => ({
  ValidationIssueList: () => null,
}));

vi.mock("../panel-shared", () => ({
  Field: ({ label, children }: any) => <section data-testid={`field-${String(label)}`}>{children}</section>,
  Header: () => null,
  PropertyTabs: () => null,
  flowPatch: (flow: any, patch: any) => ({ ...flow, ...patch }),
  getFlowTabs: () => ["properties"],
  issuesFor: () => [],
  objectName: (_schema: any, objectId: string) => objectId,
}));

afterEach(() => cleanup());

function buildProps(onFlowChange = vi.fn()): MicroflowPropertyPanelProps {
  const selectedFlow = {
    id: "flow-error",
    stableId: "flow-error",
    kind: "sequence",
    officialType: "Microflows$SequenceFlow",
    originObjectId: "action-1",
    destinationObjectId: "error-1",
    originConnectionIndex: 0,
    destinationConnectionIndex: 0,
    isErrorHandler: true,
    caseValues: [],
    line: {
      kind: "orthogonal",
      points: [],
      routing: { mode: "auto", bendPoints: [] },
      style: { strokeType: "solid", strokeWidth: 2, arrow: "target" },
    },
    editor: {
      edgeKind: "errorHandler",
      label: "Error",
      description: "",
    },
  } as any;

  return {
    selectedObject: null,
    selectedFlow,
    schema: {
      id: "mf",
      stableId: "mf",
      schemaVersion: "1.0.0",
      mendixProfile: "mx11",
      name: "MF",
      displayName: "MF",
      moduleId: "module-a",
      parameters: [],
      returnType: { kind: "void" },
      objectCollection: { id: "root", officialType: "Microflows$MicroflowObjectCollection", objects: [], flows: [] },
      flows: [selectedFlow],
      security: { allowedModuleRoleIds: [], allowedRoleNames: [], applyEntityAccess: true },
      concurrency: { allowConcurrentExecution: true },
      exposure: { exportLevel: "module", markAsUsed: false },
      validation: { issues: [] },
      editor: { viewport: { x: 0, y: 0, zoom: 1 }, zoom: 1, selection: {} },
      audit: { version: "1", status: "draft" },
    } as any,
    validationIssues: [],
    onObjectChange: vi.fn(),
    onFlowChange,
    onClose: vi.fn(),
  };
}

describe("FlowEdgeForm error handling", () => {
  it("normalizes error-handler edge kind back to sequence when toggle is turned off", () => {
    const onFlowChange = vi.fn();
    render(<FlowEdgeForm {...buildProps(onFlowChange)} />);

    const field = screen.getByTestId("field-Error Handler");
    const checkbox = field.querySelector("input[type='checkbox']") as HTMLInputElement | null;
    expect(checkbox).toBeTruthy();
    fireEvent.click(checkbox!);

    expect(onFlowChange).toHaveBeenCalledWith(
      "flow-error",
      expect.objectContaining({
        isErrorHandler: false,
        editor: expect.objectContaining({
          edgeKind: "sequence",
        }),
      }),
    );
  });

  it("shows $latestError as the default error variable name", () => {
    render(<FlowEdgeForm {...buildProps()} />);

    const field = screen.getByTestId("field-Error variable name");
    const input = field.querySelector("input") as HTMLInputElement | null;
    expect(input?.value).toBe("$latestError");
  });
});

describe("nextEdgeKindForErrorHandlerToggle", () => {
  it("preserves non-error sequence semantics and clears stale errorHandler kind", () => {
    expect(nextEdgeKindForErrorHandlerToggle("sequence", true)).toBe("errorHandler");
    expect(nextEdgeKindForErrorHandlerToggle("errorHandler", false)).toBe("sequence");
    expect(nextEdgeKindForErrorHandlerToggle("decisionCondition", false)).toBe("decisionCondition");
  });
});
