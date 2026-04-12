import { ContainerModule } from "inversify";
import { WorkflowGlobalStateEntity } from "../entities/workflow-global-state";
import {
  WorkflowDragService,
  WorkflowEditService,
  WorkflowLineService,
  WorkflowOperationService,
  WorkflowRunService,
  WorkflowSaveService
} from "../services";
import { WORKFLOW_EDITOR_DI } from "./symbols";

export function buildWorkflowEditorContainerModule() {
  return new ContainerModule(({ bind }) => {
    bind(WorkflowGlobalStateEntity).toSelf().inSingletonScope();
    bind(WorkflowOperationService).toSelf().inSingletonScope();
    bind(WorkflowDragService).toSelf().inSingletonScope();
    bind(WorkflowEditService).toSelf().inSingletonScope();
    bind(WorkflowLineService).toSelf().inSingletonScope();
    bind(WorkflowSaveService).toSelf().inSingletonScope();
    bind(WorkflowRunService).toSelf().inSingletonScope();

    bind(WORKFLOW_EDITOR_DI.workflowGlobalState).toService(WorkflowGlobalStateEntity);
    bind(WORKFLOW_EDITOR_DI.workflowOperationService).toService(WorkflowOperationService);
    bind(WORKFLOW_EDITOR_DI.workflowDragService).toService(WorkflowDragService);
    bind(WORKFLOW_EDITOR_DI.workflowEditService).toService(WorkflowEditService);
    bind(WORKFLOW_EDITOR_DI.workflowLineService).toService(WorkflowLineService);
    bind(WORKFLOW_EDITOR_DI.workflowSaveService).toService(WorkflowSaveService);
    bind(WORKFLOW_EDITOR_DI.workflowRunService).toService(WorkflowRunService);
  });
}
