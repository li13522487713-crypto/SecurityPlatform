const RESERVED_SYSTEM_VARIABLES = [
  "$currentUser",
  "$currentSession",
  "$currentIndex",
  "$latestError",
  "$latestHttpResponse",
  "$latestSoapFault",
] as const;

const RESERVED_SYSTEM_VARIABLE_NAME_MAP = new Map(
  RESERVED_SYSTEM_VARIABLES.map(name => [normalizeReservedVariableName(name), name] as const),
);

export function normalizeReservedVariableName(name: string): string {
  const trimmed = name.trim();
  return trimmed.startsWith("$") ? trimmed.slice(1).toLocaleLowerCase() : trimmed.toLocaleLowerCase();
}

export function resolveReservedSystemVariable(name: string): (typeof RESERVED_SYSTEM_VARIABLES)[number] | undefined {
  return RESERVED_SYSTEM_VARIABLE_NAME_MAP.get(normalizeReservedVariableName(name));
}

export function isReservedSystemVariableName(name: string): boolean {
  return resolveReservedSystemVariable(name) !== undefined;
}

