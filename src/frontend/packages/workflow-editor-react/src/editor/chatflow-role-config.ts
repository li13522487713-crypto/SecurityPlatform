export const CHATFLOW_ROLE_CONFIG_KEY = "chatflowRoleConfig";

export interface ChatflowRoleConfig {
  roleName: string;
  roleDescription: string;
  avatarLabel: string;
  openingText: string;
  openingQuestions: string[];
  showAllOpeningQuestions: boolean;
}

function normalizeString(value: unknown): string {
  return typeof value === "string" ? value.trim() : "";
}

function normalizeQuestions(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }
  return value
    .map((item) => (typeof item === "string" ? item.trim() : ""))
    .slice(0, 8);
}

export function getDefaultChatflowRoleConfig(): ChatflowRoleConfig {
  return {
    roleName: "",
    roleDescription: "",
    avatarLabel: "",
    openingText: "",
    openingQuestions: [],
    showAllOpeningQuestions: false
  };
}

export function readChatflowRoleConfig(globals: Record<string, unknown> | undefined): ChatflowRoleConfig {
  const current = globals?.[CHATFLOW_ROLE_CONFIG_KEY];
  if (!current || typeof current !== "object" || Array.isArray(current)) {
    return getDefaultChatflowRoleConfig();
  }

  const record = current as Record<string, unknown>;
  const defaults = getDefaultChatflowRoleConfig();
  return {
    roleName: normalizeString(record.roleName),
    roleDescription: normalizeString(record.roleDescription),
    avatarLabel: normalizeString(record.avatarLabel),
    openingText: normalizeString(record.openingText),
    openingQuestions: normalizeQuestions(record.openingQuestions),
    showAllOpeningQuestions:
      typeof record.showAllOpeningQuestions === "boolean"
        ? record.showAllOpeningQuestions
        : defaults.showAllOpeningQuestions
  };
}

function isSameRoleConfig(left: ChatflowRoleConfig, right: ChatflowRoleConfig): boolean {
  if (
    left.roleName !== right.roleName ||
    left.roleDescription !== right.roleDescription ||
    left.avatarLabel !== right.avatarLabel ||
    left.openingText !== right.openingText ||
    left.showAllOpeningQuestions !== right.showAllOpeningQuestions ||
    left.openingQuestions.length !== right.openingQuestions.length
  ) {
    return false;
  }

  for (let index = 0; index < left.openingQuestions.length; index += 1) {
    if (left.openingQuestions[index] !== right.openingQuestions[index]) {
      return false;
    }
  }

  return true;
}

export function ensureChatflowGlobals(globals: Record<string, unknown> | undefined): Record<string, unknown> {
  const baseGlobals = globals ?? {};
  const normalized = readChatflowRoleConfig(baseGlobals);
  const current = baseGlobals[CHATFLOW_ROLE_CONFIG_KEY];

  if (current && typeof current === "object" && !Array.isArray(current) && isSameRoleConfig(current as ChatflowRoleConfig, normalized)) {
    return baseGlobals;
  }

  return {
    ...baseGlobals,
    [CHATFLOW_ROLE_CONFIG_KEY]: normalized
  };
}

export function patchChatflowRoleConfig(
  globals: Record<string, unknown> | undefined,
  patch: Partial<ChatflowRoleConfig>
): Record<string, unknown> {
  const baseGlobals = ensureChatflowGlobals(globals);
  const current = readChatflowRoleConfig(baseGlobals);

  return {
    ...baseGlobals,
    [CHATFLOW_ROLE_CONFIG_KEY]: {
      ...current,
      ...patch,
      openingQuestions: patch.openingQuestions ? normalizeQuestions(patch.openingQuestions) : current.openingQuestions
    }
  };
}

export function validateChatflowRoleConfig(config: ChatflowRoleConfig): string[] {
  const issues: string[] = [];

  if (!config.roleName.trim()) {
    issues.push("角色名称不能为空。");
  }

  if (config.roleName.length > 50) {
    issues.push("角色名称不能超过 50 个字符。");
  }

  if (config.roleDescription.length > 600) {
    issues.push("角色描述不能超过 600 个字符。");
  }

  if (config.avatarLabel.length > 2) {
    issues.push("角色头像简称不能超过 2 个字符。");
  }

  if (config.openingText.length > 2000) {
    issues.push("开场白不能超过 2000 个字符。");
  }

  if (config.openingQuestions.length > 8) {
    issues.push("预置问题最多 8 条。");
  }

  const seenQuestions = new Set<string>();
  for (const question of config.openingQuestions) {
    if (!question.trim()) {
      issues.push("预置问题不能为空。");
      continue;
    }
    if (question.length > 100) {
      issues.push("单条预置问题不能超过 100 个字符。");
    }
    if (seenQuestions.has(question)) {
      issues.push("预置问题不能重复。");
    }
    seenQuestions.add(question);
  }

  return Array.from(new Set(issues));
}
