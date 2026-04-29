// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { MicroflowAuthoringSchema } from "@atlas/microflow";

import { MicroflowApiException } from "../adapter/http/microflow-api-error";
import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";
import type { MicroflowResource } from "../resource/resource-types";
import { MendixMicroflowEditorEntry } from "./MendixMicroflowEditorEntry";
import { useMendixStudioStore } from "../../store";

const modalConfirm = vi.hoisted(() => vi.fn((options: { onOk?: () => void }) => options.onOk?.()));

vi.mock("@douyinfe/semi-icons", () => ({
  IconArrowLeft: () => <span>back</span>,
}));

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick, disabled }: any) => <button type="button" disabled={disabled} onClick={onClick}>{children}</button>,
  Modal: Object.assign(
    ({ visible, children, footer }: any) => visible ? <div role="dialog">{children}<div>{footer}</div></div> : null,
    { confirm: modalConfirm },
  ),
  Space: ({ children }: any) => <div>{children}</div>,
  Switch: ({ checked, onChange }: any) => <input aria-label="autosave" type="checkbox" checked={checked} onChange={event => onChange?.(event.currentTarget.checked)} />,
  Tag: ({ children }: any) => <span>{children}</span>,
  Toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() },
  Tooltip: ({ children }: any) => <>{children}</>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
  },
}));

vi.mock("@atlas/microflow", () => ({
  MicroflowEditor: ({ schema, onSchemaChange, apiClient, toolbarPrefix, toolbarSuffix }: any) => (
    <div>
      <div>{toolbarPrefix}</div>
      <div>{toolbarSuffix}</div>
      <button type="button" onClick={() => onSchemaChange?.({ ...schema, displayName: "Changed" })}>make-dirty</button>
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

function schema(id: string): MicroflowAuthoringSchema {
  return {
    schemaVersion: "1.0.0",
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
    objectCollection: { objects: [] },
    flows: [],
    validation: { issues: [] },
    editor: { viewport: { x: 0, y: 0, zoom: 1 }, selection: {} },
    audit: { version: "1", status: "draft" },
  } as MicroflowAuthoringSchema;
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

    fireEvent.click(screen.getByText("Force Save"));

    await waitFor(() => expect(modalConfirm).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(saveMicroflowSchema).toHaveBeenCalledTimes(2));
    expect(saveMicroflowSchema.mock.calls[1]?.[2]).toMatchObject({ force: true, saveReason: "force" });
  });
});
