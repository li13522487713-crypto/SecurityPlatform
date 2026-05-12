// @vitest-environment jsdom

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { MicroflowPropertyPanelProps } from "../types";
import { ObjectPanel } from "./object-panel";

const actionActivityFormMock = vi.fn(() => null);

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick, ...rest }: any) => <button type="button" onClick={onClick} {...rest}>{children}</button>,
  Input: ({ value, onChange, disabled }: any) => <input value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  Select: ({ value, onChange, disabled, optionList }: any) => (
    <select value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
      {(optionList ?? []).map((option: any) => <option key={option.value} value={option.value} disabled={option.disabled}>{option.label ?? option.value}</option>)}
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

vi.mock("../common", async () => {
  const actual = await vi.importActual<any>("../common");
  return {
    ...actual,
    ValidationIssueList: () => null,
    IssueSummaryBar: () => null,
    locateFieldByPath: () => false,
  };
});

vi.mock("../panel-shared", () => ({
  dataTypeLabel: (type: { kind?: string } | undefined) => type?.kind ?? "unknown",
  Field: ({ label, children }: any) => <section data-testid={`field-${String(label)}`}>{children}</section>,
  getObjectTabLabels: () => ({}),
  getObjectTabs: () => ["errorHandling"],
  Header: () => null,
  issuesFor: () => [],
  objectIconGlyph: () => "•",
  objectTitle: () => "Object",
  objectSubtitle: () => "Object",
  PropertyTabs: () => null,
  updateAction: (object: unknown, patch: Record<string, unknown>) => ({
    ...(object as Record<string, unknown>),
    action: { ...((object as any).action ?? {}), ...patch },
  }),
  updateObjectAdvanced: (object: unknown) => object,
  updateObjectDocumentation: (object: unknown) => object,
}));

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
vi.mock("./action-activity-form", () => ({
  ActionActivityForm: (props: any) => {
    actionActivityFormMock(props);
    return <div data-testid="action-activity-form" />;
  },
}));

afterEach(() => {
  cleanup();
  actionActivityFormMock.mockClear();
});

function baseProps(selectedObject: any): MicroflowPropertyPanelProps {
  return {
    selectedObject,
    selectedFlow: null,
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
      objectCollection: { id: "root", officialType: "Microflows$MicroflowObjectCollection", objects: [selectedObject], flows: [] },
      flows: [],
      security: { allowedModuleRoleIds: [], allowedRoleNames: [], applyEntityAccess: true },
      concurrency: { allowConcurrentExecution: true },
      exposure: { exportLevel: "module", markAsUsed: false },
      validation: { issues: [] },
      editor: { viewport: { x: 0, y: 0, zoom: 1 }, zoom: 1, selection: {} },
      audit: { version: "1", status: "draft" },
    } as any,
    validationIssues: [],
    onObjectChange: vi.fn(),
    onClose: vi.fn(),
  };
}

describe("ObjectPanel error handling tab", () => {
  it("delegates rest-call errorHandling tab to action form authentication view", () => {
    const selectedObject = {
      id: "rest-node",
      stableId: "rest-node",
      kind: "actionActivity",
      officialType: "Microflows$ActionActivity",
      caption: "REST",
      autoGenerateCaption: false,
      backgroundColor: "default",
      disabled: false,
      relativeMiddlePoint: { x: 0, y: 0 },
      size: { width: 178, height: 76 },
      editor: {},
      action: {
        id: "rest-action",
        kind: "restCall",
        officialType: "Microflows$RestCallAction",
        caption: "REST",
        errorHandlingType: "continue",
        documentation: "",
        editor: { category: "integration", iconKey: "rest", availability: "supported" },
        request: { method: "GET", urlExpression: { raw: "https://example.com" }, headers: [], queryParameters: [], body: { kind: "none" } },
        response: { handling: { kind: "ignore" } },
        timeoutSeconds: 30,
      },
    } as any;

    render(<ObjectPanel {...baseProps(selectedObject)} />);

    expect(screen.getByTestId("action-activity-form")).toBeTruthy();
    const call = actionActivityFormMock.mock.calls.at(-1)?.[0];
    expect(call?.activeTab).toBe("errorHandling");
    expect(call?.object?.action?.kind).toBe("restCall");
  });

  it("keeps Continue unavailable for createVariable actions", () => {
    const selectedObject = {
      id: "create-var-node",
      stableId: "create-var-node",
      kind: "actionActivity",
      officialType: "Microflows$ActionActivity",
      caption: "Create Variable",
      autoGenerateCaption: false,
      backgroundColor: "default",
      disabled: false,
      relativeMiddlePoint: { x: 0, y: 0 },
      size: { width: 178, height: 76 },
      editor: {},
      action: {
        id: "create-var-action",
        kind: "createVariable",
        officialType: "Microflows$CreateVariableAction",
        caption: "Create Variable",
        errorHandlingType: "rollback",
        documentation: "",
        editor: { category: "variable", iconKey: "variable", availability: "supported" },
        variableName: "approvalLevel",
        dataType: { kind: "string" },
        initialValue: { raw: "'L1'", references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
        readonly: false,
      },
    } as any;

    render(<ObjectPanel {...baseProps(selectedObject)} />);

    const select = screen.getByDisplayValue("Rollback") as HTMLSelectElement;
    const continueOption = [...select.options].find(option => option.value === "continue");
    expect(continueOption?.disabled).toBe(true);
  });

  it("shows SOAP fault guidance for webServiceCall actions", () => {
    const selectedObject = {
      id: "soap-node",
      stableId: "soap-node",
      kind: "actionActivity",
      officialType: "Microflows$ActionActivity",
      caption: "SOAP",
      autoGenerateCaption: false,
      backgroundColor: "default",
      disabled: false,
      relativeMiddlePoint: { x: 0, y: 0 },
      size: { width: 178, height: 76 },
      editor: {},
      action: {
        id: "soap-action",
        kind: "webServiceCall",
        officialType: "Microflows$WebServiceCallAction",
        caption: "SOAP",
        errorHandlingType: "customWithRollback",
        documentation: "",
        editor: { category: "integration", iconKey: "webServiceCall", availability: "supported" },
        endpoint: "https://soap.example.com/service",
        operation: "SubmitOrder",
        outputVariableName: "soapResult",
      },
    } as any;

    render(<ObjectPanel {...baseProps(selectedObject)} />);

    expect(screen.getByText("Web Service 错误路径中还可读取 $latestSoapFault。")).toBeTruthy();
  });
});
