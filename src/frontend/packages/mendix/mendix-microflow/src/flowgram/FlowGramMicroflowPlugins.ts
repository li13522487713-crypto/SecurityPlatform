import { ContainerModule } from "inversify";
import {
  bindContributions,
  EntityManagerContribution,
  FlowDocumentContribution,
  FlowRendererContribution,
  WorkflowDocumentOptions,
} from "@flowgram-adapter/free-layout-editor";

import {
  FlowGramMicroflowDocumentOptions,
  FlowGramMicroflowRenderContribution,
} from "./FlowGramMicroflowEvents";
import { FlowGramMicroflowNodeRegistryContribution } from "./FlowGramMicroflowNodeRegistries";

export const FlowGramMicroflowContainerModule = new ContainerModule(
  (bind, _unbind, isBound, rebind) => {
    bindContributions(bind, FlowGramMicroflowNodeRegistryContribution, [
      EntityManagerContribution,
      FlowDocumentContribution,
    ]);
    bindContributions(bind, FlowGramMicroflowRenderContribution, [FlowRendererContribution]);
    const bindDocumentOptions = () =>
      bind(FlowGramMicroflowDocumentOptions)
        .to(FlowGramMicroflowDocumentOptions)
        .inSingletonScope();
    if (isBound(FlowGramMicroflowDocumentOptions)) {
      rebind(FlowGramMicroflowDocumentOptions)
        .to(FlowGramMicroflowDocumentOptions)
        .inSingletonScope();
    } else {
      bindDocumentOptions();
    }
    // WorkflowDocumentContainerModule (from WorkflowRenderProvider) already binds this token in production.
    if (isBound(WorkflowDocumentOptions)) {
      rebind(WorkflowDocumentOptions).toService(FlowGramMicroflowDocumentOptions);
    } else {
      bind(WorkflowDocumentOptions).toService(FlowGramMicroflowDocumentOptions);
    }
  },
);
