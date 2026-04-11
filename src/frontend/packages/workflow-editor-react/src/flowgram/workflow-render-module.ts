import { ContainerModule } from "inversify";

export interface WorkflowRenderHooks {
  overlayLayers: string[];
  lineDecorators: string[];
}

export const WORKFLOW_RENDER_HOOKS = Symbol("WORKFLOW_RENDER_HOOKS");

const DEFAULT_WORKFLOW_RENDER_HOOKS: WorkflowRenderHooks = {
  overlayLayers: [],
  lineDecorators: []
};

// 渲染模块预埋：统一扩展层与连线装饰注册位，避免后续继续改空 stub。
export const workflowRenderModule = new ContainerModule(async ({ bind, isBound, rebind }) => {
  const alreadyBound = await isBound(WORKFLOW_RENDER_HOOKS);
  if (alreadyBound) {
    (await rebind(WORKFLOW_RENDER_HOOKS)).toConstantValue(DEFAULT_WORKFLOW_RENDER_HOOKS);
    return;
  }
  (await bind(WORKFLOW_RENDER_HOOKS)).toConstantValue(DEFAULT_WORKFLOW_RENDER_HOOKS);
});
