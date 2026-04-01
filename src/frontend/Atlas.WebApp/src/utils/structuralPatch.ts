export type StructuralPatchOperation =
  | { op: "add" | "replace"; path: string; value: unknown }
  | { op: "remove"; path: string };

export interface StructuralPatchPair {
  forward: StructuralPatchOperation[];
  backward: StructuralPatchOperation[];
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  if (value === null || typeof value !== "object") {
    return false;
  }
  const proto = Object.getPrototypeOf(value);
  return proto === Object.prototype || proto === null;
}

export function cloneStructuralValue<T>(value: T): T {
  if (Array.isArray(value)) {
    return value.map((item) => cloneStructuralValue(item)) as T;
  }

  if (isPlainObject(value)) {
    const cloned: Record<string, unknown> = {};
    for (const [key, item] of Object.entries(value)) {
      cloned[key] = cloneStructuralValue(item);
    }
    return cloned as T;
  }

  return value;
}

function escapePointerToken(token: string): string {
  return token.replace(/~/g, "~0").replace(/\//g, "~1");
}

function unescapePointerToken(token: string): string {
  return token.replace(/~1/g, "/").replace(/~0/g, "~");
}

function appendPath(basePath: string, token: string): string {
  return `${basePath}/${escapePointerToken(token)}`;
}

function parsePointer(path: string): string[] {
  if (path === "") {
    return [];
  }

  if (!path.startsWith("/")) {
    throw new Error(`Invalid JSON pointer path: ${path}`);
  }

  return path
    .slice(1)
    .split("/")
    .map(unescapePointerToken);
}

function pushOperation(
  target: StructuralPatchOperation[],
  operation: StructuralPatchOperation
): void {
  if (operation.op === "remove") {
    target.push(operation);
    return;
  }

  target.push({
    op: operation.op,
    path: operation.path,
    value: cloneStructuralValue(operation.value),
  });
}

function buildObjectPatch(
  previous: Record<string, unknown>,
  next: Record<string, unknown>,
  basePath: string,
  forward: StructuralPatchOperation[],
  backward: StructuralPatchOperation[]
): void {
  const keySet = new Set([...Object.keys(previous), ...Object.keys(next)]);
  for (const key of keySet) {
    const hasPrevious = Object.prototype.hasOwnProperty.call(previous, key);
    const hasNext = Object.prototype.hasOwnProperty.call(next, key);
    const currentPath = appendPath(basePath, key);

    if (!hasPrevious && hasNext) {
      pushOperation(forward, { op: "add", path: currentPath, value: next[key] });
      pushOperation(backward, { op: "remove", path: currentPath });
      continue;
    }

    if (hasPrevious && !hasNext) {
      pushOperation(forward, { op: "remove", path: currentPath });
      pushOperation(backward, { op: "add", path: currentPath, value: previous[key] });
      continue;
    }

    buildPatch(previous[key], next[key], currentPath, forward, backward);
  }
}

function buildArrayPatch(
  previous: unknown[],
  next: unknown[],
  basePath: string,
  forward: StructuralPatchOperation[],
  backward: StructuralPatchOperation[]
): void {
  const sharedLength = Math.min(previous.length, next.length);

  for (let index = 0; index < sharedLength; index += 1) {
    buildPatch(
      previous[index],
      next[index],
      appendPath(basePath, String(index)),
      forward,
      backward
    );
  }

  if (next.length > previous.length) {
    for (let index = previous.length; index < next.length; index += 1) {
      const currentPath = appendPath(basePath, String(index));
      pushOperation(forward, { op: "add", path: currentPath, value: next[index] });
      pushOperation(backward, { op: "remove", path: currentPath });
    }
  }

  if (previous.length > next.length) {
    for (let index = previous.length - 1; index >= next.length; index -= 1) {
      const currentPath = appendPath(basePath, String(index));
      pushOperation(forward, { op: "remove", path: currentPath });
      pushOperation(backward, { op: "add", path: currentPath, value: previous[index] });
    }
  }
}

function buildPatch(
  previous: unknown,
  next: unknown,
  basePath: string,
  forward: StructuralPatchOperation[],
  backward: StructuralPatchOperation[]
): void {
  if (Object.is(previous, next)) {
    return;
  }

  if (Array.isArray(previous) && Array.isArray(next)) {
    buildArrayPatch(previous, next, basePath, forward, backward);
    return;
  }

  if (isPlainObject(previous) && isPlainObject(next)) {
    buildObjectPatch(previous, next, basePath, forward, backward);
    return;
  }

  if (basePath === "") {
    pushOperation(forward, { op: "replace", path: "", value: next });
    pushOperation(backward, { op: "replace", path: "", value: previous });
    return;
  }

  if (typeof previous === "undefined") {
    pushOperation(forward, { op: "add", path: basePath, value: next });
    pushOperation(backward, { op: "remove", path: basePath });
    return;
  }

  if (typeof next === "undefined") {
    pushOperation(forward, { op: "remove", path: basePath });
    pushOperation(backward, { op: "add", path: basePath, value: previous });
    return;
  }

  pushOperation(forward, { op: "replace", path: basePath, value: next });
  pushOperation(backward, { op: "replace", path: basePath, value: previous });
}

export function createStructuralPatchPair(previous: unknown, next: unknown): StructuralPatchPair {
  const forward: StructuralPatchOperation[] = [];
  const backward: StructuralPatchOperation[] = [];
  buildPatch(previous, next, "", forward, backward);

  return {
    forward,
    backward: backward.reverse(),
  };
}

function parseArrayIndex(token: string, length: number, op: "add" | "replace" | "remove"): number {
  const index = Number(token);
  if (!Number.isInteger(index) || index < 0) {
    throw new Error(`Invalid array index token: ${token}`);
  }

  if (op === "add") {
    if (index > length) {
      throw new Error(`Array add index out of range: ${token}`);
    }
    return index;
  }

  if (index >= length) {
    throw new Error(`Array index out of range: ${token}`);
  }

  return index;
}

function applyOperation(target: unknown, operation: StructuralPatchOperation): unknown {
  const segments = parsePointer(operation.path);

  if (segments.length === 0) {
    if (operation.op === "remove") {
      throw new Error("Removing root value is not supported.");
    }
    return cloneStructuralValue(operation.value);
  }

  let parent: unknown = target;
  for (let index = 0; index < segments.length - 1; index += 1) {
    const token = segments[index];
    if (Array.isArray(parent)) {
      const itemIndex = parseArrayIndex(token, parent.length, "replace");
      parent = parent[itemIndex];
      continue;
    }

    if (!isPlainObject(parent)) {
      throw new Error(`Cannot traverse non-object path segment: ${token}`);
    }
    parent = parent[token];
  }

  const finalToken = segments[segments.length - 1];

  if (Array.isArray(parent)) {
    const itemIndex = parseArrayIndex(finalToken, parent.length, operation.op);
    if (operation.op === "remove") {
      parent.splice(itemIndex, 1);
      return target;
    }

    const nextValue = cloneStructuralValue(operation.value);
    if (operation.op === "add") {
      if (itemIndex === parent.length) {
        parent.push(nextValue);
      } else {
        parent.splice(itemIndex, 0, nextValue);
      }
      return target;
    }

    parent[itemIndex] = nextValue;
    return target;
  }

  if (!isPlainObject(parent)) {
    throw new Error(`Cannot apply patch on non-object target at path: ${operation.path}`);
  }

  if (operation.op === "remove") {
    delete parent[finalToken];
    return target;
  }

  parent[finalToken] = cloneStructuralValue(operation.value);
  return target;
}

export function applyStructuralPatch<T>(source: T, operations: StructuralPatchOperation[]): T {
  let target = source as unknown;
  for (const operation of operations) {
    target = applyOperation(target, operation);
  }
  return target as T;
}
