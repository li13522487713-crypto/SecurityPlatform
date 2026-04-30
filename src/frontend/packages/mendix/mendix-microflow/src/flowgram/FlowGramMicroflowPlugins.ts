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
  FlowGramMicroflowSchemaContextService,
  FlowGramMicroflowSchemaContextServiceToken,
} from "./FlowGramMicroflowEvents";
import { FlowGramMicroflowNodeRegistryContribution } from "./FlowGramMicroflowNodeRegistries";

export const FlowGramMicroflowContainerModule = new ContainerModule(
  (bind, _unbind, isBound, rebind) => {
    if (!isBound(FlowGramMicroflowSchemaContextServiceToken)) {
      bind<FlowGramMicroflowSchemaContextService>(FlowGramMicroflowSchemaContextServiceToken)
        .to(FlowGramMicroflowSchemaContextService)
        .inSingletonScope();
    }
    bindContributions(bind, FlowGramMicroflowNodeRegistryContribution, [
      EntityManagerContribution,
      FlowDocumentContribution,
    ]);
    bindContributions(bind, FlowGramMicroflowRenderContribution, [FlowRendererContribution]);
    const bindDocumentOptions = () =>
      bind(FlowGramMicroflowDocumentOptions)
        .toDynamicValue(ctx => new FlowGramMicroflowDocumentOptions(
          ctx.container.get<FlowGramMicroflowSchemaContextService>(FlowGramMicroflowSchemaContextServiceToken),
        ))
        .inSingletonScope();
    if (isBound(FlowGramMicroflowDocumentOptions)) {
      rebind(FlowGramMicroflowDocumentOptions)
        .toDynamicValue(ctx => new FlowGramMicroflowDocumentOptions(
          ctx.container.get<FlowGramMicroflowSchemaContextService>(FlowGramMicroflowSchemaContextServiceToken),
        ))
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
