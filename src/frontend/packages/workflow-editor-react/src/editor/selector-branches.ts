export interface SelectorCondition {
  left: string;
  op: string;
  right: string;
  logic?: "and" | "or";
  branchId?: string;
}

function branchIdByIndex(index: number): string {
  return index === 0 ? "true" : `true_${index}`;
}

function createUniqueBranchId(used: Set<string>): string {
  let index = 0;
  while (used.has(branchIdByIndex(index))) {
    index += 1;
  }
  return branchIdByIndex(index);
}

export function normalizeSelectorConditions(value: unknown): SelectorCondition[] {
  const rows = Array.isArray(value) ? value : [];
  const used = new Set<string>();
  return rows.map((item, index) => {
    const row = item as Record<string, unknown>;
    const preferId = typeof row.branchId === "string" && row.branchId.trim() ? row.branchId.trim() : branchIdByIndex(index);
    const nextId = used.has(preferId) ? createUniqueBranchId(used) : preferId;
    used.add(nextId);
    return {
      left: typeof row.left === "string" ? row.left : "",
      op: typeof row.op === "string" ? row.op : "eq",
      right: typeof row.right === "string" ? row.right : "",
      logic: row.logic === "or" ? "or" : "and",
      branchId: nextId
    };
  });
}

export function addSelectorCondition(conditions: SelectorCondition[]): SelectorCondition[] {
  const used = new Set(conditions.map((item) => item.branchId).filter((item): item is string => Boolean(item)));
  return [
    ...conditions,
    {
      left: "",
      op: "eq",
      right: "",
      logic: "and",
      branchId: createUniqueBranchId(used)
    }
  ];
}

export function removeSelectorCondition(conditions: SelectorCondition[], index: number): SelectorCondition[] {
  return conditions.filter((_, rowIndex) => rowIndex !== index);
}

export function reorderSelectorCondition(conditions: SelectorCondition[], from: number, to: number): SelectorCondition[] {
  if (from === to || from < 0 || to < 0 || from >= conditions.length || to >= conditions.length) {
    return conditions;
  }
  const next = [...conditions];
  const [target] = next.splice(from, 1);
  next.splice(to, 0, target);
  return next;
}

export function deriveSelectorOutputPortKeys(conditions: unknown): string[] {
  const normalized = normalizeSelectorConditions(conditions);
  if (normalized.length === 0) {
    return ["true", "false"];
  }
  return [...normalized.map((item) => item.branchId ?? "true"), "false"];
}
