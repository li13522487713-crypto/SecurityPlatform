import type { MicroflowApiError } from "../microflow/contracts/api/api-envelope";
import type { MicroflowValidationSummary } from "../store";
import type { StudioMicroflowDefinitionView } from "../microflow/studio/studio-microflow-types";
import type { ExplorerTreeNode, MicroflowLoadStatus } from "./app-explorer";

export const MicroflowsSectionKey = "microflows";

export function createMicroflowStateChildren(
  status: MicroflowLoadStatus,
  microflows: StudioMicroflowDefinitionView[],
  error?: MicroflowApiError,
  validationSummaryByMicroflowId: Record<string, MicroflowValidationSummary> = {}
): ExplorerTreeNode[] {
  if (status === "loading" || status === "idle") {
    return [{ key: "microflows:loading", label: "Loading microflows...", kind: "loading", readonly: true }];
  }

  if (status === "error") {
    return [{
      key: "microflows:error",
      label: "Load failed",
      kind: "error",
      readonly: true,
      errorMessage: error?.message,
      error,
      action: "retryMicroflows"
    }];
  }

  if (microflows.length === 0) {
    return [{ key: "microflows:empty", label: "No microflows", kind: "empty", readonly: true }];
  }

  return microflows.map(resource => ({
    key: `microflow:${resource.id}`,
    label: resource.displayName || resource.name,
    icon: "M",
    kind: "microflow",
    moduleId: resource.moduleId,
    microflowId: resource.id,
    resourceId: resource.id,
    name: resource.name,
    displayName: resource.displayName,
    qualifiedName: resource.qualifiedName,
    status: resource.status,
    publishStatus: resource.publishStatus,
    referenceCount: resource.referenceCount,
    problemSummary: validationSummaryByMicroflowId[resource.id],
    dynamic: true,
    title: resource.qualifiedName
  }));
}
