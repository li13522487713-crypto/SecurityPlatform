// @vitest-environment jsdom

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { MicroflowPropertyPanelProps } from "../types";
import { FlowEdgeForm } from "./flow-edge-form";

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
  useMicroflowMetadataCatalog: () => ({}),
}));

vi.mock("../../flowgram/adapters/flowgram-case-options", () => ({
  caseValueKey: (caseValue: { kind?: string }) => String(caseValue?.kind ?? ""),
  getCaseEditorKind: () => "objectType",
  getCaseOptionsForSource: () => [
    {
      key: "empty",
      label: "(empty)",
      caseValue: { kind: "empty", officialType: "Microflows$NoCase" },
      disabled: false,
    },
  ],
}));

vi.mock("../../schema/utils", () => ({
  collectLoopObjects: () => [],
  getDecisionBranchConflicts: () => [],
  getLoopFlowKind: () => "none",
}));

vi.mock("../common", () => ({
  ValidationIssueList: () => null,
  IssueSummaryBar: () => null,
  locateFieldByPath: () => false,
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

describe("FlowEdgeForm object type case", () => {
  it("normalizes legacy fallback branches to the empty selector option", () => {
    render(<FlowEdgeForm {...buildProps()} />);

    const field = screen.getByTestId("field-Object Type Case");
    const select = field.querySelector("select") as HTMLSelectElement | null;
    expect(select?.value).toBe("empty");
  });
});

function buildProps(): MicroflowPropertyPanelProps {
  const selectedFlow = {
    id: "flow-object-type",
    stableId: "flow-object-type",
    kind: "sequence",
    officialType: "Microflows$SequenceFlow",
    originObjectId: "inheritance-1",
    destinationObjectId: "activity-1",
    originConnectionIndex: 0,
    destinationConnectionIndex: 0,
    isErrorHandler: false,
    caseValues: [{ kind: "fallback", officialType: "Microflows$NoCase" }],
    line: {
      kind: "orthogonal",
      points: [],
      routing: { mode: "auto", bendPoints: [] },
      style: { strokeType: "solid", strokeWidth: 2, arrow: "target" },
    },
    editor: {
      edgeKind: "objectTypeCondition",
      label: "(empty)",
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
    onFlowChange: vi.fn(),
    onClose: vi.fn(),
  };
}
