// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { MicroflowActionActivity, MicroflowAuthoringSchema } from "../../schema";
import { createSequenceFlow } from "../../adapters";
import { ActionActivityForm } from "./action-activity-form";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick }: any) => <button type="button" onClick={onClick}>{children}</button>,
  Input: ({ value, onChange, disabled }: any) => <input value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  InputNumber: ({ value, onChange, disabled }: any) => <input value={value ?? 0} disabled={disabled} onChange={event => onChange?.(Number(event.currentTarget.value))} />,
  Select: ({ value, onChange, optionList, disabled }: any) => (
    <select value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)}>
      {(optionList ?? []).map((option: any) => <option key={option.value} value={option.value}>{option.label}</option>)}
    </select>
  ),
  Space: ({ children }: any) => <div>{children}</div>,
  Switch: ({ checked, onChange, disabled }: any) => <input type="checkbox" checked={Boolean(checked)} disabled={disabled} onChange={event => onChange?.(event.currentTarget.checked)} />,
  Tag: ({ children }: any) => <span>{children}</span>,
  TextArea: ({ value, onChange, disabled }: any) => <textarea value={value ?? ""} disabled={disabled} onChange={event => onChange?.(event.currentTarget.value)} />,
  Tooltip: ({ children }: any) => <>{children}</>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
    Title: ({ children }: any) => <h3>{children}</h3>,
  },
}));

vi.mock("../../metadata", async () => {
  const actual = await vi.importActual<any>("../../metadata");
  return {
    ...actual,
    useMicroflowMetadataCatalog: () => ({
      entities: [
        { qualifiedName: "Sales.Order", name: "Order", moduleName: "Sales", attributes: [], associations: [] },
      ],
      enumerations: [],
      microflows: [],
    }),
    useMetadataStatus: () => ({ version: "test", loading: false, error: undefined }),
  };
});

afterEach(() => cleanup());

describe("ActionActivityForm auto caption", () => {
  it("regenerates caption when auto-generate is enabled", () => {
    const onPatch = vi.fn();
    render(<ActionActivityForm schema={schema()} object={createVariableObject({ autoGenerateCaption: true })} issues={[]} onPatch={onPatch} />);

    fireEvent.change(screen.getByDisplayValue("Discount"), { target: { value: "FinalDiscount" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        caption: "Create FinalDiscount",
        action: expect.objectContaining({
          caption: "Create FinalDiscount",
          variableName: "FinalDiscount",
        }),
      }),
    }));
  });

  it("generates caption immediately when turning on auto-generate", () => {
    const onPatch = vi.fn();
    render(<ActionActivityForm schema={schema()} object={createObject({ autoGenerateCaption: false, caption: "Custom" })} issues={[]} onPatch={onPatch} />);

    fireEvent.click(screen.getAllByRole("checkbox")[0]);

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        autoGenerateCaption: true,
        caption: "Create Order",
        action: expect.objectContaining({ caption: "Create Order" }),
      }),
    }));
  });

  it("does not override manual caption when auto-generate is disabled", () => {
    const onPatch = vi.fn();
    render(<ActionActivityForm schema={schema()} object={createVariableObject({ autoGenerateCaption: false, caption: "Manual Discount" })} issues={[]} onPatch={onPatch} />);

    fireEvent.change(screen.getByDisplayValue("Discount"), { target: { value: "FinalDiscount" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        caption: "Manual Discount",
        action: expect.objectContaining({
          caption: "Manual Discount",
          variableName: "FinalDiscount",
        }),
      }),
    }));
  });

  it("routes Create Variable rename through schema-level rewrite when onSchemaChange is available", () => {
    const onPatch = vi.fn();
    const onSchemaChange = vi.fn();
    render(
      <ActionActivityForm
        schema={
          schema(
            [createVariableObject(), changeVariableObject()],
            [createSequenceFlow({ originObjectId: "create-variable-activity", destinationObjectId: "change-variable-activity" })],
          )
        }
        object={createVariableObject()}
        issues={[]}
        onPatch={onPatch}
        onSchemaChange={onSchemaChange}
      />,
    );

    fireEvent.change(screen.getByDisplayValue("Discount"), { target: { value: "FinalDiscount" } });

    expect(onPatch).not.toHaveBeenCalled();
    expect(screen.getByText("Variable rename rewrites variable-scoped expressions and direct variable reference fields; unrelated shadowed loop/local variables stay unchanged.")).toBeTruthy();
    expect(onSchemaChange).toHaveBeenCalledTimes(1);
    const [nextSchema, reason] = onSchemaChange.mock.calls[0] ?? [];
    const changed = nextSchema?.objectCollection?.objects?.find((item: any) => item.id === "change-variable-activity");
    expect(reason).toBe("renameCreateVariable");
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.targetVariableName : undefined).toBe("FinalDiscount");
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeVariable" ? changed.action.newValueExpression.raw : undefined).toBe("$FinalDiscount + 1");
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

function createVariableObject(options?: { autoGenerateCaption?: boolean; caption?: string }): MicroflowActionActivity {
  const caption = options?.caption ?? "Create Discount";
  return {
    id: "create-variable-activity",
    stableId: "create-variable-activity",
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption,
    documentation: "",
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 160, height: 80 },
    ports: [],
    autoGenerateCaption: options?.autoGenerateCaption ?? false,
    backgroundColor: "default",
    editor: {},
    action: {
      id: "create-variable-action",
      officialType: "Microflows$CreateVariableAction",
      kind: "createVariable",
      caption,
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "variable", iconKey: "createVariable", availability: "supported" },
      variableName: "Discount",
      dataType: { kind: "decimal" },
      initialValue: { raw: "0.15", referencedVariables: [], diagnostics: [] },
      readonly: false,
    } as never,
  } as unknown as MicroflowActionActivity;
}

function createObject(options?: { autoGenerateCaption?: boolean; caption?: string }): MicroflowActionActivity {
  const caption = options?.caption ?? "Create Object";
  return {
    id: "create-object-activity",
    stableId: "create-object-activity",
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption,
    documentation: "",
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 160, height: 80 },
    ports: [],
    autoGenerateCaption: options?.autoGenerateCaption ?? false,
    backgroundColor: "default",
    editor: {},
    action: {
      id: "create-object-action",
      officialType: "Microflows$CreateObjectAction",
      kind: "createObject",
      caption,
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "object", iconKey: "createObject", availability: "supported" },
      entityQualifiedName: "Sales.Order",
      outputVariableName: "Order",
      memberChanges: [],
      commit: { enabled: false, withEvents: true, refreshInClient: false },
    } as never,
  } as unknown as MicroflowActionActivity;
}

function changeVariableObject(): MicroflowActionActivity {
  return {
    id: "change-variable-activity",
    stableId: "change-variable-activity",
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: "Change Discount",
    documentation: "",
    relativeMiddlePoint: { x: 200, y: 0 },
    size: { width: 160, height: 80 },
    ports: [],
    autoGenerateCaption: false,
    backgroundColor: "default",
    editor: {},
    action: {
      id: "change-variable-action",
      officialType: "Microflows$ChangeVariableAction",
      kind: "changeVariable",
      caption: "Change Discount",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "variable", iconKey: "changeVariable", availability: "supported" },
      targetVariableName: "Discount",
      newValueExpression: {
        raw: "$Discount + 1",
        inferredType: { kind: "decimal" },
        references: { variables: ["$Discount"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
        diagnostics: [],
      },
    } as never,
  } as unknown as MicroflowActionActivity;
}
