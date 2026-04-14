import type { StudioPageProps } from "../types";

/** 左侧配置导航：与中间 `AgentConfigPanel` 的 Tab 一一对应。 */
export type AgentConfigNavKey =
  | "basic"
  | "persona"
  | "model"
  | "plugins"
  | "knowledge"
  | "workflow"
  | "opening"
  | "variables";

export type AgentWorkbenchProps = StudioPageProps & {
  botId: string;
  onOpenPublish?: () => void;
};
