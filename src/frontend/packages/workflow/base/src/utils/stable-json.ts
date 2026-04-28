/*
 * Copyright 2025 coze-dev Authors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

type Jsonish =
  | null
  | boolean
  | number
  | string
  | Jsonish[]
  | { [key: string]: Jsonish | undefined };

function normalizeStable(value: unknown, seen: WeakSet<object>): Jsonish | undefined {
  if (value === undefined || typeof value === 'function' || typeof value === 'symbol') {
    return undefined;
  }

  if (value === null || typeof value === 'string' || typeof value === 'boolean') {
    return value;
  }

  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : null;
  }

  if (typeof value === 'bigint') {
    return value.toString();
  }

  if (value instanceof Date) {
    return value.toISOString();
  }

  if (value instanceof Map) {
    return normalizeStable(Object.fromEntries(value), seen);
  }

  if (value instanceof Set) {
    return normalizeStable([...value], seen);
  }

  if (Array.isArray(value)) {
    return value.map(item => normalizeStable(item, seen) ?? null);
  }

  if (typeof value === 'object') {
    if (seen.has(value)) {
      throw new TypeError('Cannot stable stringify circular workflow schema.');
    }

    seen.add(value);

    const sorted: Record<string, Jsonish> = {};
    for (const key of Object.keys(value).sort()) {
      const normalized = normalizeStable(
        (value as Record<string, unknown>)[key],
        seen,
      );
      if (normalized !== undefined) {
        sorted[key] = normalized;
      }
    }

    seen.delete(value);
    return sorted;
  }

  return undefined;
}

export function stableStringifyWorkflowSchema(value: unknown): string {
  return JSON.stringify(normalizeStable(value, new WeakSet<object>()));
}
