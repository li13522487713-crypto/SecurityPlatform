import { getValueByPath } from "./path-utils";

function normalizeLanguage(raw: string): string {
  const value = raw.trim().toLowerCase();
  if (!value) {
    return "plaintext";
  }
  if (value === "js" || value === "javascript" || value === "nodejs") {
    return "javascript";
  }
  if (value === "ts" || value === "typescript") {
    return "typescript";
  }
  if (value === "py" || value === "python") {
    return "python";
  }
  if (value === "sql" || value === "mysql" || value === "postgres") {
    return "sql";
  }
  if (value === "json") {
    return "json";
  }
  return "plaintext";
}

export function resolveEditorLanguage(config: Record<string, unknown>, explicitPath?: string, fallback = "json"): string {
  if (!explicitPath) {
    return normalizeLanguage(fallback);
  }
  const pathValue = getValueByPath(config, explicitPath);
  if (typeof pathValue !== "string") {
    return normalizeLanguage(fallback);
  }
  return normalizeLanguage(pathValue);
}

