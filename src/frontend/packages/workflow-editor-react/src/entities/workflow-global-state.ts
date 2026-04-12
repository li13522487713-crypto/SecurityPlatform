import { injectable } from "inversify";
import type { WorkflowEditorState } from "../stores/workflow-editor-store";
import { useWorkflowEditorStore } from "../stores/workflow-editor-store";

@injectable()
export class WorkflowGlobalStateEntity {
  get snapshot(): WorkflowEditorState {
    return useWorkflowEditorStore.getState();
  }

  patchState(partial: Partial<WorkflowEditorState>): void {
    useWorkflowEditorStore.setState(partial);
  }

  select<T>(selector: (state: WorkflowEditorState) => T): T {
    return selector(useWorkflowEditorStore.getState());
  }
}
