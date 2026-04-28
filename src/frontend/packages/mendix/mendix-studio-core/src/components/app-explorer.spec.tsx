// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { AppExplorer } from "./app-explorer";
import { useMendixStudioStore } from "../store";
import type { MicroflowAdapterBundle } from "../microflow/adapter/microflow-adapter-factory";
import type { MicroflowResource } from "../microflow/resource/resource-types";

vi.mock("@atlas/microflow", () => ({
  sampleOrderProcessingMicroflow: {
    schemaVersion: "test",
    id: "sampleOrderProcessingMicroflow",
    name: "sampleOrderProcessingMicroflow",
    nodes: [],
    edges: [],
  },
}));

vi.mock("@douyinfe/semi-icons", () => ({
  IconChevronDown: () => <span>v</span>,
  IconChevronRight: () => <span>&gt;</span>,
  IconSearch: () => <span>search</span>,
}));

vi.mock("@douyinfe/semi-ui", async () => {
  return {
    Toast: {
      success: vi.fn(),
      error: vi.fn(),
      warning: vi.fn(),
    },
    Modal: Object.assign(({ visible, children }: any) => visible ? <div>{children}</div> : null, {
      confirm: vi.fn(),
    }),
    Button: ({ children, onClick, title }: any) => (
      <button type="button" title={title} onClick={onClick}>{children}</button>
    ),
    Dropdown: Object.assign(({ children }: any) => <>{children}</>, {
      Menu: ({ children }: any) => <div>{children}</div>,
      Item: ({ children, onClick, disabled, title }: any) => (
        <button type="button" title={title} disabled={disabled} onClick={onClick}>{children}</button>
      ),
    }),
    Input: ({ value, onChange, placeholder }: any) => (
      <input aria-label={placeholder} value={value ?? ""} onChange={event => onChange?.(event.target.value)} />
    ),
    Space: ({ children }: any) => <div>{children}</div>,
    Typography: {
      Text: ({ children }: any) => <p>{children}</p>,
    },
  };
});

function createResource(input: Partial<MicroflowResource> & Pick<MicroflowResource, "id" | "name">): MicroflowResource {
  return {
    id: input.id,
    schemaId: input.schemaId ?? `schema-${input.id}`,
    workspaceId: input.workspaceId ?? "workspace-1",
    moduleId: input.moduleId ?? "mod_procurement",
    moduleName: input.moduleName ?? "Procurement",
    name: input.name,
    displayName: input.displayName ?? input.name,
    description: input.description,
    tags: input.tags ?? [],
    createdAt: input.createdAt ?? "2026-04-28T00:00:00.000Z",
    updatedAt: input.updatedAt ?? "2026-04-28T00:00:00.000Z",
    version: input.version ?? "1",
    status: input.status ?? "draft",
    publishStatus: input.publishStatus ?? "neverPublished",
    favorite: input.favorite ?? false,
    archived: input.archived ?? false,
    referenceCount: input.referenceCount ?? 0,
    schema: input.schema ?? ({} as MicroflowResource["schema"]),
  };
}

function createBundle(listMicroflows: MicroflowAdapterBundle["resourceAdapter"]["listMicroflows"]): MicroflowAdapterBundle {
  return {
    mode: "http",
    runtimePolicy: "production",
    resourceAdapter: {
      listMicroflows,
    },
    metadataAdapter: {},
    runtimeAdapter: {},
  } as MicroflowAdapterBundle;
}

beforeEach(() => {
  vi.useRealTimers();
  useMendixStudioStore.setState({
    activeTab: "pageBuilder",
    activeTabId: "page",
    workbenchTabs: [
      {
        id: "page",
        kind: "page",
        title: "Page",
        resourceId: "page",
        closable: false,
        openedAt: "2026-04-28T00:00:00.000Z",
        historyKey: "page",
      },
    ],
    activeWorkbenchTabId: "page",
    activeModuleId: undefined,
    activeMicroflowId: undefined,
    selectedExplorerNodeId: "page_purchase_request_edit",
    selectedKind: undefined,
    selectedId: undefined,
    microflowResourcesById: {},
    microflowIdsByModuleId: {},
  });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  vi.useRealTimers();
});

describe("AppExplorer Microflows assets", () => {
  it("renders loading state while listMicroflows is pending", () => {
    const bundle = createBundle(vi.fn(() => new Promise(() => undefined)));

    render(<AppExplorer adapterBundle={bundle} workspaceId="workspace-loading" />);

    expect(screen.getByText("Loading microflows...")).toBeTruthy();
  });

  it("renders empty state without hardcoded sample microflow", async () => {
    const bundle = createBundle(vi.fn(async () => ({ items: [], total: 0, pageIndex: 1, pageSize: 100 })));

    render(<AppExplorer adapterBundle={bundle} workspaceId="workspace-1" />);

    expect(await screen.findByText("No microflows")).toBeTruthy();
    expect(screen.queryByText("MF_SubmitPurchaseRequest")).toBeNull();
  });

  it("renders error state and retries without falling back to hardcoded nodes", async () => {
    const listMicroflows = vi
      .fn()
      .mockRejectedValueOnce(new TypeError("Failed to fetch"))
      .mockResolvedValueOnce({ items: [], total: 0, pageIndex: 1, pageSize: 100 });
    const bundle = createBundle(listMicroflows);

    render(<AppExplorer adapterBundle={bundle} workspaceId="workspace-1" />);

    expect(await screen.findByText("Load failed")).toBeTruthy();
    expect(screen.getByText(/MICROFLOW_NETWORK_ERROR/u)).toBeTruthy();
    fireEvent.click(screen.getByText("Retry"));

    await waitFor(() => expect(listMicroflows).toHaveBeenCalledTimes(2));
    expect(await screen.findByText("No microflows")).toBeTruthy();
    expect(screen.queryByText("MF_SubmitPurchaseRequest")).toBeNull();
  });

  it("renders real microflow nodes from resourceAdapter and writes store indexes", async () => {
    const resource = createResource({ id: "mf-1", name: "MF_ApprovePurchase", displayName: "Approve Purchase" });
    const listMicroflows = vi.fn(async () => ({ items: [resource], total: 1, pageIndex: 1, pageSize: 100 }));
    const bundle = createBundle(listMicroflows);

    render(<AppExplorer adapterBundle={bundle} workspaceId="workspace-1" />);

    expect(await screen.findByText("Approve Purchase")).toBeTruthy();
    expect(listMicroflows).toHaveBeenCalledWith(expect.objectContaining({
      workspaceId: "workspace-1",
      moduleId: "mod_procurement",
      sortBy: "name",
      sortOrder: "asc",
    }));
    expect(useMendixStudioStore.getState().microflowResourcesById["mf-1"]?.qualifiedName).toBe("Procurement.MF_ApprovePurchase");
    expect(useMendixStudioStore.getState().microflowIdsByModuleId.mod_procurement).toEqual(["mf-1"]);
  });

  it("filters search by displayName and restores full list when cleared", async () => {
    const bundle = createBundle(vi.fn(async () => ({
      items: [
        createResource({ id: "mf-1", name: "MF_ApprovePurchase", displayName: "Approve Purchase" }),
        createResource({ id: "mf-2", name: "MF_RejectPurchase", displayName: "Reject Purchase" }),
      ],
      total: 2,
      pageIndex: 1,
      pageSize: 100,
    })));

    render(<AppExplorer adapterBundle={bundle} workspaceId="workspace-1" />);

    expect(await screen.findByText("Approve Purchase")).toBeTruthy();
    expect(screen.getByText("Reject Purchase")).toBeTruthy();

    fireEvent.change(screen.getByLabelText("搜索（⌘K）"), { target: { value: "Reject" } });
    await waitFor(() => expect(screen.queryByText("Approve Purchase")).toBeNull());
    expect(screen.getByText("Reject Purchase")).toBeTruthy();

    fireEvent.change(screen.getByLabelText("搜索（⌘K）"), { target: { value: "" } });
    await waitFor(() => expect(screen.getByText("Approve Purchase")).toBeTruthy());
    expect(screen.getByText("Reject Purchase")).toBeTruthy();
  });

  it("clicking a real microflow opens a microflowId workbench tab without using the sample editor tab", async () => {
    const bundle = createBundle(vi.fn(async () => ({
      items: [createResource({ id: "mf-1", name: "MF_ApprovePurchase", displayName: "Approve Purchase" })],
      total: 1,
      pageIndex: 1,
      pageSize: 100,
    })));

    render(<AppExplorer adapterBundle={bundle} workspaceId="workspace-1" />);

    fireEvent.click(await screen.findByText("Approve Purchase"));

    const state = useMendixStudioStore.getState();
    expect(state.selectedExplorerNodeId).toBe("microflow:mf-1");
    expect(state.selectedKind).toBe("microflow");
    expect(state.activeModuleId).toBe("mod_procurement");
    expect(state.activeMicroflowId).toBe("mf-1");
    expect(state.workbenchTabs.some(tab => tab.id === "microflow:mf-1")).toBe(true);
    expect(state.activeWorkbenchTabId).toBe("microflow:mf-1");
    expect(state.activeTab).toBe("microflowDesigner");
  });

  it("refresh calls listMicroflows again and replaces stale module ids", async () => {
    const listMicroflows = vi
      .fn()
      .mockResolvedValueOnce({
        items: [createResource({ id: "mf-1", name: "MF_First", displayName: "First" })],
        total: 1,
        pageIndex: 1,
        pageSize: 100,
      })
      .mockResolvedValueOnce({
        items: [createResource({ id: "mf-2", name: "MF_Second", displayName: "Second" })],
        total: 1,
        pageIndex: 1,
        pageSize: 100,
      });
    const bundle = createBundle(listMicroflows);

    render(<AppExplorer adapterBundle={bundle} workspaceId="workspace-1" />);

    expect(await screen.findByText("First")).toBeTruthy();
    fireEvent.click(screen.getByTitle("Refresh Microflows"));

    expect(await screen.findByText("Second")).toBeTruthy();
    await waitFor(() => expect(screen.queryByText("First")).toBeNull());
    expect(listMicroflows).toHaveBeenCalledTimes(2);
    expect(useMendixStudioStore.getState().microflowIdsByModuleId.mod_procurement).toEqual(["mf-2"]);
    expect(useMendixStudioStore.getState().microflowResourcesById["mf-1"]).toBeUndefined();
  });
});
