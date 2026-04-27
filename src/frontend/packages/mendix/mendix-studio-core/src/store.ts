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

export type ActiveTabId = "page" | "microflow" | "workflow";
export type InspectorTab = "property" | "style" | "binding" | "structure";
export type BottomTab = "errors" | "trace";

type StudioState = {
  appSchema: LowCodeAppSchema;
  activeTab: MendixStudioTab;
  activeTabId: ActiveTabId;
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

  /** 微流资产 CRUD action（仅更新 store 索引，不调用 API） */
  setModuleMicroflows: (moduleId: string, microflows: StudioMicroflowDefinitionView[]) => void;
  upsertStudioMicroflow: (resource: StudioMicroflowDefinitionView) => void;
  removeStudioMicroflow: (id: string) => void;
};

const INITIAL_MICROFLOW_SCHEMA: MicroflowSchema = {
  ...sampleOrderProcessingMicroflow,
  version: "1.0.0"
};

export const useMendixStudioStore = create<StudioState>((set, get) => ({
  appSchema: SAMPLE_PROCUREMENT_APP,
  activeTab: "pageBuilder",
  activeTabId: "page",
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
  setActiveTabId: activeTabId => set({ activeTabId }),
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
      microflowIdsByModuleId: { ...microflowIdsByModuleId, [resource.moduleId]: nextModuleIds }
    });
  },

  removeStudioMicroflow: id => {
    const { microflowResourcesById, microflowIdsByModuleId } = get();
    const resource = microflowResourcesById[id];
    const nextById = { ...microflowResourcesById };
    delete nextById[id];
    if (!resource) {
      set({ microflowResourcesById: nextById });
      return;
    }
    const filteredIds = (microflowIdsByModuleId[resource.moduleId] ?? []).filter(mid => mid !== id);
    set({
      microflowResourcesById: nextById,
      microflowIdsByModuleId: { ...microflowIdsByModuleId, [resource.moduleId]: filteredIds }
    });
  },
}));
