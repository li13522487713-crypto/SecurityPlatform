// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { MicroflowAuthoringSchema } from "@atlas/microflow";

import type { MicroflowAdapterBundle } from "../../adapter/microflow-adapter-factory";
import { createMicroflowApiError } from "../../adapter/http/microflow-api-error";
import type { MicroflowResource } from "../../resource/resource-types";
import { MicroflowResourceEditorHost } from "../MicroflowResourceEditorHost";

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick }: any) => <button type="button" onClick={onClick}>{children}</button>,
  Empty: ({ title, description, children }: any) => (
    <div>
      <h1>{title}</h1>
      <div>{description}</div>
      {children}
    </div>
  ),
  Space: ({ children }: any) => <div>{children}</div>,
  Spin: () => <div>loading</div>,
  Tag: ({ children }: any) => <span>{children}</span>,
  Typography: {
    Text: ({ children }: any) => <span>{children}</span>,
  },
}));

vi.mock("../../editor/MendixMicroflowEditorEntry", () => ({
  MendixMicroflowEditorEntry: ({ resource, onDirtyChange, onSave }: any) => (
    <div data-testid="entry" data-resource-id={resource.id} data-schema-id={resource.schema.id}>
      <button type="button" onClick={() => onDirtyChange?.(true)}>dirty</button>
      <button type="button" onClick={() => onSave?.({ ...resource, version: "2", updatedAt: "2026-04-28T01:00:00.000Z" })}>save-success</button>
    </div>
  ),
}));

function createSchema(id: string): MicroflowAuthoringSchema {
  return {
    schemaVersion: "1.0.0",
    id,
    stableId: id,
    moduleId: "mod_procurement",
    moduleName: "Procurement",
    name: `MF_${id}`,
    displayName: `MF_${id}`,
    description: "",
    parameters: [],
    returnType: { kind: "void" },
    variables: [],
    objectCollection: { objects: [] },
    flows: [],
    validation: { issues: [] },
    editor: { viewport: { x: 0, y: 0, zoom: 1 }, selection: {} },
    audit: {
      version: "1",
      status: "draft",
      createdAt: "2026-04-28T00:00:00.000Z",
      createdBy: "tester",
      updatedAt: "2026-04-28T00:00:00.000Z",
      updatedBy: "tester",
    },
  } as MicroflowAuthoringSchema;
}

function createResource(id: string): MicroflowResource {
  return {
    id,
    schemaId: `schema-${id}`,
    workspaceId: "workspace-1",
    moduleId: "mod_procurement",
    moduleName: "Procurement",
    name: `MF_${id}`,
    displayName: `MF_${id}`,
    description: "",
    tags: [],
    createdAt: "2026-04-28T00:00:00.000Z",
    updatedAt: "2026-04-28T00:00:00.000Z",
    version: "1",
    status: "draft",
    publishStatus: "neverPublished",
    favorite: false,
    archived: false,
    referenceCount: 0,
    schema: createSchema(`resource-${id}`),
  };
}

function createBundle(input: {
  getMicroflow: MicroflowAdapterBundle["resourceAdapter"]["getMicroflow"];
  getMicroflowSchema: MicroflowAdapterBundle["resourceAdapter"]["getMicroflowSchema"];
}): MicroflowAdapterBundle {
  return {
    mode: "http",
    runtimePolicy: "production",
    resourceAdapter: {
      listMicroflows: vi.fn(),
      getMicroflow: input.getMicroflow,
      getMicroflowSchema: input.getMicroflowSchema,
    },
    metadataAdapter: { getMetadataCatalog: vi.fn(async () => ({ entities: [], enumerations: [], microflows: [] })) },
    runtimeAdapter: {},
    validationAdapter: { validate: vi.fn() },
  } as unknown as MicroflowAdapterBundle;
}

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("MicroflowResourceEditorHost", () => {
  it("loads resource and schema through resourceAdapter using the current microflowId", async () => {
    const getMicroflow = vi.fn(async () => createResource("mf-a"));
    const getMicroflowSchema = vi.fn(async () => createSchema("schema-mf-a"));
    const onDirtyChange = vi.fn();

    render(
      <MicroflowResourceEditorHost
        microflowId="mf-a"
        workspaceId="workspace-1"
        adapterBundle={createBundle({ getMicroflow, getMicroflowSchema })}
        onDirtyChange={onDirtyChange}
      />
    );

    expect((await screen.findByTestId("entry")).getAttribute("data-resource-id")).toBe("mf-a");
    expect(screen.getByTestId("entry").getAttribute("data-schema-id")).toBe("schema-mf-a");
    expect(getMicroflow).toHaveBeenCalledWith("mf-a");
    expect(getMicroflowSchema).toHaveBeenCalledWith("mf-a");
    expect(onDirtyChange).toHaveBeenCalledWith(false);
  });

  it("keeps dirty state per host callback and reports saved resources", async () => {
    const onDirtyChange = vi.fn();
    const onResourceUpdated = vi.fn();

    render(
      <MicroflowResourceEditorHost
        microflowId="mf-a"
        workspaceId="workspace-1"
        adapterBundle={createBundle({
          getMicroflow: vi.fn(async () => createResource("mf-a")),
          getMicroflowSchema: vi.fn(async () => createSchema("schema-mf-a")),
        })}
        onDirtyChange={onDirtyChange}
        onResourceUpdated={onResourceUpdated}
      />
    );

    fireEvent.click(await screen.findByText("dirty"));
    expect(onDirtyChange).toHaveBeenLastCalledWith(true);

    fireEvent.click(screen.getByText("save-success"));
    expect(onDirtyChange).toHaveBeenLastCalledWith(false);
    expect(onResourceUpdated).toHaveBeenCalledWith(expect.objectContaining({ id: "mf-a", version: "2" }));
  });

  it("renders missing state instead of falling back to a sample schema", async () => {
    render(
      <MicroflowResourceEditorHost
        microflowId="missing"
        workspaceId="workspace-1"
        adapterBundle={createBundle({
          getMicroflow: vi.fn(async () => {
            throw createMicroflowApiError("missing", 404);
          }),
          getMicroflowSchema: vi.fn(async () => createSchema("schema-missing")),
        })}
      />
    );

    await waitFor(() => expect(screen.getByText("Microflow no longer exists")).toBeTruthy());
    expect(screen.queryByText("sampleOrderProcessingMicroflow")).toBeNull();
  });
});
