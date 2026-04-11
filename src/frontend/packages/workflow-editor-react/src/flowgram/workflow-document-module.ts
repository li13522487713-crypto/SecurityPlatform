import { ContainerModule } from "inversify";

// P3a: 预留 WorkflowDocument 相关注入扩展点。
export const workflowDocumentModule = new ContainerModule(() => {
  // 当前版本使用 free-layout-editor 默认 document 注入。
});
