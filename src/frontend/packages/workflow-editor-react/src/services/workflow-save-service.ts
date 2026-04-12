import { injectable } from "inversify";
import { message } from "antd";
import { normalizeConnectionsByPorts, parseCanvasJson } from "../editor/workflow-editor-state";
import { NodeRegistry } from "../node-registry";
import type { WorkflowNodeRegistryV2 } from "../node-registry";
import { useWorkflowEditorStore } from "../stores/workflow-editor-store";
import { WorkflowOperationService } from "./workflow-operation-service";

const HIGH_DEBOUNCE_TIME = 1000;
const LOW_DEBOUNCE_TIME = 2000;

function debounce<T extends (...args: never[]) => void>(fn: T, delay: number): T {
  let timer: ReturnType<typeof setTimeout> | null = null;
  return ((...args: Parameters<T>) => {
    if (timer) {
      clearTimeout(timer);
    }
    timer = setTimeout(() => {
      fn(...args);
      timer = null;
    }, delay);
  }) as T;
}

@injectable()
export class WorkflowSaveService {
  private onSavedResolvers = new Set<() => void>();
  private readonly nodeRegistry = new NodeRegistry();

  constructor(private readonly operationService: WorkflowOperationService) {}

  private readonly highPrioritySaveImpl = debounce(async () => {
    await this.save(false);
  }, HIGH_DEBOUNCE_TIME);

  private readonly lowPrioritySaveImpl = debounce(async () => {
    await this.save(true);
  }, LOW_DEBOUNCE_TIME);

  async loadDocument(): Promise<void> {
    const state = useWorkflowEditorStore.getState();
    state.resetEditorState();
    const [nodeTypesResp, templatesResp, modelCatalogResp, detailResp] = await Promise.all([
      this.operationService.apiClient?.getNodeTypes?.(),
      this.operationService.apiClient?.getNodeTemplates?.(),
      this.operationService.apiClient?.getModelCatalog?.(),
      this.operationService.apiClient?.getDetail?.(this.operationService.workflowId, this.operationService.detailQuery)
    ]);

    const nodeTypesMeta = nodeTypesResp?.data ?? [];
    const nodeTemplates = templatesResp?.data ?? [];
    const modelCatalog = modelCatalogResp?.data ?? [];
    state.setNodeTypesMeta(nodeTypesMeta);
    state.setNodeTemplates(nodeTemplates);
    state.setModelCatalog(modelCatalog);

    if (detailResp?.data) {
      const parsed = parseCanvasJson(detailResp.data.canvasJson);
      const normalized = normalizeConnectionsByPorts(parsed.nodes, parsed.connections, nodeTypesMeta);
      state.setCanvasSnapshot({
        nodes: parsed.nodes,
        connections: normalized.connections,
        globals: parsed.globals,
        viewport: parsed.viewport,
        workflowName: detailResp.data.name || `Workflow_${this.operationService.workflowId}`,
        isDirty: false
      });
      state.setSelectedNodeKeys([]);
      if (normalized.migratedCount > 0) {
        message.info(`已迁移 ${normalized.migratedCount} 条历史连线到默认端口。`);
      }
      return;
    }

    state.setWorkflowName(`Workflow_${this.operationService.workflowId}`);
    state.setLastSavedAt(null);
  }

  async save(ignoreStatusTransfer: boolean): Promise<void> {
    const state = useWorkflowEditorStore.getState();
    state.setSaving(true);
    try {
      const preparedNodes = state.canvasNodes.map((node) => {
        const registry = this.nodeRegistry.resolve(node.type) as WorkflowNodeRegistryV2;
        if (!registry.beforeNodeSubmit) {
          return node;
        }
        const cloned = structuredClone(node) as unknown as Record<string, unknown>;
        registry.beforeNodeSubmit(cloned);
        return cloned as unknown as typeof node;
      });
      state.setCanvasNodes(preparedNodes);
      await this.operationService.save(ignoreStatusTransfer);
      state.setDirty(false);
      state.setLastSavedAt(Date.now());
      state.appendLog("save_draft");
    } finally {
      state.setSaving(false);
      this.onSavedResolvers.forEach((resolve) => resolve());
      this.onSavedResolvers.clear();
    }
  }

  listenContentChange(event: { type: "MOVE_NODE" | "META_CHANGE" | "NODE_ADD" | "NODE_DELETE" | "LINE_CHANGE" }): void {
    if (event.type === "MOVE_NODE" || event.type === "META_CHANGE") {
      this.lowPrioritySave();
      return;
    }
    this.highPrioritySave();
  }

  highPrioritySave(): void {
    this.highPrioritySaveImpl();
  }

  lowPrioritySave(): void {
    this.lowPrioritySaveImpl();
  }

  waitSaving(): Promise<void> {
    const state = useWorkflowEditorStore.getState();
    if (!state.saving) {
      return Promise.resolve();
    }
    return new Promise<void>((resolve) => {
      this.onSavedResolvers.add(resolve);
    });
  }

  async reloadDocument(): Promise<void> {
    await this.waitSaving();
    await this.loadDocument();
  }
}
