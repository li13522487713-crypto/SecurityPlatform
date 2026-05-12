// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { MicroflowActionActivity, MicroflowAuthoringSchema } from "../../schema";
import { createSequenceFlow } from "../../adapters";
import { GenericActionFields } from "./generic-action-fields-form";

vi.mock("@douyinfe/semi-ui", () => ({
  Input: ({ value, onChange, disabled }: any) => <input value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
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
    useMicroflowMetadataCatalog: () => ({ entities: [], enumerations: [], microflows: [] }),
    useMetadataStatus: () => ({ version: "test", loading: false, error: undefined }),
  };
});

afterEach(() => cleanup());

describe("GenericActionFields connector-backed forms", () => {
  it("shows capability missing publish blocker and patches WebService fields", () => {
    const onPatch = vi.fn();
    render(<GenericActionFields schema={schema()} object={connectorObject("webServiceCall")} issues={[]} onPatch={onPatch} />);

    expect(screen.getByText("Capability Missing")).toBeTruthy();
    expect(screen.getByText(/publish must block/i)).toBeTruthy();
    fireEvent.change(screen.getAllByDisplayValue("")[0], { target: { value: "SalesOrderService" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ webServiceQualifiedName: "SalesOrderService" }),
      }),
    }));
  });

  it("patches external action fields", () => {
    const onPatch = vi.fn();
    render(<GenericActionFields schema={schema()} object={connectorObject("callExternalAction")} issues={[]} onPatch={onPatch} />);

    fireEvent.change(screen.getAllByDisplayValue("")[0], { target: { value: "crm" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ consumedServiceQualifiedName: "crm" }),
      }),
    }));
  });

  it("routes external action return variable rename through schema-level rewrite when onSchemaChange is available", () => {
    const onPatch = vi.fn();
    const onSchemaChange = vi.fn();
    render(
      <GenericActionFields
        schema={schema(
          [connectorObject("callExternalAction", { returnVariableName: "externalResult" }), downstreamChangeVariableObject()],
          [createSequenceFlow({ originObjectId: "callExternalAction-activity", destinationObjectId: "downstream-change-variable-activity" })],
        )}
        object={connectorObject("callExternalAction", { returnVariableName: "externalResult" })}
        issues={[]}
        onPatch={onPatch}
        onSchemaChange={onSchemaChange}
      />,
    );

    fireEvent.change(screen.getAllByDisplayValue("externalResult")[0], { target: { value: "crmResult" } });

    expect(onPatch).not.toHaveBeenCalled();
    expect(onSchemaChange).toHaveBeenCalledTimes(1);
    const [nextSchema, reason] = onSchemaChange.mock.calls[0] ?? [];
    const changed = nextSchema?.objectCollection?.objects?.find((item: any) => item.id === "downstream-change-variable-activity");
    expect(reason).toBe("renameActionOutputVariable");
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.targetVariableName : undefined).toBe("crmResult");
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.newValueExpression.raw : undefined).toBe("$crmResult");
  });

  it("routes generate document output rename through schema-level rewrite when onSchemaChange is available", () => {
    const onPatch = vi.fn();
    const onSchemaChange = vi.fn();
    render(
      <GenericActionFields
        schema={schema(
          [connectorObject("generateDocument", { outputFileDocumentVariableName: "invoiceDoc" }), downstreamDocumentConsumerObject()],
          [createSequenceFlow({ originObjectId: "generateDocument-activity", destinationObjectId: "downstream-document-consumer-activity" })],
        )}
        object={connectorObject("generateDocument", { outputFileDocumentVariableName: "invoiceDoc" })}
        issues={[]}
        onPatch={onPatch}
        onSchemaChange={onSchemaChange}
      />,
    );

    fireEvent.change(screen.getByDisplayValue("invoiceDoc"), { target: { value: "generatedInvoice" } });

    expect(onPatch).not.toHaveBeenCalled();
    expect(onSchemaChange).toHaveBeenCalledTimes(1);
    const [nextSchema, reason] = onSchemaChange.mock.calls[0] ?? [];
    const changed = nextSchema?.objectCollection?.objects?.find((item: any) => item.id === "downstream-document-consumer-activity");
    expect(reason).toBe("renameActionOutputVariable");
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.targetVariableName : undefined).toBe("generatedInvoice");
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.newValueExpression.raw : undefined).toBe("$generatedInvoice");
  });

  it("patches external object fields", () => {
    const onPatch = vi.fn();
    render(<GenericActionFields schema={schema()} object={connectorObject("sendExternalObject")} issues={[]} onPatch={onPatch} />);

    fireEvent.change(screen.getAllByDisplayValue("")[0], { target: { value: "externalOrder" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ externalObjectVariableName: "externalOrder" }),
      }),
    }));
  });

  it("renders and patches sortList expression and direction fields", () => {
    const onPatch = vi.fn();
    render(
      <GenericActionFields
        schema={schema()}
        object={connectorObject("sortList", {
          editor: { category: "list", iconKey: "sortList", availability: "supported" },
          sourceListVariableName: "orders",
          sortExpression: { raw: "$item/score" },
          direction: "asc",
          outputVariableName: "sortedOrders",
        })}
        issues={[]}
        onPatch={onPatch}
      />,
    );

    const expressionInput = screen.getByLabelText("expression-editor");
    fireEvent.change(expressionInput, { target: { value: "$item/priority" } });
    fireEvent.change(screen.getByDisplayValue("asc"), { target: { value: "desc" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({
          sortExpression: expect.objectContaining({ raw: "$item/priority" }),
        }),
      }),
    }));
    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({
          direction: "desc",
        }),
      }),
    }));
  });
});

function schema(objects: MicroflowActionActivity[] = [], flows: unknown[] = []): MicroflowAuthoringSchema {
  return {
    schemaVersion: "1.0.0",
    id: "mf",
    name: "MF",
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: { objects },
    flows,
    editor: { selection: {}, viewport: { x: 0, y: 0, zoom: 1 } },
  } as unknown as MicroflowAuthoringSchema;
}

function connectorObject(kind: string, patch: Record<string, unknown> = {}): MicroflowActionActivity {
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
      officialType: "Microflows$GenericAction",
      kind,
      caption: kind,
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "integration", iconKey: kind, availability: "requiresConnector", availabilityReason: "需要连接器" },
      ...patch,
    } as never,
  } as unknown as MicroflowActionActivity;
}

function downstreamChangeVariableObject(): MicroflowActionActivity {
  return {
    id: "downstream-change-variable-activity",
    stableId: "downstream-change-variable-activity",
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: "changeVariable",
    documentation: "",
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 160, height: 80 },
    ports: [],
    autoGenerateCaption: false,
    backgroundColor: "default",
    action: {
      id: "downstream-change-variable-action",
      officialType: "Microflows$ChangeVariableAction",
      kind: "changeVariable",
      caption: "changeVariable",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "variable", iconKey: "changeVariable", availability: "supported" },
      targetVariableName: "externalResult",
      newValueExpression: { raw: "$externalResult", inferredType: { kind: "string" }, references: { variables: ["$externalResult"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
    } as never,
  } as unknown as MicroflowActionActivity;
}

function downstreamDocumentConsumerObject(): MicroflowActionActivity {
  return {
    ...downstreamChangeVariableObject(),
    id: "downstream-document-consumer-activity",
    stableId: "downstream-document-consumer-activity",
    action: {
      ...downstreamChangeVariableObject().action,
      id: "downstream-document-consumer-action",
      targetVariableName: "invoiceDoc",
      newValueExpression: { raw: "$invoiceDoc", inferredType: { kind: "object", entityQualifiedName: "System.FileDocument" }, references: { variables: ["$invoiceDoc"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
    } as never,
  } as unknown as MicroflowActionActivity;
}
