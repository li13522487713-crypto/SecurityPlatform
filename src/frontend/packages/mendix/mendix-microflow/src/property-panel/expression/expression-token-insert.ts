export const compositeConditionTokens = ["and", "or", "not", "(", ")", "=", "!=", ">", "<", ">=", "<="] as const;

export function insertExpressionToken(raw: string, token: string): string {
  if (!raw.trim()) {
    return token;
  }

  return `${raw}${raw.endsWith(" ") ? "" : " "}${token}`;
}
