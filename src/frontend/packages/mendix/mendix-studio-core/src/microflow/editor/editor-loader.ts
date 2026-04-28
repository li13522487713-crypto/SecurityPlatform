import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";

export async function loadMicroflowEditorResource(adapter: MicroflowResourceAdapter, resourceId: string) {
  return adapter.getMicroflow(resourceId);
}
