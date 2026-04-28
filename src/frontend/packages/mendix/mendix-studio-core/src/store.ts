import { create } from "zustand";
import type {
  ExecuteActionResponse,
  FlowExecutionTraceSchema,
  LowCodeAppSchema,
  ValidationErrorSchema
} from "@atlas/mendix-schema";
import type { MicroflowSchema, MicroflowValidationIssue } from "@atlas/microflow";
import { SAMPLE_PROCUREMENT_APP, SAMPLE_RUNTIME_OBJECT } from "./sample-app";
import type { StudioMicroflowDefinitionView } from "./microflow/studio/studio-microflow-types";
import { mapMicroflowResourceToStudioDefinitionView } from "./microflow/studio/studio-microflow-mappers";
import type { MicroflowResource } from "./microflow/resource/resource-types";
import type { MicroflowApiError } from "./microflow/contracts/api/api-envelope";
import type { MicroflowFolder } from "./microflow/folders/microflow-folder-types";

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
  | "security"
  | "navigation"
  | "other";

export interface StudioWorkbenchTab {
  id: string;
  kind: StudioWorkbenchTabKind;
  title: string;
  subtitle?: string;
  moduleId?: string;
  resourceId?: string;
  microflowId?: string;
  qualifiedName?: string;
  status?: string;
  publishStatus?: string;
  dirty?: boolean;
  closable?: boolean;
  icon?: string;
  openedAt: string;
  updatedAt?: string;
  historyKey?: string;
}

export interface MicroflowValidationSummary {
  errorCount: number;
  warningCount: number;
  infoCount: number;
  totalCount: number;
  saveBlockerCount: number;
  publishBlockerCount: number;
  lastValidatedAt?: string;
  status?: string;
}

export interface StudioMicroflowValidationState {
  microflowId: string;
  issues: MicroflowValidationIssue[];
  running: boolean;
  lastValidatedAt?: string;
  lastError?: string;
  requestId?: string;
}

export type MicroflowSaveStatus =
  | "idle"
  | "dirty"
  | "saving"
  | "saved"
  | "error"
  | "conflict"
  | "autosaving"
  | "queued";

export interface MicroflowSaveConflict {
  microflowId: string;
  localVersion?: string;
  baseVersion?: string;
  remoteVersion?: string;
  remoteUpdatedAt?: string;
  remoteUpdatedBy?: string;
  traceId?: string;
  message: string;
}

export interface MicroflowSaveState {
  microflowId: string;
  tabId: string;
  status: MicroflowSaveStatus;
  dirty: boolean;
  saving: boolean;
  queued: boolean;
  lastSavedAt?: string;
  lastSavedBy?: string;
  lastSaveDurationMs?: number;
  lastError?: MicroflowApiError;
  conflict?: MicroflowSaveConflict;
  localVersion?: string;
  remoteVersion?: string;
  schemaId?: string;
  baseVersion?: string;
}

type StudioState = {
  appSchema: LowCodeAppSchema;
  activeTab: MendixStudioTab;
  activeTabId?: ActiveTabId;
  workbenchTabs: StudioWorkbenchTab[];
  activeWorkbenchTabId?: string;
  dirtyByWorkbenchTabId: Record<string, boolean>;
  saveStateByMicroflowId: Record<string, MicroflowSaveState>;
  savingByMicroflowId: Record<string, boolean>;
  saveErrorByMicroflowId: Record<string, MicroflowApiError | undefined>;
  saveConflictByMicroflowId: Record<string, MicroflowSaveConflict | undefined>;
  canUndoByWorkbenchTabId: Record<string, boolean>;
  canRedoByWorkbenchTabId: Record<string, boolean>;
  lastActiveMicroflowTabId?: string;
  pendingCloseTabId?: string;
  tabCloseGuardOpen?: boolean;
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
  foldersByModuleId: Record<string, MicroflowFolder[]>;
  folderLoadingByModuleId: Record<string, boolean>;
  folderErrorByModuleId: Record<string, MicroflowApiError | undefined>;
  microflowsLoadStateByModuleId: Record<string, "idle" | "loading" | "success" | "error">;
  expandedExplorerNodeIds: string[];
  microflowTreeSearchKeyword: string;
  validationByMicroflowId: Record<string, StudioMicroflowValidationState>;
  validationSummaryByMicroflowId: Record<string, MicroflowValidationSummary>;
  problemsPanelOpen: boolean;
  activeProblemsMicroflowId?: string;

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
  closeWorkbenchTab: (tabId: string, options?: { force?: boolean }) => void;
  cancelWorkbenchTabCloseGuard: () => void;
  renameMicroflowWorkbenchTab: (microflowId: string, title: string, subtitle?: string) => void;
  removeMicroflowWorkbenchTab: (microflowId: string) => void;
  markWorkbenchTabDirty: (tabId: string, dirty: boolean) => void;
  markMicroflowDirty: (microflowId: string, dirty: boolean) => void;
  updateMicroflowSaveState: (microflowId: string, patch: Partial<MicroflowSaveState>) => void;
  clearMicroflowSaveState: (microflowId: string) => void;
  updateMicroflowWorkbenchTabFromResource: (resource: StudioMicroflowDefinitionView) => void;

  /** 微流资产 CRUD action（仅更新 store 索引，不调用 API） */
  setModuleMicroflows: (moduleId: string, microflows: StudioMicroflowDefinitionView[]) => void;
  setFoldersForModule: (moduleId: string, folders: MicroflowFolder[]) => void;
  upsertFolder: (folder: MicroflowFolder) => void;
  removeFolder: (folderId: string, moduleId?: string) => void;
  setFolderLoading: (moduleId: string, loading: boolean) => void;
  setFolderError: (moduleId: string, error?: MicroflowApiError) => void;
  setModuleMicroflowsLoadState: (moduleId: string, loadState: "idle" | "loading" | "success" | "error") => void;
  setExpandedExplorerNodeIds: (nodeIds: string[]) => void;
  setMicroflowTreeSearchKeyword: (keyword: string) => void;
  upsertStudioMicroflow: (resource: StudioMicroflowDefinitionView) => void;
  updateStudioMicroflowFromResource: (resource: MicroflowResource) => void;
  removeStudioMicroflow: (id: string) => void;
  setMicroflowValidationState: (input: { microflowId: string; issues: MicroflowValidationIssue[]; status?: string; lastValidatedAt?: Date | string }) => void;
  setValidationRunning: (microflowId: string, running: boolean, requestId?: string) => void;
  setValidationError: (microflowId: string, error?: unknown) => void;
  openProblemsPanel: (microflowId: string) => void;
  clearValidationForMicroflow: (microflowId: string) => void;
};

const INITIAL_MICROFLOW_SCHEMA: MicroflowSchema = {
  schemaVersion: "1.0.0",
  mendixProfile: "mx10",
  id: "unloaded",
  stableId: "unloaded",
  name: "Unloaded",
  displayName: "Unloaded",
  description: "No active microflow schema is loaded.",
  moduleId: "",
  parameters: [],
  returnType: { kind: "void" },
  objectCollection: {
    id: "unloaded-root",
    officialType: "Microflows$MicroflowObjectCollection",
    objects: [],
    flows: []
  },
  flows: [],
  security: { applyEntityAccess: true, allowedModuleRoleIds: [] },
  concurrency: { allowConcurrentExecution: true },
  exposure: { exportLevel: "hidden", markAsUsed: false },
  validation: { issues: [] },
  editor: {
    viewport: { x: 0, y: 0, zoom: 1 },
    zoom: 1,
    selection: {}
  },
  audit: {
    version: "v0",
    status: "draft"
  }
};

const INITIAL_WORKBENCH_TABS: StudioWorkbenchTab[] = [
  {
    id: "page",
    kind: "page",
    title: "PurchaseRequest_EditPage",
    resourceId: "page_purchase_request_edit",
    closable: false,
    openedAt: "2026-04-28T00:00:00.000Z",
    historyKey: "page"
  },
  {
    id: "workflow",
    kind: "workflow",
    title: "WF_PurchaseApproval",
    resourceId: "wf_purchase_approval",
    closable: false,
    openedAt: "2026-04-28T00:00:00.000Z",
    historyKey: "workflow"
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

function ensureMicroflowSaveState(
  current: Record<string, MicroflowSaveState>,
  microflowId: string,
  patch: Partial<MicroflowSaveState> = {}
): Record<string, MicroflowSaveState> {
  const previous = current[microflowId];
  const tabId = patch.tabId ?? previous?.tabId ?? getMicroflowWorkbenchTabId(microflowId);
  const dirty = patch.dirty ?? previous?.dirty ?? false;
  const saving = patch.saving ?? previous?.saving ?? false;
  const queued = patch.queued ?? previous?.queued ?? false;
  const status = patch.status ?? previous?.status ?? (dirty ? "dirty" : "idle");
  return {
    ...current,
    [microflowId]: {
      microflowId,
      tabId,
      status,
      dirty,
      saving,
      queued,
      lastSavedAt: patch.lastSavedAt ?? previous?.lastSavedAt,
      lastSavedBy: patch.lastSavedBy ?? previous?.lastSavedBy,
      lastSaveDurationMs: patch.lastSaveDurationMs ?? previous?.lastSaveDurationMs,
      lastError: Object.prototype.hasOwnProperty.call(patch, "lastError") ? patch.lastError : previous?.lastError,
      conflict: Object.prototype.hasOwnProperty.call(patch, "conflict") ? patch.conflict : previous?.conflict,
      localVersion: patch.localVersion ?? previous?.localVersion,
      remoteVersion: patch.remoteVersion ?? previous?.remoteVersion,
      schemaId: patch.schemaId ?? previous?.schemaId,
      baseVersion: patch.baseVersion ?? previous?.baseVersion
    }
  };
}

function createMicroflowWorkbenchTab(resource: StudioMicroflowDefinitionView): StudioWorkbenchTab {
  const tabId = getMicroflowWorkbenchTabId(resource.id);
  const now = new Date().toISOString();
  return {
    id: tabId,
    kind: "microflow",
    title: resource.displayName || resource.name,
    subtitle: resource.qualifiedName,
    moduleId: resource.moduleId,
    resourceId: resource.id,
    microflowId: resource.id,
    qualifiedName: resource.qualifiedName,
    status: resource.status,
    publishStatus: resource.publishStatus,
    closable: true,
    icon: "M",
    openedAt: now,
    updatedAt: now,
    historyKey: tabId
  };
}

function getDirtyTabRecord(
  current: Record<string, boolean>,
  tabId: string,
  dirty: boolean
): Record<string, boolean> {
  const next = { ...current };
  if (dirty) {
    next[tabId] = true;
  } else {
    delete next[tabId];
  }
  return next;
}

function omitTabRecord<T>(
  current: Record<string, T>,
  tabId: string
): Record<string, T> {
  const next = { ...current };
  delete next[tabId];
  return next;
}

function summarizeMicroflowValidationIssues(
  issues: MicroflowValidationIssue[],
  lastValidatedAt?: string,
  status?: string
): MicroflowValidationSummary {
  return {
    errorCount: issues.filter(issue => issue.severity === "error").length,
    warningCount: issues.filter(issue => issue.severity === "warning").length,
    infoCount: issues.filter(issue => issue.severity === "info").length,
    totalCount: issues.length,
    saveBlockerCount: issues.filter(issue => issue.severity === "error" && issue.blockSave).length,
    publishBlockerCount: issues.filter(issue => issue.severity === "error" && issue.blockPublish).length,
    lastValidatedAt,
    status,
  };
}

export const useMendixStudioStore = create<StudioState>((set, get) => ({
  appSchema: SAMPLE_PROCUREMENT_APP,
  activeTab: "pageBuilder",
  activeTabId: "page",
  workbenchTabs: INITIAL_WORKBENCH_TABS,
  activeWorkbenchTabId: "page",
  dirtyByWorkbenchTabId: {},
  saveStateByMicroflowId: {},
  savingByMicroflowId: {},
  saveErrorByMicroflowId: {},
  saveConflictByMicroflowId: {},
  canUndoByWorkbenchTabId: {},
  canRedoByWorkbenchTabId: {},
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
  foldersByModuleId: {},
  folderLoadingByModuleId: {},
  folderErrorByModuleId: {},
  microflowsLoadStateByModuleId: {},
  expandedExplorerNodeIds: [],
  microflowTreeSearchKeyword: "",
  validationByMicroflowId: {},
  validationSummaryByMicroflowId: {},
  problemsPanelOpen: false,

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
      nextState.lastActiveMicroflowTabId = tab.id;
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
      activeModuleId: resource.moduleId,
      lastActiveMicroflowTabId: tabId
    });
  },

  closeWorkbenchTab: (tabId, options) => {
    const {
      workbenchTabs,
      activeWorkbenchTabId,
      dirtyByWorkbenchTabId,
      saveStateByMicroflowId,
      canUndoByWorkbenchTabId,
      canRedoByWorkbenchTabId
    } = get();
    const closingIndex = workbenchTabs.findIndex(tab => tab.id === tabId);
    if (closingIndex < 0) {
      return;
    }

    const closingTab = workbenchTabs[closingIndex];
    if (closingTab.closable === false) {
      return;
    }

    const microflowId = closingTab.microflowId ?? closingTab.resourceId;
    const saveState = microflowId ? saveStateByMicroflowId[microflowId] : undefined;
    if ((dirtyByWorkbenchTabId[tabId] || closingTab.dirty || saveState?.saving || saveState?.queued) && options?.force !== true) {
      set({
        pendingCloseTabId: tabId,
        tabCloseGuardOpen: true
      });
      return;
    }

    const nextTabs = workbenchTabs.filter(tab => tab.id !== tabId);
    const nextDirtyByTabId = omitTabRecord(dirtyByWorkbenchTabId, tabId);
    const nextSaveStateByMicroflowId = microflowId ? omitTabRecord(saveStateByMicroflowId, microflowId) : saveStateByMicroflowId;
    const nextCanUndoByTabId = omitTabRecord(canUndoByWorkbenchTabId, tabId);
    const nextCanRedoByTabId = omitTabRecord(canRedoByWorkbenchTabId, tabId);
    if (activeWorkbenchTabId !== tabId) {
      const shouldClearActiveMicroflow =
        closingTab.kind === "microflow" &&
        get().activeMicroflowId === (closingTab.microflowId ?? closingTab.resourceId) &&
        nextTabs.find(tab => tab.id === activeWorkbenchTabId)?.kind !== "microflow";
      set({
        workbenchTabs: nextTabs,
        dirtyByWorkbenchTabId: nextDirtyByTabId,
        saveStateByMicroflowId: nextSaveStateByMicroflowId,
        savingByMicroflowId: microflowId ? omitTabRecord(get().savingByMicroflowId, microflowId) : get().savingByMicroflowId,
        saveErrorByMicroflowId: microflowId ? omitTabRecord(get().saveErrorByMicroflowId, microflowId) : get().saveErrorByMicroflowId,
        saveConflictByMicroflowId: microflowId ? omitTabRecord(get().saveConflictByMicroflowId, microflowId) : get().saveConflictByMicroflowId,
        canUndoByWorkbenchTabId: nextCanUndoByTabId,
        canRedoByWorkbenchTabId: nextCanRedoByTabId,
        pendingCloseTabId: get().pendingCloseTabId === tabId ? undefined : get().pendingCloseTabId,
        tabCloseGuardOpen: get().pendingCloseTabId === tabId ? false : get().tabCloseGuardOpen,
        activeMicroflowId: shouldClearActiveMicroflow ? undefined : get().activeMicroflowId,
        lastActiveMicroflowTabId: get().lastActiveMicroflowTabId === tabId ? undefined : get().lastActiveMicroflowTabId
      });
      return;
    }

    const nextActiveTab =
      nextTabs[Math.min(closingIndex, nextTabs.length - 1)] ??
      nextTabs[closingIndex - 1];

    if (!nextActiveTab) {
      set({
        workbenchTabs: nextTabs,
        dirtyByWorkbenchTabId: nextDirtyByTabId,
        saveStateByMicroflowId: nextSaveStateByMicroflowId,
        savingByMicroflowId: microflowId ? omitTabRecord(get().savingByMicroflowId, microflowId) : get().savingByMicroflowId,
        saveErrorByMicroflowId: microflowId ? omitTabRecord(get().saveErrorByMicroflowId, microflowId) : get().saveErrorByMicroflowId,
        saveConflictByMicroflowId: microflowId ? omitTabRecord(get().saveConflictByMicroflowId, microflowId) : get().saveConflictByMicroflowId,
        canUndoByWorkbenchTabId: nextCanUndoByTabId,
        canRedoByWorkbenchTabId: nextCanRedoByTabId,
        activeWorkbenchTabId: undefined,
        activeTabId: undefined,
        activeMicroflowId: undefined,
        lastActiveMicroflowTabId: undefined,
        pendingCloseTabId: get().pendingCloseTabId === tabId ? undefined : get().pendingCloseTabId,
        tabCloseGuardOpen: get().pendingCloseTabId === tabId ? false : get().tabCloseGuardOpen,
        activeTab: "pageBuilder"
      });
      return;
    }

    set({
      workbenchTabs: nextTabs,
      dirtyByWorkbenchTabId: nextDirtyByTabId,
      saveStateByMicroflowId: nextSaveStateByMicroflowId,
      savingByMicroflowId: microflowId ? omitTabRecord(get().savingByMicroflowId, microflowId) : get().savingByMicroflowId,
      saveErrorByMicroflowId: microflowId ? omitTabRecord(get().saveErrorByMicroflowId, microflowId) : get().saveErrorByMicroflowId,
      saveConflictByMicroflowId: microflowId ? omitTabRecord(get().saveConflictByMicroflowId, microflowId) : get().saveConflictByMicroflowId,
      canUndoByWorkbenchTabId: nextCanUndoByTabId,
      canRedoByWorkbenchTabId: nextCanRedoByTabId,
      activeWorkbenchTabId: nextActiveTab.id,
      activeTabId: nextActiveTab.id,
      activeTab: getStudioTabForWorkbenchKind(nextActiveTab.kind),
      activeMicroflowId: nextActiveTab.kind === "microflow"
        ? nextActiveTab.microflowId ?? nextActiveTab.resourceId
        : undefined,
      activeModuleId: nextActiveTab.kind === "microflow"
        ? nextActiveTab.moduleId
        : get().activeModuleId,
      lastActiveMicroflowTabId: nextActiveTab.kind === "microflow"
        ? nextActiveTab.id
        : undefined,
      pendingCloseTabId: get().pendingCloseTabId === tabId ? undefined : get().pendingCloseTabId,
      tabCloseGuardOpen: get().pendingCloseTabId === tabId ? false : get().tabCloseGuardOpen
    });
  },

  cancelWorkbenchTabCloseGuard: () => set({
    pendingCloseTabId: undefined,
    tabCloseGuardOpen: false
  }),

  renameMicroflowWorkbenchTab: (microflowId, title, subtitle) => {
    const tabId = getMicroflowWorkbenchTabId(microflowId);
    set({
      workbenchTabs: get().workbenchTabs.map(tab =>
        tab.id === tabId
          ? { ...tab, title, subtitle: subtitle ?? tab.subtitle, qualifiedName: subtitle ?? tab.qualifiedName, updatedAt: new Date().toISOString() }
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
    get().closeWorkbenchTab(tabId, { force: true });
    if (get().activeMicroflowId === microflowId) {
      set({ activeMicroflowId: undefined });
    }
  },

  markWorkbenchTabDirty: (tabId, dirty) => set({
    dirtyByWorkbenchTabId: getDirtyTabRecord(get().dirtyByWorkbenchTabId, tabId, dirty),
    workbenchTabs: get().workbenchTabs.map(tab =>
      tab.id === tabId
        ? { ...tab, dirty }
        : tab
    )
  }),

  markMicroflowDirty: (microflowId, dirty) => {
    const tabId = getMicroflowWorkbenchTabId(microflowId);
    const currentState = get().saveStateByMicroflowId[microflowId];
    set({
      dirtyByWorkbenchTabId: getDirtyTabRecord(get().dirtyByWorkbenchTabId, tabId, dirty),
      saveStateByMicroflowId: ensureMicroflowSaveState(get().saveStateByMicroflowId, microflowId, {
        tabId,
        dirty,
        status: dirty ? (currentState?.saving ? "queued" : "dirty") : "saved",
        queued: dirty && Boolean(currentState?.saving) ? true : currentState?.queued ?? false,
        lastError: dirty ? undefined : currentState?.lastError,
        conflict: dirty && currentState?.status !== "conflict" ? undefined : currentState?.conflict
      }),
      workbenchTabs: get().workbenchTabs.map(tab =>
        tab.id === tabId
          ? { ...tab, dirty }
          : tab
      )
    });
  },

  updateMicroflowSaveState: (microflowId, patch) => {
    const tabId = patch.tabId ?? getMicroflowWorkbenchTabId(microflowId);
    const nextSaveStateByMicroflowId = ensureMicroflowSaveState(get().saveStateByMicroflowId, microflowId, {
      ...patch,
      tabId
    });
    const nextState = nextSaveStateByMicroflowId[microflowId];
    set({
      saveStateByMicroflowId: nextSaveStateByMicroflowId,
      savingByMicroflowId: nextState.saving
        ? { ...get().savingByMicroflowId, [microflowId]: true }
        : omitTabRecord(get().savingByMicroflowId, microflowId),
      saveErrorByMicroflowId: nextState.lastError
        ? { ...get().saveErrorByMicroflowId, [microflowId]: nextState.lastError }
        : omitTabRecord(get().saveErrorByMicroflowId, microflowId),
      saveConflictByMicroflowId: nextState.conflict
        ? { ...get().saveConflictByMicroflowId, [microflowId]: nextState.conflict }
        : omitTabRecord(get().saveConflictByMicroflowId, microflowId),
      dirtyByWorkbenchTabId: getDirtyTabRecord(get().dirtyByWorkbenchTabId, tabId, nextState.dirty),
      workbenchTabs: get().workbenchTabs.map(tab =>
        tab.id === tabId
          ? { ...tab, dirty: nextState.dirty, updatedAt: patch.lastSavedAt ?? tab.updatedAt }
          : tab
      )
    });
  },

  clearMicroflowSaveState: microflowId => {
    const tabId = getMicroflowWorkbenchTabId(microflowId);
    set({
      saveStateByMicroflowId: omitTabRecord(get().saveStateByMicroflowId, microflowId),
      savingByMicroflowId: omitTabRecord(get().savingByMicroflowId, microflowId),
      saveErrorByMicroflowId: omitTabRecord(get().saveErrorByMicroflowId, microflowId),
      saveConflictByMicroflowId: omitTabRecord(get().saveConflictByMicroflowId, microflowId),
      dirtyByWorkbenchTabId: omitTabRecord(get().dirtyByWorkbenchTabId, tabId),
      workbenchTabs: get().workbenchTabs.map(tab =>
        tab.id === tabId
          ? { ...tab, dirty: false }
          : tab
      )
    });
  },

  updateMicroflowWorkbenchTabFromResource: resource => {
    const tabId = getMicroflowWorkbenchTabId(resource.id);
    set({
      workbenchTabs: get().workbenchTabs.map(tab =>
        tab.id === tabId
          ? {
              ...tab,
              title: resource.displayName || resource.name,
              subtitle: resource.qualifiedName,
              moduleId: resource.moduleId,
              resourceId: resource.id,
              microflowId: resource.id,
              qualifiedName: resource.qualifiedName,
              status: resource.status,
              publishStatus: resource.publishStatus,
              updatedAt: new Date().toISOString()
            }
          : tab
      )
    });
  },

  setModuleMicroflows: (moduleId, microflows) => {
    const { microflowResourcesById, microflowIdsByModuleId } = get();
    const previousIds = new Set(microflowIdsByModuleId[moduleId] ?? []);
    const nextById = { ...microflowResourcesById };
    const openMicroflowIds = new Set(
      get().workbenchTabs
        .filter(tab => tab.kind === "microflow")
        .map(tab => tab.microflowId ?? tab.resourceId)
        .filter((id): id is string => Boolean(id))
    );
    for (const id of previousIds) {
      if (!openMicroflowIds.has(id)) {
        delete nextById[id];
      }
    }
    for (const resource of microflows) {
      nextById[resource.id] = resource;
    }
    set({
      microflowResourcesById: nextById,
      microflowIdsByModuleId: {
        ...microflowIdsByModuleId,
        [moduleId]: microflows.map(resource => resource.id)
      },
      workbenchTabs: get().workbenchTabs.map(tab => {
        const microflowId = tab.microflowId ?? tab.resourceId;
        const resource = microflowId ? nextById[microflowId] : undefined;
        return tab.kind === "microflow" && resource
          ? {
              ...tab,
              title: resource.displayName || resource.name,
              subtitle: resource.qualifiedName,
              moduleId: resource.moduleId,
              resourceId: resource.id,
              microflowId: resource.id,
              qualifiedName: resource.qualifiedName,
              status: resource.status,
              publishStatus: resource.publishStatus,
              updatedAt: new Date().toISOString()
            }
          : tab;
      })
    });
  },

  setFoldersForModule: (moduleId, folders) => set({
    foldersByModuleId: {
      ...get().foldersByModuleId,
      [moduleId]: folders
    }
  }),

  upsertFolder: folder => {
    const current = get().foldersByModuleId[folder.moduleId] ?? [];
    const nextFolders = current.some(item => item.id === folder.id)
      ? current.map(item => item.id === folder.id ? folder : item)
      : [...current, folder];
    set({
      foldersByModuleId: {
        ...get().foldersByModuleId,
        [folder.moduleId]: nextFolders
      }
    });
  },

  removeFolder: (folderId, moduleId) => {
    const foldersByModuleId = get().foldersByModuleId;
    if (moduleId) {
      set({
        foldersByModuleId: {
          ...foldersByModuleId,
          [moduleId]: (foldersByModuleId[moduleId] ?? []).filter(folder => folder.id !== folderId)
        }
      });
      return;
    }
    set({
      foldersByModuleId: Object.fromEntries(
        Object.entries(foldersByModuleId).map(([key, folders]) => [
          key,
          folders.filter(folder => folder.id !== folderId)
        ])
      )
    });
  },

  setFolderLoading: (moduleId, loading) => set({
    folderLoadingByModuleId: {
      ...get().folderLoadingByModuleId,
      [moduleId]: loading
    }
  }),

  setFolderError: (moduleId, error) => set({
    folderErrorByModuleId: {
      ...get().folderErrorByModuleId,
      [moduleId]: error
    }
  }),

  setModuleMicroflowsLoadState: (moduleId, loadState) => set({
    microflowsLoadStateByModuleId: {
      ...get().microflowsLoadStateByModuleId,
      [moduleId]: loadState
    }
  }),

  setExpandedExplorerNodeIds: expandedExplorerNodeIds => set({ expandedExplorerNodeIds }),

  setMicroflowTreeSearchKeyword: microflowTreeSearchKeyword => set({ microflowTreeSearchKeyword }),

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
              subtitle: resource.qualifiedName,
              moduleId: resource.moduleId,
              resourceId: resource.id,
              microflowId: resource.id,
              qualifiedName: resource.qualifiedName,
              status: resource.status,
              publishStatus: resource.publishStatus,
              updatedAt: new Date().toISOString()
            }
          : tab
      )
    });
  },

  updateStudioMicroflowFromResource: resource => {
    get().upsertStudioMicroflow(mapMicroflowResourceToStudioDefinitionView(resource));
  },

  removeStudioMicroflow: id => {
    const { microflowResourcesById, microflowIdsByModuleId, selectedExplorerNodeId, validationByMicroflowId, validationSummaryByMicroflowId } = get();
    const resource = microflowResourcesById[id];
    get().removeMicroflowWorkbenchTab(id);
    get().clearMicroflowSaveState(id);
    const nextById = { ...microflowResourcesById };
    delete nextById[id];
    const nextValidationById = { ...validationByMicroflowId };
    delete nextValidationById[id];
    const nextValidationSummaryById = { ...validationSummaryByMicroflowId };
    delete nextValidationSummaryById[id];
    if (!resource) {
      set({
        activeMicroflowId: get().activeMicroflowId === id ? undefined : get().activeMicroflowId,
        microflowResourcesById: nextById,
        validationByMicroflowId: nextValidationById,
        validationSummaryByMicroflowId: nextValidationSummaryById,
        selectedExplorerNodeId: selectedExplorerNodeId === `microflow:${id}` ? "microflows" : selectedExplorerNodeId
      });
      return;
    }
    const filteredIds = (microflowIdsByModuleId[resource.moduleId] ?? []).filter(mid => mid !== id);
    set({
      activeMicroflowId: get().activeMicroflowId === id ? undefined : get().activeMicroflowId,
      microflowResourcesById: nextById,
      microflowIdsByModuleId: { ...microflowIdsByModuleId, [resource.moduleId]: filteredIds },
      validationByMicroflowId: nextValidationById,
      validationSummaryByMicroflowId: nextValidationSummaryById,
      selectedExplorerNodeId: selectedExplorerNodeId === `microflow:${id}` ? "microflows" : selectedExplorerNodeId
    });
  },

  setMicroflowValidationState: ({ microflowId, issues, status, lastValidatedAt }) => {
    const normalizedValidatedAt = lastValidatedAt instanceof Date ? lastValidatedAt.toISOString() : lastValidatedAt;
    const normalizedIssues = issues.map(issue => ({ ...issue, microflowId: issue.microflowId ?? microflowId }));
    set({
      validationByMicroflowId: {
        ...get().validationByMicroflowId,
        [microflowId]: {
          ...(get().validationByMicroflowId[microflowId] ?? { microflowId, issues: [], running: false }),
          microflowId,
          issues: normalizedIssues,
          running: status === "validating",
          lastValidatedAt: normalizedValidatedAt,
          lastError: status === "failed" ? get().validationByMicroflowId[microflowId]?.lastError : undefined,
        },
      },
      validationSummaryByMicroflowId: {
        ...get().validationSummaryByMicroflowId,
        [microflowId]: summarizeMicroflowValidationIssues(normalizedIssues, normalizedValidatedAt, status),
      },
    });
  },

  setValidationRunning: (microflowId, running, requestId) => set({
    validationByMicroflowId: {
      ...get().validationByMicroflowId,
      [microflowId]: {
        ...(get().validationByMicroflowId[microflowId] ?? { microflowId, issues: [] }),
        microflowId,
        running,
        requestId,
      },
    },
  }),

  setValidationError: (microflowId, error) => set({
    validationByMicroflowId: {
      ...get().validationByMicroflowId,
      [microflowId]: {
        ...(get().validationByMicroflowId[microflowId] ?? { microflowId, issues: [], running: false }),
        microflowId,
        running: false,
        lastError: error instanceof Error ? error.message : error ? String(error) : undefined,
      },
    },
  }),

  openProblemsPanel: microflowId => set({ problemsPanelOpen: true, activeProblemsMicroflowId: microflowId }),

  clearValidationForMicroflow: microflowId => {
    const nextValidationById = { ...get().validationByMicroflowId };
    const nextSummaryById = { ...get().validationSummaryByMicroflowId };
    delete nextValidationById[microflowId];
    delete nextSummaryById[microflowId];
    set({
      validationByMicroflowId: nextValidationById,
      validationSummaryByMicroflowId: nextSummaryById,
      activeProblemsMicroflowId: get().activeProblemsMicroflowId === microflowId ? undefined : get().activeProblemsMicroflowId,
    });
  },
}));
