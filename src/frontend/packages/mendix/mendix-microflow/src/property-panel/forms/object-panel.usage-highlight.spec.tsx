// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { sampleMicroflowSchema } from "../../schema/sample";
import type { MicroflowPropertyPanelProps } from "../types";
import { ObjectPanel } from "./object-panel";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick, ...rest }: any) => <button type="button" onClick={onClick} {...rest}>{children}</button>,
  Input: ({ value, onChange, disabled }: any) => <input value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  Select: ({ value, onChange, disabled, optionList }: any) => (
    <select value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
      {(optionList ?? []).map((option: any) => <option key={option.value} value={option.value}>{option.label ?? option.value}</option>)}
    </select>
  ),
  Space: ({ children }: any) => <div>{children}</div>,
  Switch: ({ checked, onChange, disabled }: any) => (
    <input type="checkbox" checked={Boolean(checked)} disabled={disabled} onChange={event => onChange?.(event.currentTarget.checked)} />
  ),
  Tag: ({ children }: any) => <span>{children}</span>,
  TextArea: ({ value, onChange, disabled }: any) => <textarea value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  Tooltip: ({ children }: any) => <>{children}</>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
  },
}));

vi.mock("../../metadata", async () => {
  const actual = await vi.importActual<any>("../../metadata");
  return {
    ...actual,
    useMicroflowMetadataCatalog: () => undefined,
    useMetadataStatus: () => ({ version: "test-version" }),
  };
});

vi.mock("../common", () => ({
  ValidationIssueList: () => null,
  IssueSummaryBar: () => null,
  locateFieldByPath: () => false,
}));

vi.mock("../panel-shared", () => ({
  dataTypeLabel: (type: { kind?: string } | undefined) => type?.kind ?? "unknown",
  Field: ({ label, children }: any) => <section data-testid={`field-${String(label)}`}>{children}</section>,
  getObjectTabLabels: () => ({}),
  getObjectTabs: () => ["output"],
  Header: () => null,
  issuesFor: () => [],
  objectIconGlyph: () => "•",
  objectTitle: () => "Object",
  objectSubtitle: () => "Object",
  PropertyTabs: () => null,
  updateAction: (object: unknown) => object,
  updateObjectAdvanced: (object: unknown) => object,
  updateObjectDocumentation: (object: unknown) => object,
}));

vi.mock("./action-activity-form", () => ({ ActionActivityForm: () => null }));
vi.mock("./annotation-object-form", () => ({ AnnotationObjectForm: () => null }));
vi.mock("./event-nodes-form", () => ({ EventNodesForm: () => null }));
vi.mock("./exclusive-split-form", () => ({ ExclusiveSplitForm: () => null }));
vi.mock("./generic-action-fields-form", () => ({ genericOutputSummary: () => "" }));
vi.mock("./inheritance-split-form", () => ({ InheritanceSplitForm: () => null }));
vi.mock("./loop-node-form", () => ({ LoopNodeForm: () => null }));
vi.mock("./merge-node-form", () => ({ MergeNodeForm: () => null }));
vi.mock("./object-base-form", () => ({ ObjectBaseForm: () => null }));
vi.mock("./parameter-object-form", () => ({ ParameterObjectForm: () => null }));
vi.mock("./parallel-gateway-form", () => ({ ParallelGatewayForm: () => null }));
vi.mock("./inclusive-gateway-form", () => ({ InclusiveGatewayForm: () => null }));
vi.mock("./try-catch-form", () => ({ TryCatchForm: () => null }));
vi.mock("./error-handler-form", () => ({ ErrorHandlerForm: () => null }));

afterEach(() => cleanup());

describe("ObjectPanel variable usage highlight", () => {
  it("emits onHighlightVariableUsage when clicking output variable button", () => {
    const onHighlightVariableUsage = vi.fn();
    const selectedObject = sampleMicroflowSchema.objectCollection.objects.find(object => object.id === "param-order-id") ?? null;
    if (!selectedObject) {
      throw new Error("Expected sample schema to contain parameter object param-order-id.");
    }

    const props: MicroflowPropertyPanelProps = {
      selectedObject,
      selectedFlow: null,
      schema: sampleMicroflowSchema,
      validationIssues: [],
      onObjectChange: vi.fn(),
      onClose: vi.fn(),
      onHighlightVariableUsage,
    };

    render(<ObjectPanel {...props} />);

    fireEvent.click(screen.getByTestId("microflow-output-variable--orderId"));

    expect(onHighlightVariableUsage).toHaveBeenCalledTimes(1);
    expect(onHighlightVariableUsage).toHaveBeenCalledWith("orderId");
  });
});
