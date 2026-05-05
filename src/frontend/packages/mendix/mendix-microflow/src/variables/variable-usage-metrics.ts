import type { MicroflowAuthoringSchema, MicroflowVariableIndex } from "../schema";
import { getVariablesForExpressionFromIndex } from "./variable-scope-query";

export interface MicroflowVariableUsageMetric {
  name: string;
  referenceCount: number;
  visibleAtObject: boolean;
}

function collectExpressionRawValues(value: unknown, sink: string[]): void {
  if (!value) {
    return;
  }
  if (typeof value === "object") {
    const record = value as Record<string, unknown>;
    const raw = record.raw;
    if (typeof raw === "string" && raw.trim()) {
      sink.push(raw);
    }
    Object.values(record).forEach(item => collectExpressionRawValues(item, sink));
  }
}

function parseVariableNamesFromExpression(raw: string): string[] {
  const names = new Set<string>();
  const regex = /\$\.?[A-Za-z_][A-Za-z0-9_]*(?:\/[A-Za-z_][A-Za-z0-9_]*)?/g;
  for (const match of raw.matchAll(regex)) {
    const token = String(match[0] ?? "");
    const normalized = token.startsWith("$.") ? token.slice(2) : token.slice(1);
    const variableName = normalized.split("/")[0] ?? normalized;
    if (variableName) {
      names.add(variableName);
    }
  }
  return [...names];
}

export function buildVariableUsageMetrics(input: {
  schema: MicroflowAuthoringSchema;
  variableIndex: MicroflowVariableIndex;
  objectId?: string;
}): Record<string, MicroflowVariableUsageMetric> {
  const expressions: string[] = [];
  collectExpressionRawValues(input.schema.objectCollection, expressions);

  const referenceCounter: Record<string, number> = {};
  expressions.forEach(raw => {
    parseVariableNamesFromExpression(raw).forEach(name => {
      referenceCounter[name] = (referenceCounter[name] ?? 0) + 1;
    });
  });

  const visibleSet = new Set(
    input.objectId
      ? getVariablesForExpressionFromIndex(input.schema, input.variableIndex, { objectId: input.objectId }).map(item => item.name)
      : [],
  );

  const metrics: Record<string, MicroflowVariableUsageMetric> = {};
  Object.keys(input.variableIndex.byName ?? {}).forEach(name => {
    metrics[name] = {
      name,
      referenceCount: referenceCounter[name] ?? 0,
      visibleAtObject: visibleSet.has(name),
    };
  });
  return metrics;
}

