import { create } from "zustand";
import type { ExecuteActionResponse, FlowExecutionTraceSchema, LowCodeAppSchema, ValidationErrorSchema } from "@atlas/mendix-schema";
import { SAMPLE_PROCUREMENT_APP, SAMPLE_RUNTIME_OBJECT } from "./sample-app";

export type MendixStudioTab =
  | "domainModel"
  | "pageBuilder"
  | "microflowDesigner"
  | "workflowDesigner"
  | "securityEditor"
  | "runtimePreview";

type StudioState = {
  appSchema: LowCodeAppSchema;
  activeTab: MendixStudioTab;
  selectedId?: string;
  selectedKind?: string;
  runtimeObject: Record<string, unknown>;
  validationErrors: ValidationErrorSchema[];
  latestActionResponse?: ExecuteActionResponse;
  latestTrace?: FlowExecutionTraceSchema;
  setAppSchema: (schema: LowCodeAppSchema) => void;
  setActiveTab: (tab: MendixStudioTab) => void;
  setSelected: (kind: string, id: string) => void;
  setValidationErrors: (errors: ValidationErrorSchema[]) => void;
  setRuntimeObject: (next: Record<string, unknown>) => void;
  setLatestActionResponse: (response?: ExecuteActionResponse) => void;
  setLatestTrace: (trace?: FlowExecutionTraceSchema) => void;
  loadSampleApp: () => void;
};

export const useMendixStudioStore = create<StudioState>(set => ({
  appSchema: SAMPLE_PROCUREMENT_APP,
  activeTab: "domainModel",
  runtimeObject: SAMPLE_RUNTIME_OBJECT,
  validationErrors: [],
  setAppSchema: appSchema => set({ appSchema }),
  setActiveTab: activeTab => set({ activeTab }),
  setSelected: (selectedKind, selectedId) => set({ selectedKind, selectedId }),
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
