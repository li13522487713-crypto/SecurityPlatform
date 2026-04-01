import runtimeMessagesZhCN from "./runtime-messages.zh-CN";
import runtimeMessagesEnUS from "./runtime-messages.en-US";

export type MessageTree = Record<string, unknown>;

export const runtimeMessages = {
  "zh-CN": runtimeMessagesZhCN,
  "en-US": runtimeMessagesEnUS
} as const;

