import type { AgentConfigNavKey } from "./agent-workbench-types";

export interface AgentConfigSectionItem {
  key: AgentConfigNavKey;
  label: string;
  description: string;
  title: string;
  subtitle: string;
}

export const AGENT_CONFIG_SECTIONS: AgentConfigSectionItem[] = [
  {
    key: "basic",
    label: "基础",
    description: "名称、描述与头像",
    title: "基础信息",
    subtitle: "智能体名称、对外描述与头像。"
  },
  {
    key: "persona",
    label: "人设",
    description: "角色 Markdown（Persona）",
    title: "人设",
    subtitle: "角色 Persona，写入后将进入系统提示中的「角色」段落。"
  },
  {
    key: "model",
    label: "模型",
    description: "模型配置、记忆与行为段落",
    title: "模型与记忆",
    subtitle: "选择已启用的模型配置，并调整记忆与行为相关段落（目标、技能等）。"
  },
  {
    key: "plugins",
    label: "插件与数据库",
    description: "工具绑定与数据连接",
    title: "插件与数据库",
    subtitle: "绑定可调用的工具能力，以及只读/读写数据库连接。"
  },
  {
    key: "knowledge",
    label: "知识库",
    description: "检索策略与内容类型",
    title: "知识库",
    subtitle: "选择知识库并配置检索参数。"
  },
  {
    key: "workflow",
    label: "工作流",
    description: "默认工作流与编排说明",
    title: "工作流",
    subtitle: "绑定默认工作流，并补充工作流相关说明段落。"
  },
  {
    key: "opening",
    label: "开场与引导",
    description: "开场白与预置问题",
    title: "开场与引导",
    subtitle: "开场白与用户侧预置问题。"
  },
  {
    key: "variables",
    label: "变量",
    description: "Bot 变量别名与默认值",
    title: "变量",
    subtitle: "暴露给工具与编排的 Bot 变量。"
  }
];

export const AGENT_CONFIG_SECTION_MAP: Record<AgentConfigNavKey, AgentConfigSectionItem> =
  AGENT_CONFIG_SECTIONS.reduce(
    (accumulator, section) => {
      accumulator[section.key] = section;
      return accumulator;
    },
    {} as Record<AgentConfigNavKey, AgentConfigSectionItem>
  );
