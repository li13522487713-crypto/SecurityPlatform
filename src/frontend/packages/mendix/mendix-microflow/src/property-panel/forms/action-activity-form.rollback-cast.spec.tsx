// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { MicroflowActionActivity, MicroflowAuthoringSchema } from "../../schema";
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

describe("ActionActivityForm rollback/cast", () => {
  it("patches rollback mode and switches without fake defaults", () => {
    const onPatch = vi.fn();
    render(<ActionActivityForm schema={schema()} object={rollbackObject()} issues={[]} onPatch={onPatch} />);

    expect(screen.getByText("Rollback Mode")).toBeTruthy();
    fireEvent.change(screen.getByDisplayValue("objectOnly"), { target: { value: "objectAndAssociations" } });
    fireEvent.click(screen.getByText("Fail If Not Changed").parentElement!.querySelector("input")!);

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ rollbackMode: "objectAndAssociations" }),
      }),
    }));
    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ failIfNotChanged: true }),
      }),
    }));
  });

  it("renders cast-specific generic fields and patches canonical descriptor names", () => {
    const onPatch = vi.fn();
    render(<ActionActivityForm schema={schema()} object={castObject()} issues={[]} onPatch={onPatch} />);

    expect(screen.getByText("Source Variable")).toBeTruthy();
    expect(screen.getByText("Target Entity")).toBeTruthy();
    expect(screen.getByText("Cast Mode")).toBeTruthy();

    fireEvent.change(screen.getAllByDisplayValue("")[0], { target: { value: "sourceOrder" } });
    fireEvent.change(screen.getAllByDisplayValue("")[1], { target: { value: "Sales.Order" } });
    fireEvent.change(screen.getByDisplayValue("strict"), { target: { value: "allowNull" } });

    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ sourceVariable: "sourceOrder" }),
      }),
    }));
    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ targetEntity: "Sales.Order" }),
      }),
    }));
    expect(onPatch).toHaveBeenCalledWith(expect.objectContaining({
      object: expect.objectContaining({
        action: expect.objectContaining({ castMode: "allowNull" }),
      }),
    }));
  });
});

function schema(): MicroflowAuthoringSchema {
  return {
    schemaVersion: "1.0.0",
    id: "mf",
    name: "MF",
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: { objects: [] },
    flows: [],
    editor: { selection: {}, viewport: { x: 0, y: 0, zoom: 1 } },
  } as MicroflowAuthoringSchema;
}

function base(kind: string): MicroflowActionActivity {
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
      officialType: kind === "rollback" ? "Microflows$RollbackAction" : "Microflows$CastAction",
      kind,
      caption: kind,
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "object", iconKey: kind, availability: "supported" },
    } as never,
  } as MicroflowActionActivity;
}

function rollbackObject(): MicroflowActionActivity {
  const object = base("rollback");
  object.action = {
    ...object.action,
    objectOrListVariableName: "order",
    refreshInClient: false,
    rollbackMode: "objectOnly",
    failIfNotChanged: false,
    clearValidationErrors: true,
  } as never;
  return object;
}

function castObject(): MicroflowActionActivity {
  const object = base("cast");
  object.action = {
    ...object.action,
    sourceVariable: "",
    targetEntity: "",
    outputVariable: "",
    castMode: "strict",
    failOnInvalidType: true,
  } as never;
  return object;
}
