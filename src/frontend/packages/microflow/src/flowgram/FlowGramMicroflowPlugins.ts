import { ContainerModule } from "inversify";
import {
  bindContributions,
  FlowDocumentContribution,
  FlowRendererContribution,
  WorkflowDocumentOptions,
} from "@flowgram-adapter/free-layout-editor";

import {
  FlowGramMicroflowBridgeService,
  FlowGramMicroflowDocumentOptions,
  FlowGramMicroflowRenderContribution,
} from "./FlowGramMicroflowEvents";
import { FlowGramMicroflowNodeRegistryContribution } from "./FlowGramMicroflowNodeRegistries";

export const FlowGramMicroflowContainerModule = new ContainerModule(bind => {
  bind(FlowGramMicroflowBridgeService).toSelf().inSingletonScope();
  bindContributions(bind, FlowGramMicroflowNodeRegistryContribution, [FlowDocumentContribution]);
  bindContributions(bind, FlowGramMicroflowRenderContribution, [FlowRendererContribution]);
  bind(FlowGramMicroflowDocumentOptions).toSelf().inSingletonScope();
  bind(WorkflowDocumentOptions).toService(FlowGramMicroflowDocumentOptions);
});

