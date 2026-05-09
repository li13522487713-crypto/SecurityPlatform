// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { ReactNode } from "react";
import type { MicroflowDesignSchema } from "@atlas/microflow";

import { MicroflowApiException } from "../adapter/http/microflow-api-error";
import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";
import type { MicroflowResource } from "../resource/resource-types";
import { MendixMicroflowEditorEntry } from "./MendixMicroflowEditorEntry";
import { useMendixStudioStore } from "../../store";

const modalConfirm = vi.hoisted(() => vi.fn((options: { onOk?: () => void }) => options.onOk?.()));

interface MockShellProps {
  children?: ReactNode;
}

interface MockButtonProps extends MockShellProps {
  disabled?: boolean;
  onClick?: () => void;
}

interface MockModalProps extends MockShellProps {
  visible?: boolean;
  footer?: ReactNode;
}

interface MockSwitchProps {
  checked?: boolean;
  onChange?: (checked: boolean) => void;
}

interface MockMicroflowEditorProps {
  schema: MicroflowDesignSchema;
  onSchemaChange?: (schema: MicroflowDesignSchema) => void;
  apiClient: {
    saveMicroflow: (request: { schema: MicroflowDesignSchema }) => Promise<unknown>;
  };
  toolbarPrefix?: ReactNode;
  toolbarSuffix?: ReactNode;
}

vi.mock("@douyinfe/semi-icons", () => ({
  IconArrowLeft: () => <span>back</span>,
}));

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick, disabled }: MockButtonProps) => <button type="button" disabled={disabled} onClick={() => onClick?.()}>{children}</button>,
  Modal: Object.assign(
    ({ visible, children, footer }: MockModalProps) => visible ? <div role="dialog">{children}<div>{footer}</div></div> : null,
    { confirm: modalConfirm },
  ),
  Space: ({ children }: MockShellProps) => <div>{children}</div>,
  Switch: ({ checked, onChange }: MockSwitchProps) => <input aria-label="autosave" type="checkbox" checked={checked} onChange={event => onChange?.(event.currentTarget.checked)} />,
  Tag: ({ children }: MockShellProps) => <span>{children}</span>,
  Toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() },
  Tooltip: ({ children }: MockShellProps) => <>{children}</>,
  Typography: {
    Text: ({ children }: MockShellProps) => <span>{children}</span>,
  },
}));

vi.mock("@atlas/microflow", () => ({
  MicroflowEditor: ({ schema, onSchemaChange, apiClient, toolbarPrefix, toolbarSuffix }: MockMicroflowEditorProps) => (
    <div>
      <div>{toolbarPrefix}</div>
      <div>{toolbarSuffix}</div>
      <button
        type="button"
        onClick={() => onSchemaChange?.({
          ...schema,
          displayName: "Changed",
          workflow: {
            ...schema.workflow,
            nodes: schema.workflow.nodes.map((node, index) => index === 0
              ? {
                  ...node,
                  meta: {
                    ...node.meta,
                    position: { x: 320, y: 180 },
                  },
                }
              : node),
          },
        })}
      >
        make-dirty
      </button>
      <button type="button" onClick={() => void apiClient.saveMicroflow({ schema }).catch(() => undefined)}>save-now</button>
    </div>
  ),
}));

vi.mock("../publish/PublishMicroflowModal", () => ({
  PublishMicroflowModal: () => null,
}));

vi.mock("../versions/MicroflowVersionsDrawer", () => ({
  MicroflowVersionsDrawer: () => null,
}));

vi.mock("../references/MicroflowReferencesDrawer", () => ({
  MicroflowReferencesDrawer: () => null,
}));

function schema(id: string): MicroflowDesignSchema {
  return {
    schemaVersion: "flowgram.microflow.v1",
    id,
    stableId: id,
    moduleId: "sales",
    moduleName: "Sales",
    name: "OrderSubmit",
    displayName: "Order Submit",
    description: "",
    parameters: [],
    returnType: { kind: "void" },
    variables: [],
    workflow: { nodes: [], edges: [] },
    validation: { issues: [] },
    editor: { viewport: { x: 0, y: 0, zoom: 1 }, selection: {} },
    audit: { version: "1", status: "draft" },
  } as MicroflowDesignSchema;
}

function schemaWithStart(id: string, position: { x: number; y: number }): MicroflowDesignSchema {
  return {
    ...schema(id),
    workflow: {
      nodes: [{
        id: "start-1",
        type: "startEvent",
        data: {
          objectId: "start-1",
          objectKind: "startEvent",
          collectionId: "root-collection",
          title: "Start",
          officialType: "Microflows$StartEvent",
        },
        meta: {
          position,
          size: { width: 160, height: 72 },
          collectionId: "root-collection",
        },
      }],
      edges: [],
    },
  } as MicroflowDesignSchema;
}

function resource(overrides: Partial<MicroflowResource> = {}): MicroflowResource {
  return {
    id: "mf-1",
    schemaId: "schema-1",
    workspaceId: "workspace-1",
    moduleId: "sales",
    moduleName: "Sales",
    name: "OrderSubmit",
    displayName: "Order Submit",
    description: "",
    tags: [],
    createdAt: "2026-04-29T00:00:00.000Z",
    updatedAt: "2026-04-29T00:00:00.000Z",
    version: "1",
    status: "draft",
    publishStatus: "neverPublished",
    favorite: false,
    archived: false,
    referenceCount: 0,
    schema: schema("mf-1"),
    ...overrides,
  };
}

describe("MendixMicroflowEditorEntry save conflict", () => {
  beforeEach(() => {
    modalConfirm.mockClear();
    useMendixStudioStore.setState({
      workbenchTabs: [{ id: "microflow:mf-1", kind: "microflow", title: "OrderSubmit", microflowId: "mf-1", resourceId: "mf-1", openedAt: "2026-04-29T00:00:00.000Z" }],
      activeWorkbenchTabId: "microflow:mf-1",
      dirtyByWorkbenchTabId: {},
      saveStateByMicroflowId: {},
      saveErrorByMicroflowId: {},
      saveConflictByMicroflowId: {},
    });
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("shows conflict actions and force save confirms before overriding remote schema", async () => {
    const initial = resource();
    const remote = resource({ schemaId: "schema-remote", version: "2", updatedAt: "2026-04-29T01:00:00.000Z" });
    const saveMicroflowSchema = vi
      .fn()
      .mockRejectedValueOnce(new MicroflowApiException("版本冲突", {
        status: 409,
        traceId: "trace-409",
        apiError: {
          code: "MICROFLOW_VERSION_CONFLICT",
          message: "版本冲突",
          category: "conflict",
          httpStatus: 409,
          traceId: "trace-409",
          details: JSON.stringify({
            remoteVersion: "2",
            remoteSchemaId: "schema-remote",
            remoteUpdatedAt: "2026-04-29T01:00:00.000Z",
            remoteUpdatedBy: "remote-user",
            baseVersion: "schema-1",
          }),
        },
      }))
      .mockResolvedValueOnce(remote);
    const adapter = {
      saveMicroflowSchema,
      getMicroflow: vi.fn(async () => remote),
      getMicroflowSchema: vi.fn(async () => remote.schema),
    } as unknown as MicroflowResourceAdapter;

    render(<MendixMicroflowEditorEntry resource={initial} adapter={adapter} />);

    fireEvent.click(screen.getByText("make-dirty"));
    fireEvent.click(screen.getByText("save-now"));

    expect(await screen.findByText("Reload Remote")).toBeTruthy();
    expect(screen.getByText("Keep Local")).toBeTruthy();
    expect(screen.getByText("Force Save")).toBeTruthy();
    expect(screen.getByText("Cancel")).toBeTruthy();
    expect(screen.getByText((_, node) => node?.textContent === "remote version: 2")).toBeTruthy();
    expect(screen.getByText((_, node) => node?.textContent === "remote updatedBy: remote-user")).toBeTruthy();
    expect(screen.getByText((_, node) => node?.textContent === "traceId: trace-409")).toBeTruthy();
    expect(saveMicroflowSchema.mock.calls[0]?.[2]).toMatchObject({
      baseVersion: "schema-1",
      schemaId: "schema-1",
      version: "1",
      saveReason: "manual",
      force: false,
    });
    expect(saveMicroflowSchema.mock.calls[0]?.[2]?.clientRequestId).toEqual(expect.stringContaining("mf-1:"));

    fireEvent.click(screen.getByText("Force Save"));

    await waitFor(() => expect(modalConfirm).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(saveMicroflowSchema).toHaveBeenCalledTimes(2));
    expect(saveMicroflowSchema.mock.calls[1]?.[2]).toMatchObject({ force: true, saveReason: "force" });
  });

  it("queues autosave with version metadata and clears dirty state after save", async () => {
    vi.useFakeTimers();
    const initial = resource();
    const saved = resource({ schemaId: "schema-2", version: "2" });
    const saveMicroflowSchema = vi.fn(async () => saved);
    const adapter = {
      saveMicroflowSchema,
      getMicroflow: vi.fn(async () => saved),
      getMicroflowSchema: vi.fn(async () => saved.schema),
    } as unknown as MicroflowResourceAdapter;

    render(<MendixMicroflowEditorEntry resource={initial} adapter={adapter} />);

    fireEvent.click(screen.getByLabelText("autosave"));
    fireEvent.click(screen.getByText("make-dirty"));
    await Promise.resolve();
    await vi.advanceTimersByTimeAsync(4_100);
    await Promise.resolve();

    expect(saveMicroflowSchema).toHaveBeenCalledTimes(1);
    expect(saveMicroflowSchema.mock.calls[0]?.[2]).toMatchObject({
      baseVersion: "schema-1",
      schemaId: "schema-1",
      version: "1",
      saveReason: "autosave",
      force: false,
    });
    expect(useMendixStudioStore.getState().saveStateByMicroflowId["mf-1"]).toMatchObject({
      status: "saved",
      dirty: false,
      saving: false,
    });

    vi.useRealTimers();
  });

  it("preserves submitted node layout when the save response returns stale positions", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const initialSchema = schemaWithStart("mf-1", { x: 0, y: 0 });
    const initial = resource({ schema: initialSchema });
    const saved = resource({ schemaId: "schema-2", version: "2", schema: initialSchema });
    const saveMicroflowSchema = vi.fn(async () => saved);
    const onSave = vi.fn();
    const adapter = {
      saveMicroflowSchema,
      getMicroflow: vi.fn(async () => saved),
      getMicroflowSchema: vi.fn(async () => saved.schema),
    } as unknown as MicroflowResourceAdapter;

    render(<MendixMicroflowEditorEntry resource={initial} adapter={adapter} onSave={onSave} />);

    fireEvent.click(screen.getByText("make-dirty"));
    fireEvent.click(screen.getByText("save-now"));

    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(1));
    expect(saveMicroflowSchema.mock.calls[0]?.[1].workflow.nodes[0]?.meta?.position).toEqual({ x: 320, y: 180 });
    expect(onSave.mock.calls[0]?.[0]).toMatchObject({ schemaId: "schema-2", version: "2" });
    expect(onSave.mock.calls[0]?.[0].schema.workflow.nodes[0]?.meta?.position).toEqual({ x: 320, y: 180 });
    expect(warn).toHaveBeenCalledWith(
      expect.stringContaining("Microflow save response schema layout differs"),
      expect.objectContaining({ microflowId: "mf-1" }),
    );
    warn.mockRestore();
  });
});
