export const WORKFLOW_EDITOR_DI = {
  workflowGlobalState: Symbol("workflowGlobalState"),
  workflowOperationService: Symbol("workflowOperationService"),
  workflowEditService: Symbol("workflowEditService"),
  workflowDragService: Symbol("workflowDragService"),
  workflowLineService: Symbol("workflowLineService"),
  workflowSaveService: Symbol("workflowSaveService"),
  workflowRunService: Symbol("workflowRunService")
} as const;
