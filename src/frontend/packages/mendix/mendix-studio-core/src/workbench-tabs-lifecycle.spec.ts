import { beforeEach, describe, expect, it, vi } from "vitest";

import { useMendixStudioStore } from "./store";
import type { StudioMicroflowDefinitionView } from "./microflow/studio/studio-microflow-types";

vi.mock("@atlas/microflow", () => ({
  sampleOrderProcessingMicroflow: {
    schemaVersion: "test",
    id: "sampleOrderProcessingMicroflow",
    name: "sampleOrderProcessingMicroflow",
    nodes: [],
    edges: [],
  },
}));

function createMicroflow(input: Partial<StudioMicroflowDefinitionView> & Pick<StudioMicroflowDefinitionView, "id" | "name">): StudioMicroflowDefinitionView {
  return {
    id: input.id,
    moduleId: input.moduleId ?? "mod_procurement",
    moduleName: input.moduleName ?? "Procurement",
    name: input.name,
    displayName: input.displayName ?? input.name,
    qualifiedName: input.qualifiedName ?? `Procurement.${input.name}`,
    description: input.description,
    status: input.status ?? "draft",
    publishStatus: input.publishStatus ?? "neverPublished",
    schemaId: input.schemaId ?? `schema-${input.id}`,
    version: input.version ?? "1",
    latestPublishedVersion: input.latestPublishedVersion,
    referenceCount: input.referenceCount ?? 0,
    favorite: input.favorite ?? false,
    archived: input.archived ?? false,
    createdAt: input.createdAt ?? "2026-04-28T00:00:00.000Z",
    updatedAt: input.updatedAt ?? "2026-04-28T00:00:00.000Z",
  };
}

beforeEach(() => {
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
    selectedExplorerNodeId: "page",
    selectedKind: undefined,
    selectedId: undefined,
    dirtyByWorkbenchTabId: {},
    saveStateByMicroflowId: {},
    savingByMicroflowId: {},
    saveErrorByMicroflowId: {},
    saveConflictByMicroflowId: {},
    canUndoByWorkbenchTabId: {},
    canRedoByWorkbenchTabId: {},
    pendingCloseTabId: undefined,
    tabCloseGuardOpen: false,
    microflowResourcesById: {},
    microflowIdsByModuleId: {},
  });
});

describe("Workbench microflow document lifecycle", () => {
  it("opens one tab per microflowId and activates existing tabs on repeated open", () => {
    const first = createMicroflow({ id: "mf-a", name: "MF_ValidatePurchaseRequest", displayName: "Validate Purchase Request" });
    const second = createMicroflow({ id: "mf-b", name: "MF_CalculateApprovalLevel", displayName: "Calculate Approval Level" });
    useMendixStudioStore.getState().setModuleMicroflows("mod_procurement", [first, second]);

    useMendixStudioStore.getState().openMicroflowWorkbenchTab("mf-a");
    useMendixStudioStore.getState().openMicroflowWorkbenchTab("mf-a");
    useMendixStudioStore.getState().openMicroflowWorkbenchTab("mf-b");

    const state = useMendixStudioStore.getState();
    expect(state.workbenchTabs.filter(tab => tab.id === "microflow:mf-a")).toHaveLength(1);
    expect(state.workbenchTabs.filter(tab => tab.id === "microflow:mf-b")).toHaveLength(1);
    expect(state.activeWorkbenchTabId).toBe("microflow:mf-b");
    expect(state.activeMicroflowId).toBe("mf-b");
    expect(state.workbenchTabs.find(tab => tab.id === "microflow:mf-a")?.title).toBe("Validate Purchase Request");
  });

  it("keeps activeWorkbenchTabId and activeMicroflowId synchronized while switching and closing", () => {
    const first = createMicroflow({ id: "mf-a", name: "MF_A" });
    const second = createMicroflow({ id: "mf-b", name: "MF_B" });
    useMendixStudioStore.getState().setModuleMicroflows("mod_procurement", [first, second]);
    useMendixStudioStore.getState().openMicroflowWorkbenchTab("mf-a");
    useMendixStudioStore.getState().openMicroflowWorkbenchTab("mf-b");

    useMendixStudioStore.getState().setActiveWorkbenchTab("microflow:mf-a");
    expect(useMendixStudioStore.getState().activeMicroflowId).toBe("mf-a");

    useMendixStudioStore.getState().closeWorkbenchTab("microflow:mf-a");
    expect(useMendixStudioStore.getState().activeWorkbenchTabId).toBe("microflow:mf-b");
    expect(useMendixStudioStore.getState().activeMicroflowId).toBe("mf-b");

    useMendixStudioStore.getState().closeWorkbenchTab("microflow:mf-b");
    expect(useMendixStudioStore.getState().activeWorkbenchTabId).toBe("page");
    expect(useMendixStudioStore.getState().activeMicroflowId).toBeUndefined();
  });

  it("uses per-tab dirty state and opens a close guard before discarding", () => {
    const microflow = createMicroflow({ id: "mf-dirty", name: "MF_Dirty" });
    useMendixStudioStore.getState().setModuleMicroflows("mod_procurement", [microflow]);
    useMendixStudioStore.getState().openMicroflowWorkbenchTab("mf-dirty");
    useMendixStudioStore.getState().markWorkbenchTabDirty("microflow:mf-dirty", true);

    useMendixStudioStore.getState().closeWorkbenchTab("microflow:mf-dirty");
    expect(useMendixStudioStore.getState().tabCloseGuardOpen).toBe(true);
    expect(useMendixStudioStore.getState().pendingCloseTabId).toBe("microflow:mf-dirty");
    expect(useMendixStudioStore.getState().workbenchTabs.some(tab => tab.id === "microflow:mf-dirty")).toBe(true);

    useMendixStudioStore.getState().closeWorkbenchTab("microflow:mf-dirty", { force: true });
    expect(useMendixStudioStore.getState().workbenchTabs.some(tab => tab.id === "microflow:mf-dirty")).toBe(false);
    expect(useMendixStudioStore.getState().dirtyByWorkbenchTabId["microflow:mf-dirty"]).toBeUndefined();
  });

  it("syncs rename/delete and keeps duplicate resources from overwriting source tabs", () => {
    const source = createMicroflow({ id: "mf-source", name: "MF_Source", displayName: "Source" });
    const duplicate = createMicroflow({ id: "mf-copy", name: "MF_Source_Copy", displayName: "Source Copy" });
    useMendixStudioStore.getState().setModuleMicroflows("mod_procurement", [source]);
    useMendixStudioStore.getState().openMicroflowWorkbenchTab("mf-source");

    useMendixStudioStore.getState().upsertStudioMicroflow({
      ...source,
      displayName: "Renamed Source",
      qualifiedName: "Procurement.MF_RenamedSource",
    });
    expect(useMendixStudioStore.getState().workbenchTabs.find(tab => tab.id === "microflow:mf-source")?.title).toBe("Renamed Source");

    useMendixStudioStore.getState().upsertStudioMicroflow(duplicate);
    expect(useMendixStudioStore.getState().workbenchTabs.some(tab => tab.id === "microflow:mf-copy")).toBe(false);
    expect(useMendixStudioStore.getState().workbenchTabs.find(tab => tab.id === "microflow:mf-source")?.microflowId).toBe("mf-source");

    useMendixStudioStore.getState().removeStudioMicroflow("mf-source");
    expect(useMendixStudioStore.getState().workbenchTabs.some(tab => tab.id === "microflow:mf-source")).toBe(false);
    expect(useMendixStudioStore.getState().activeMicroflowId).toBeUndefined();
  });

  it("keeps dirty and save status isolated per microflow", () => {
    const first = createMicroflow({ id: "mf-a", name: "MF_A" });
    const second = createMicroflow({ id: "mf-b", name: "MF_B" });
    useMendixStudioStore.getState().setModuleMicroflows("mod_procurement", [first, second]);
    useMendixStudioStore.getState().openMicroflowWorkbenchTab("mf-a");
    useMendixStudioStore.getState().openMicroflowWorkbenchTab("mf-b");

    useMendixStudioStore.getState().markMicroflowDirty("mf-a", true);
    useMendixStudioStore.getState().updateMicroflowSaveState("mf-b", {
      status: "error",
      dirty: true,
      saving: false,
      queued: false,
      lastError: { code: "MICROFLOW_SERVICE_UNAVAILABLE", message: "boom" },
    });
    useMendixStudioStore.getState().updateMicroflowSaveState("mf-a", {
      status: "saved",
      dirty: false,
      saving: false,
      queued: false,
      lastSavedAt: "2026-04-28T01:00:00.000Z",
    });

    const state = useMendixStudioStore.getState();
    expect(state.saveStateByMicroflowId["mf-a"].dirty).toBe(false);
    expect(state.saveStateByMicroflowId["mf-a"].status).toBe("saved");
    expect(state.saveStateByMicroflowId["mf-b"].dirty).toBe(true);
    expect(state.saveStateByMicroflowId["mf-b"].status).toBe("error");
    expect(state.dirtyByWorkbenchTabId["microflow:mf-b"]).toBe(true);
    expect(state.dirtyByWorkbenchTabId["microflow:mf-a"]).toBeUndefined();
  });

  it("clears save state on delete but keeps it on rename", () => {
    const source = createMicroflow({ id: "mf-source", name: "MF_Source", displayName: "Source" });
    useMendixStudioStore.getState().setModuleMicroflows("mod_procurement", [source]);
    useMendixStudioStore.getState().openMicroflowWorkbenchTab("mf-source");
    useMendixStudioStore.getState().updateMicroflowSaveState("mf-source", {
      status: "dirty",
      dirty: true,
      saving: false,
      queued: false,
    });

    useMendixStudioStore.getState().renameMicroflowWorkbenchTab("mf-source", "Renamed", "Procurement.Renamed");
    expect(useMendixStudioStore.getState().saveStateByMicroflowId["mf-source"].dirty).toBe(true);

    useMendixStudioStore.getState().removeStudioMicroflow("mf-source");
    expect(useMendixStudioStore.getState().saveStateByMicroflowId["mf-source"]).toBeUndefined();
    expect(useMendixStudioStore.getState().dirtyByWorkbenchTabId["microflow:mf-source"]).toBeUndefined();
  });
});
