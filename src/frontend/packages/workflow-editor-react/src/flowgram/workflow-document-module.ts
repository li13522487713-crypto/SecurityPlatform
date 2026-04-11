import { ContainerModule } from "inversify";

export interface WorkflowDocumentHooks {
  beforeInit: Array<() => void>;
  beforeSubmit: Array<() => void>;
}

export const WORKFLOW_DOCUMENT_HOOKS = Symbol("WORKFLOW_DOCUMENT_HOOKS");

const DEFAULT_WORKFLOW_DOCUMENT_HOOKS: WorkflowDocumentHooks = {
  beforeInit: [],
  beforeSubmit: []
};

// 统一 document 扩展入口，便于后续接入流程标准化/提交拦截逻辑。
export const workflowDocumentModule = new ContainerModule(({ bind, isBound, rebind }) => {
  if (isBound(WORKFLOW_DOCUMENT_HOOKS)) {
    rebind(WORKFLOW_DOCUMENT_HOOKS).toConstantValue(DEFAULT_WORKFLOW_DOCUMENT_HOOKS);
    return;
  }
  bind(WORKFLOW_DOCUMENT_HOOKS).toConstantValue(DEFAULT_WORKFLOW_DOCUMENT_HOOKS);
});
