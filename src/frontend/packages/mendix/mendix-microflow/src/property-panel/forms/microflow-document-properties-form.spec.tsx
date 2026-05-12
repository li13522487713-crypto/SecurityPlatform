// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { MicroflowDocumentPropertiesForm } from "./microflow-document-properties-form";

vi.mock("@douyinfe/semi-ui", () => ({
  Input: ({ value, onChange, disabled, placeholder }: any) => <input value={value ?? ""} placeholder={placeholder} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  Select: ({ value, onChange, disabled, optionList }: any) => (
    <select value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
      {(optionList ?? []).map((option: any) => <option key={option.value} value={option.value}>{option.label ?? option.value}</option>)}
    </select>
  ),
  Switch: ({ checked, onChange, disabled }: any) => <input type="checkbox" checked={Boolean(checked)} disabled={disabled} onChange={event => onChange?.(event.currentTarget.checked)} />,
  TextArea: ({ value, onChange, disabled, placeholder }: any) => <textarea value={value ?? ""} placeholder={placeholder} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  Tooltip: ({ children }: any) => <>{children}</>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
    Title: ({ children }: any) => <h1>{children}</h1>,
  },
}));

vi.mock("../panel-shared", () => ({
  dataTypeLabel: (type: { kind?: string } | undefined) => type?.kind ?? "unknown",
  Field: ({ label, children }: any) => <section data-testid={`field-${String(label)}`}>{children}</section>,
}));

afterEach(() => cleanup());

describe("MicroflowDocumentPropertiesForm", () => {
  it("patches export level, mark-as-used, and URL search parameters through schema updates", () => {
    const onSchemaChange = vi.fn();
    render(
      <MicroflowDocumentPropertiesForm
        {...({
          readonly: false,
          onSchemaChange,
          schema: {
            id: "mf-doc",
            stableId: "mf-doc",
            schemaVersion: "1.0.0",
            mendixProfile: "mx11",
            name: "ACT_ProcessOrder",
            displayName: "Process Order",
            moduleId: "sales-module",
            moduleName: "Sales",
            description: "Resource description",
            documentation: "",
            parameters: [],
            returnType: { kind: "void" },
            objectCollection: { id: "root", officialType: "Microflows$MicroflowObjectCollection", objects: [], flows: [] },
            flows: [],
            security: { applyEntityAccess: true, allowedModuleRoleIds: [], allowedRoleNames: [] },
            concurrency: { allowConcurrentExecution: true },
            exposure: {
              exportLevel: "module",
              markAsUsed: true,
              asMicroflowAction: { enabled: false },
              asWorkflowAction: { enabled: false },
              url: { enabled: true, path: "/orders/process", searchParameters: ["orderId"] },
            },
            validation: { issues: [] },
            editor: { viewport: { x: 0, y: 0, zoom: 1 }, zoom: 1, selection: {} },
            audit: { version: "1", status: "draft" },
          },
        }) as any}
      />,
    );

    const exportSelect = screen.getByTestId("field-Export Level").querySelector("select") as HTMLSelectElement;
    fireEvent.change(exportSelect, { target: { value: "public" } });
    expect(onSchemaChange.mock.calls[0]?.[0]?.exposure?.exportLevel).toBe("public");

    const markAsUsedSwitch = screen.getByTestId("field-Mark As Used").querySelector("input[type='checkbox']") as HTMLInputElement;
    fireEvent.click(markAsUsedSwitch);
    expect(onSchemaChange.mock.calls[1]?.[0]?.exposure?.markAsUsed).toBe(false);

    const workflowActionField = screen.getByTestId("field-Exposed as workflow action");
    const workflowActionSwitch = workflowActionField.querySelector("input[type='checkbox']") as HTMLInputElement;
    fireEvent.click(workflowActionSwitch);
    expect(onSchemaChange.mock.calls[2]?.[0]?.exposure?.asWorkflowAction?.enabled).toBe(true);

    const workflowActionInputs = workflowActionField.querySelectorAll("input");
    fireEvent.change(workflowActionInputs[1] as HTMLInputElement, { target: { value: "Workflow Process Order" } });
    expect(onSchemaChange.mock.calls[3]?.[0]?.exposure?.asWorkflowAction?.caption).toBe("Workflow Process Order");

    fireEvent.change(workflowActionInputs[2] as HTMLInputElement, { target: { value: "Workflow" } });
    expect(onSchemaChange.mock.calls[4]?.[0]?.exposure?.asWorkflowAction?.category).toBe("Workflow");

    const urlField = screen.getByTestId("field-URL exposure");
    const searchParametersInput = urlField.querySelectorAll("textarea")[0] as HTMLTextAreaElement;
    fireEvent.change(searchParametersInput, { target: { value: "orderId\ncustomerId, tenantId" } });
    expect(onSchemaChange.mock.calls[5]?.[0]?.exposure?.url?.searchParameters).toEqual(["orderId", "customerId", "tenantId"]);
  });
});
