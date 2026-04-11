import { ContainerModule } from "inversify";

// P3a: 预留渲染层级/line manager 自定义注入扩展点。
export const workflowRenderModule = new ContainerModule(() => {
  // 当前阶段先走 Flowgram 默认渲染管线。
});
