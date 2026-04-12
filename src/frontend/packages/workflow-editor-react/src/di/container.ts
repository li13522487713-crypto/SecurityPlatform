import { Container } from "inversify";
import { buildWorkflowEditorContainerModule } from "./container-module";

export function createWorkflowEditorContainer() {
  const container = new Container({ defaultScope: "Singleton" });
  container.load(buildWorkflowEditorContainerModule());
  return container;
}
