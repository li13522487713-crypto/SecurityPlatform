import { create } from "zustand";
import type {
  ExecuteActionResponse,
  FlowExecutionTraceSchema,
  LowCodeAppSchema,
  ValidationErrorSchema
} from "@atlas/mendix-schema";
import { SAMPLE_PROCUREMENT_APP, SAMPLE_RUNTIME_OBJECT } from "./sample-app";

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
  loadSampleApp: () => void;
};

export const useMendixStudioStore = create<StudioState>(set => ({
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
  loadSampleApp: () =>
    set({
      appSchema: JSON.parse(JSON.stringify(SAMPLE_PROCUREMENT_APP)) as LowCodeAppSchema,
      runtimeObject: { ...SAMPLE_RUNTIME_OBJECT }
    })
}));
