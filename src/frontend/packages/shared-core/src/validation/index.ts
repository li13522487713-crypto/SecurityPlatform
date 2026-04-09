export function validateRequired(value: unknown): boolean {
  if (value === null || value === undefined) return false;
  if (typeof value === "string") return value.trim().length > 0;
  if (Array.isArray(value)) return value.length > 0;
  return true;
}

export function validateMaxLength(value: string | undefined, maxLength: number): boolean {
  if (value === undefined) return true;
  return value.length <= maxLength;
}
