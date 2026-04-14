import type {
  AgentDatabaseBindingInput,
  AgentKnowledgeBindingInput,
  AgentPluginBindingInput,
  AgentVariableBindingInput,
  WorkbenchTrace
} from "../types";

export function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function parseJsonSafely(value?: string): unknown {
  if (!value) {
    return null;
  }

  try {
    return JSON.parse(value);
  } catch {
    const decoded = decodeHtmlEntities(value);
    if (!decoded || decoded === value) {
      return null;
    }

    try {
      return JSON.parse(decoded);
    } catch {
      return null;
    }
  }
}

function decodeHtmlEntities(value: string): string {
  return value
    .replaceAll("&quot;", "\"")
    .replaceAll("&#34;", "\"")
    .replaceAll("&#x27;", "'")
    .replaceAll("&lt;", "<")
    .replaceAll("&gt;", ">")
    .replaceAll("&amp;", "&");
}

interface SecurityIncidentTaskCard {
  title: string;
  summary: string;
  severity: string;
  ownerSuggestion: string;
  nextActions: string[];
}

function extractSecurityIncidentTaskCard(payload: Record<string, unknown>): SecurityIncidentTaskCard | null {
  const nextActions = Array.isArray(payload.nextActions)
    ? payload.nextActions.filter((item): item is string => typeof item === "string" && item.trim().length > 0)
    : [];
  const title = typeof payload.title === "string" ? payload.title.trim() : "";
  const summary = typeof payload.summary === "string" ? payload.summary.trim() : "";
  const severity = typeof payload.severity === "string" ? payload.severity.trim() : "";
  const ownerSuggestion = typeof payload.ownerSuggestion === "string" ? payload.ownerSuggestion.trim() : "";

  if (!title && !summary && !severity && !ownerSuggestion && nextActions.length === 0) {
    return null;
  }

  return {
    title: title || "安全事件处置任务",
    summary: summary || "工作流已完成结构化输出。",
    severity: severity || "待确认",
    ownerSuggestion: ownerSuggestion || "安全管理员",
    nextActions
  };
}

export function toSecurityIncidentTaskCard(outputsJson?: string): SecurityIncidentTaskCard | null {
  const initial = parseJsonSafely(outputsJson);
  if (typeof initial === "string") {
    const nested = parseJsonSafely(initial);
    return isRecord(nested) ? extractSecurityIncidentTaskCard(nested) : null;
  }

  if (!isRecord(initial)) {
    return null;
  }

  if (typeof initial.result === "string") {
    const nested = parseJsonSafely(initial.result);
    if (isRecord(nested)) {
      return extractSecurityIncidentTaskCard(nested);
    }
  }

  return extractSecurityIncidentTaskCard(initial);
}

export function formatWorkflowResultMessage(card: SecurityIncidentTaskCard): string {
  const lines = [
    `任务标题：${card.title}`,
    `事件摘要：${card.summary}`,
    `严重级别：${card.severity}`,
    `建议负责人：${card.ownerSuggestion}`
  ];

  if (card.nextActions.length > 0) {
    lines.push("下一步动作：");
    card.nextActions.forEach((item, index) => {
      lines.push(`${index + 1}. ${item}`);
    });
  }

  return lines.join("\n");
}

export function parseTraceSummary(metadata?: string): WorkbenchTrace | null {
  const payload = parseJsonSafely(metadata);
  if (!isRecord(payload) || !isRecord(payload.trace)) {
    return null;
  }

  const trace = payload.trace;
  const steps = Array.isArray(trace.steps)
    ? trace.steps
        .filter(isRecord)
        .map(step => ({
          nodeKey: typeof step.nodeKey === "string" ? step.nodeKey : "unknown",
          status: typeof step.status === "string" ? step.status : undefined,
          nodeType: typeof step.nodeType === "string" ? step.nodeType : undefined,
          durationMs: typeof step.durationMs === "number" ? step.durationMs : undefined,
          errorMessage: typeof step.errorMessage === "string" ? step.errorMessage : undefined
        }))
    : [];

  return {
    executionId: typeof trace.executionId === "string" ? trace.executionId : "",
    status: typeof trace.status === "string" ? trace.status : undefined,
    startedAt: typeof trace.startedAt === "string" ? trace.startedAt : undefined,
    completedAt: typeof trace.completedAt === "string" ? trace.completedAt : undefined,
    durationMs: typeof trace.durationMs === "number" ? trace.durationMs : undefined,
    steps
  };
}

export interface WorkbenchResourceUsage {
  pluginTools: string[];
  knowledgeBases: string[];
  databases: string[];
  variables: string[];
}

export function parseResourceUsageSummary(metadata?: string): WorkbenchResourceUsage | null {
  const payload = parseJsonSafely(metadata);
  if (!isRecord(payload)) {
    return null;
  }

  const source = isRecord(payload.resourceUsage)
    ? payload.resourceUsage
    : isRecord(payload.resources)
      ? payload.resources
      : payload;

  const toStringArray = (value: unknown): string[] =>
    Array.isArray(value)
      ? value.filter((item): item is string => typeof item === "string" && item.trim().length > 0).map(item => item.trim())
      : [];

  const result = {
    pluginTools: toStringArray(source.usedPlugins ?? source.pluginTools),
    knowledgeBases: toStringArray(source.usedKnowledgeBases ?? source.knowledgeBases),
    databases: toStringArray(source.usedDatabases ?? source.databases),
    variables: toStringArray(source.usedVariables ?? source.variables)
  };

  if (result.pluginTools.length === 0 && result.knowledgeBases.length === 0 && result.databases.length === 0 && result.variables.length === 0) {
    return null;
  }

  return result;
}

export interface AgentPromptSections {
  persona: string;
  goals: string;
  skills: string;
  workflow: string;
  outputFormat: string;
  constraints: string;
  opening: string;
}

export const EMPTY_AGENT_PROMPT_SECTIONS: AgentPromptSections = {
  persona: "",
  goals: "",
  skills: "",
  workflow: "",
  outputFormat: "",
  constraints: "",
  opening: ""
};

export const AGENT_PROMPT_SECTION_MAP: Array<{ title: string; key: keyof AgentPromptSections }> = [
  { title: "角色", key: "persona" },
  { title: "目标", key: "goals" },
  { title: "技能", key: "skills" },
  { title: "工作流", key: "workflow" },
  { title: "输出格式", key: "outputFormat" },
  { title: "限制", key: "constraints" },
  { title: "开场白", key: "opening" }
];

export function parseAgentPromptSections(systemPrompt?: string): AgentPromptSections {
  if (!systemPrompt?.trim()) {
    return { ...EMPTY_AGENT_PROMPT_SECTIONS };
  }

  const sections = { ...EMPTY_AGENT_PROMPT_SECTIONS };
  let currentKey: keyof AgentPromptSections | null = null;

  for (const line of systemPrompt.split(/\r?\n/)) {
    const matched = AGENT_PROMPT_SECTION_MAP.find(section => line.trim() === `## ${section.title}`);
    if (matched) {
      currentKey = matched.key;
      continue;
    }

    if (!currentKey) {
      continue;
    }

    sections[currentKey] = sections[currentKey]
      ? `${sections[currentKey]}\n${line}`
      : line;
  }

  const hasParsedSection = AGENT_PROMPT_SECTION_MAP.some(section => sections[section.key].trim().length > 0);
  if (!hasParsedSection) {
    return {
      ...EMPTY_AGENT_PROMPT_SECTIONS,
      persona: systemPrompt.trim()
    };
  }

  return sections;
}

export function composeAgentPromptSections(sections: AgentPromptSections): string {
  return AGENT_PROMPT_SECTION_MAP
    .map(section => {
      const value = sections[section.key].trim();
      if (!value) {
        return "";
      }

      return `## ${section.title}\n${value}`;
    })
    .filter(Boolean)
    .join("\n\n");
}

export function parsePluginParameterNames(requestSchemaJson?: string): Array<{ name: string; required: boolean }> {
  if (!requestSchemaJson?.trim()) {
    return [];
  }

  try {
    const schema = JSON.parse(requestSchemaJson) as {
      properties?: Record<string, unknown>;
      required?: string[];
    };
    const required = new Set(schema.required ?? []);
    return Object.keys(schema.properties ?? {}).map(name => ({
      name,
      required: required.has(name)
    }));
  } catch {
    return [];
  }
}

export function createDefaultKnowledgeBinding(knowledgeBaseId: number): AgentKnowledgeBindingInput {
  return {
    knowledgeBaseId,
    isEnabled: true,
    invokeMode: "auto",
    topK: 5,
    scoreThreshold: 0.5,
    enabledContentTypes: ["text", "table", "image"],
    rewriteQueryTemplate: undefined
  };
}

export function createDefaultPluginBinding(pluginId: number): AgentPluginBindingInput {
  return {
    pluginId,
    sortOrder: 0,
    isEnabled: true,
    toolConfigJson: "{}",
    toolBindings: []
  };
}

export function createDefaultDatabaseBinding(databaseId: number, isDefault = false): AgentDatabaseBindingInput {
  return {
    databaseId,
    alias: undefined,
    accessMode: "readonly",
    tableAllowlist: [],
    isDefault
  };
}

export function createDefaultVariableBinding(variableId: number): AgentVariableBindingInput {
  return {
    variableId,
    alias: undefined,
    isRequired: false,
    defaultValueOverride: undefined
  };
}

export function normalizeAllowlistText(value: string): string[] {
  return value
    .split(/[,\n]/)
    .map(item => item.trim())
    .filter(Boolean)
    .filter((item, index, array) => array.indexOf(item) === index);
}

export function stringifyAllowlist(value: string[]): string {
  return value.join(", ");
}

export function readPageSearchParam(name: string): string {
  if (typeof window === "undefined") {
    return "";
  }

  return new URLSearchParams(window.location.search).get(name) ?? "";
}

/** 会话/发布时间与 Bot IDE 调试面板日期展示（与 pages.tsx `formatDate` 行为一致）。 */
export function formatDate(value?: string) {
  if (!value) {
    return "-";
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

export function replacePageSearchParams(mutator: (params: URLSearchParams) => void) {
  if (typeof window === "undefined") {
    return;
  }

  const params = new URLSearchParams(window.location.search);
  mutator(params);
  const query = params.toString();
  const nextUrl = query ? `${window.location.pathname}?${query}` : window.location.pathname;
  window.history.replaceState(null, "", nextUrl);
}
