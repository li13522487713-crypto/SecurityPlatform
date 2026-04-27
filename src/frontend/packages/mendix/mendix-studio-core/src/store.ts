import { create } from "zustand";
import type {
  ExecuteActionResponse,
  FlowExecutionTraceSchema,
  LowCodeAppSchema,
  ValidationErrorSchema
} from "@atlas/mendix-schema";
import { sampleOrderProcessingMicroflow } from "@atlas/microflow";
import type { MicroflowSchema } from "@atlas/microflow";
import { SAMPLE_PROCUREMENT_APP, SAMPLE_RUNTIME_OBJECT } from "./sample-app";
import type { StudioMicroflowDefinitionView } from "./microflow/studio/studio-microflow-types";

export type MendixStudioTab =
  | "domainModel"
  | "pageBuilder"
  | "microflowDesigner"
  | "workflowDesigner"
  | "securityEditor"
  | "runtimePreview";

export type ActiveTabId = string;
export type InspectorTab = "property" | "style" | "binding" | "structure";
export type BottomTab = "errors" | "trace";

export type StudioWorkbenchTabKind =
  | "page"
  | "microflow"
  | "workflow"
  | "domainModel"
  | "other";

export interface StudioWorkbenchTab {
  id: string;
  kind: StudioWorkbenchTabKind;
  title: string;
  moduleId?: string;
  resourceId?: string;
  microflowId?: string;
  dirty?: boolean;
  closable?: boolean;
}

type StudioState = {
  appSchema: LowCodeAppSchema;
  activeTab: MendixStudioTab;
  activeTabId?: ActiveTabId;
  workbenchTabs: StudioWorkbenchTab[];
  activeWorkbenchTabId?: string;
  selectedId?: string;
  selectedKind?: string;
  selectedWidgetId: string;
  selectedExplorerNodeId: string;
  previewMode: boolean;
  inspectorTab: InspectorTab;
  bottomTab: BottomTab;
  runtimeObject: Record<string, unknown>;
  validationErrors: ValidationErrorSchema[];
  latestActionResponse?: ExecuteActionResponse;
  latestTrace?: FlowExecutionTraceSchema;

  microflowSchema: MicroflowSchema;
  microflowImmersive: boolean;

  /** Studio 上下文字段（由 MendixStudioApp 在渲染时写入） */
  workspaceId?: string;
  appId?: string;

  /** 微流资产索引（仅存放展示层视图，不存 MicroflowAuthoringSchema） */
  activeModuleId?: string;
  activeMicroflowId?: string;
  microflowResourcesById: Record<string, StudioMicroflowDefinitionView>;
  microflowIdsByModuleId: Record<string, string[]>;

  setAppSchema: (schema: LowCodeAppSchema) => void;
  setActiveTab: (tab: MendixStudioTab) => void;
  setActiveTabId: (id: ActiveTabId) => void;
  setSelected: (kind: string, id: string) => void;
  setSelectedWidgetId: (id: string) => void;
  setSelectedExplorerNodeId: (id: string) => void;
  setPreviewMode: (mode: boolean) => void;
  setInspectorTab: (tab: InspectorTab) => void;
  setBottomTab: (tab: BottomTab) => void;
  setValidationErrors: (errors: ValidationErrorSchema[]) => void;
  setRuntimeObject: (next: Record<string, unknown>) => void;
  setLatestActionResponse: (response?: ExecuteActionResponse) => void;
  setLatestTrace: (trace?: FlowExecutionTraceSchema) => void;
  setMicroflowSchema: (schema: MicroflowSchema) => void;
  setMicroflowImmersive: (immersive: boolean) => void;
  loadSampleApp: () => void;

  /** Studio 上下文 action */
  setStudioContext: (input: { workspaceId?: string; appId?: string }) => void;
  setActiveModuleId: (moduleId?: string) => void;
  setActiveMicroflowId: (microflowId?: string) => void;
  setActiveWorkbenchTab: (tabId: string) => void;
  openMicroflowWorkbenchTab: (microflowId: string) => void;
  closeWorkbenchTab: (tabId: string) => void;
  renameMicroflowWorkbenchTab: (microflowId: string, title: string) => void;
  removeMicroflowWorkbenchTab: (microflowId: string) => void;

  /** 微流资产 CRUD action（仅更新 store 索引，不调用 API） */
  setModuleMicroflows: (moduleId: string, microflows: StudioMicroflowDefinitionView[]) => void;
  upsertStudioMicroflow: (resource: StudioMicroflowDefinitionView) => void;
  removeStudioMicroflow: (id: string) => void;
};

const INITIAL_MICROFLOW_SCHEMA: MicroflowSchema = {
  ...sampleOrderProcessingMicroflow
};

const INITIAL_WORKBENCH_TABS: StudioWorkbenchTab[] = [
  {
    id: "page",
    kind: "page",
    title: "PurchaseRequest_EditPage",
    resourceId: "page_purchase_request_edit",
    closable: false
  },
  {
    id: "workflow",
    kind: "workflow",
    title: "WF_PurchaseApproval",
    resourceId: "wf_purchase_approval",
    closable: false
  }
];

function getStudioTabForWorkbenchKind(kind: StudioWorkbenchTabKind): MendixStudioTab {
  switch (kind) {
    case "page":
      return "pageBuilder";
    case "microflow":
      return "microflowDesigner";
    case "workflow":
      return "workflowDesigner";
    case "domainModel":
      return "domainModel";
    default:
      return "pageBuilder";
  }
}

function getMicroflowWorkbenchTabId(microflowId: string): string {
  return `microflow:${microflowId}`;
}

function createMicroflowWorkbenchTab(resource: StudioMicroflowDefinitionView): StudioWorkbenchTab {
  return {
    id: getMicroflowWorkbenchTabId(resource.id),
    kind: "microflow",
    title: resource.displayName || resource.name,
    moduleId: resource.moduleId,
    resourceId: resource.id,
    microflowId: resource.id,
    closable: true
  };
}

export const useMendixStudioStore = create<StudioState>((set, get) => ({
  appSchema: SAMPLE_PROCUREMENT_APP,
  activeTab: "pageBuilder",
  activeTabId: "page",
  workbenchTabs: INITIAL_WORKBENCH_TABS,
  activeWorkbenchTabId: "page",
  selectedWidgetId: "widget_submit_btn",
  selectedExplorerNodeId: "page_purchase_request_edit",
  previewMode: false,
  inspectorTab: "property",
  bottomTab: "errors",
  runtimeObject: SAMPLE_RUNTIME_OBJECT,
  validationErrors: [],
  microflowSchema: INITIAL_MICROFLOW_SCHEMA,
  microflowImmersive: false,

  microflowResourcesById: {},
  microflowIdsByModuleId: {},

  setAppSchema: appSchema => set({ appSchema }),
  setActiveTab: activeTab => set({ activeTab }),
  setActiveTabId: activeTabId => {
    const tab = get().workbenchTabs.find(item => item.id === activeTabId);
    if (!tab) {
      set({ activeTabId, activeWorkbenchTabId: activeTabId });
      return;
    }
    get().setActiveWorkbenchTab(tab.id);
  },
  setSelected: (selectedKind, selectedId) => set({ selectedKind, selectedId }),
  setSelectedWidgetId: selectedWidgetId => set({ selectedWidgetId }),
  setSelectedExplorerNodeId: selectedExplorerNodeId => set({ selectedExplorerNodeId }),
  setPreviewMode: previewMode => set({ previewMode }),
  setInspectorTab: inspectorTab => set({ inspectorTab }),
  setBottomTab: bottomTab => set({ bottomTab }),
  setValidationErrors: validationErrors => set({ validationErrors }),
  setRuntimeObject: runtimeObject => set({ runtimeObject }),
  setLatestActionResponse: latestActionResponse => set({ latestActionResponse }),
  setLatestTrace: latestTrace => set({ latestTrace }),
  setMicroflowSchema: microflowSchema => set({ microflowSchema }),
  setMicroflowImmersive: microflowImmersive => set({ microflowImmersive }),
  loadSampleApp: () =>
    set({
      appSchema: JSON.parse(JSON.stringify(SAMPLE_PROCUREMENT_APP)) as LowCodeAppSchema,
      runtimeObject: { ...SAMPLE_RUNTIME_OBJECT }
    }),

  setStudioContext: ({ workspaceId, appId }) => set({ workspaceId, appId }),
  setActiveModuleId: activeModuleId => set({ activeModuleId }),
  setActiveMicroflowId: activeMicroflowId => set({ activeMicroflowId }),

  setActiveWorkbenchTab: tabId => {
    const tab = get().workbenchTabs.find(item => item.id === tabId);
    if (!tab) {
      console.warn(`[MendixStudioStore] Cannot activate missing workbench tab: ${tabId}`);
      return;
    }

    const nextState: Partial<StudioState> = {
      activeWorkbenchTabId: tab.id,
      activeTabId: tab.id,
      activeTab: getStudioTabForWorkbenchKind(tab.kind)
    };

    if (tab.kind === "microflow") {
      nextState.activeMicroflowId = tab.microflowId ?? tab.resourceId;
      nextState.activeModuleId = tab.moduleId;
    } else {
      nextState.activeMicroflowId = undefined;
    }

    set(nextState);
  },

  openMicroflowWorkbenchTab: microflowId => {
    const resource = get().microflowResourcesById[microflowId];
    if (!resource) {
      console.warn(`[MendixStudioStore] Cannot open missing microflow resource: ${microflowId}`);
      return;
    }

    const tabId = getMicroflowWorkbenchTabId(microflowId);
    const existingTab = get().workbenchTabs.find(tab => tab.id === tabId);
    const nextTabs = existingTab
      ? get().workbenchTabs
      : [...get().workbenchTabs, createMicroflowWorkbenchTab(resource)];

    set({
      workbenchTabs: nextTabs,
      activeWorkbenchTabId: tabId,
      activeTabId: tabId,
      activeTab: "microflowDesigner",
      activeMicroflowId: microflowId,
      activeModuleId: resource.moduleId
    });
  },

  closeWorkbenchTab: tabId => {
    const { workbenchTabs, activeWorkbenchTabId } = get();
    const closingIndex = workbenchTabs.findIndex(tab => tab.id === tabId);
    if (closingIndex < 0) {
      return;
    }

    const closingTab = workbenchTabs[closingIndex];
    if (closingTab.closable === false) {
      return;
    }

    const nextTabs = workbenchTabs.filter(tab => tab.id !== tabId);
    if (activeWorkbenchTabId !== tabId) {
      const shouldClearActiveMicroflow =
        closingTab.kind === "microflow" &&
        get().activeMicroflowId === (closingTab.microflowId ?? closingTab.resourceId) &&
        nextTabs.find(tab => tab.id === activeWorkbenchTabId)?.kind !== "microflow";
      set({
        workbenchTabs: nextTabs,
        activeMicroflowId: shouldClearActiveMicroflow ? undefined : get().activeMicroflowId
      });
      return;
    }

    const nextActiveTab =
      nextTabs[Math.min(closingIndex, nextTabs.length - 1)] ??
      nextTabs[closingIndex - 1];

    if (!nextActiveTab) {
      set({
        workbenchTabs: nextTabs,
        activeWorkbenchTabId: undefined,
        activeTabId: undefined,
        activeMicroflowId: undefined,
        activeTab: "pageBuilder"
      });
      return;
    }

    set({
      workbenchTabs: nextTabs,
      activeWorkbenchTabId: nextActiveTab.id,
      activeTabId: nextActiveTab.id,
      activeTab: getStudioTabForWorkbenchKind(nextActiveTab.kind),
      activeMicroflowId: nextActiveTab.kind === "microflow"
        ? nextActiveTab.microflowId ?? nextActiveTab.resourceId
        : undefined,
      activeModuleId: nextActiveTab.kind === "microflow"
        ? nextActiveTab.moduleId
        : get().activeModuleId
    });
  },

  renameMicroflowWorkbenchTab: (microflowId, title) => {
    const tabId = getMicroflowWorkbenchTabId(microflowId);
    set({
      workbenchTabs: get().workbenchTabs.map(tab =>
        tab.id === tabId
          ? { ...tab, title }
          : tab
      )
    });
  },

  removeMicroflowWorkbenchTab: microflowId => {
    const tabId = getMicroflowWorkbenchTabId(microflowId);
    const exists = get().workbenchTabs.some(tab => tab.id === tabId);
    if (!exists) {
      if (get().activeMicroflowId === microflowId) {
        set({ activeMicroflowId: undefined });
      }
      return;
    }
    get().closeWorkbenchTab(tabId);
    if (get().activeMicroflowId === microflowId) {
      set({ activeMicroflowId: undefined });
    }
  },

  setModuleMicroflows: (moduleId, microflows) => {
    const { microflowResourcesById, microflowIdsByModuleId } = get();
    const previousIds = new Set(microflowIdsByModuleId[moduleId] ?? []);
    const nextById = { ...microflowResourcesById };
    for (const id of previousIds) {
      delete nextById[id];
    }
    for (const resource of microflows) {
      nextById[resource.id] = resource;
    }
    set({
      microflowResourcesById: nextById,
      microflowIdsByModuleId: {
        ...microflowIdsByModuleId,
        [moduleId]: microflows.map(resource => resource.id)
      }
    });
  },

  upsertStudioMicroflow: resource => {
    const { microflowResourcesById, microflowIdsByModuleId } = get();
    const nextById = { ...microflowResourcesById, [resource.id]: resource };
    const existingIds = microflowIdsByModuleId[resource.moduleId] ?? [];
    const nextModuleIds = existingIds.includes(resource.id)
      ? existingIds
      : [...existingIds, resource.id];
    set({
      microflowResourcesById: nextById,
      microflowIdsByModuleId: { ...microflowIdsByModuleId, [resource.moduleId]: nextModuleIds },
      workbenchTabs: get().workbenchTabs.map(tab =>
        tab.id === getMicroflowWorkbenchTabId(resource.id)
          ? {
              ...tab,
              title: resource.displayName || resource.name,
              moduleId: resource.moduleId,
              resourceId: resource.id,
              microflowId: resource.id
            }
          : tab
      )
    });
  },

  removeStudioMicroflow: id => {
    const { microflowResourcesById, microflowIdsByModuleId, selectedExplorerNodeId } = get();
    const resource = microflowResourcesById[id];
    get().removeMicroflowWorkbenchTab(id);
    const nextById = { ...microflowResourcesById };
    delete nextById[id];
    if (!resource) {
      set({
        activeMicroflowId: get().activeMicroflowId === id ? undefined : get().activeMicroflowId,
        microflowResourcesById: nextById,
        selectedExplorerNodeId: selectedExplorerNodeId === `microflow:${id}` ? "microflows" : selectedExplorerNodeId
      });
      return;
    }
    const filteredIds = (microflowIdsByModuleId[resource.moduleId] ?? []).filter(mid => mid !== id);
    set({
      activeMicroflowId: get().activeMicroflowId === id ? undefined : get().activeMicroflowId,
      microflowResourcesById: nextById,
      microflowIdsByModuleId: { ...microflowIdsByModuleId, [resource.moduleId]: filteredIds },
      selectedExplorerNodeId: selectedExplorerNodeId === `microflow:${id}` ? "microflows" : selectedExplorerNodeId
    });
  },
}));
