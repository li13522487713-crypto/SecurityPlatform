import type { MicroflowExpression } from "../schema/types";

export interface MicroflowExpressionReferences {
  variables: string[];
  attributeAccesses: Array<{ variableName: string; attributeName: string }>;
}

export function parseExpressionReferences(expression: string | MicroflowExpression | undefined): MicroflowExpressionReferences {
  const raw = typeof expression === "string" ? expression : expression?.raw ?? expression?.text ?? "";
  const variableNames = new Set<string>();
  const attributeAccesses: MicroflowExpressionReferences["attributeAccesses"] = [];
  for (const match of raw.matchAll(/\$([A-Za-z_][\w]*)(?:\/([A-Za-z_][\w]*))?/g)) {
    const variableName = match[1];
    variableNames.add(variableName);
    if (match[2]) {
      attributeAccesses.push({ variableName, attributeName: match[2] });
    }
  }
  return { variables: [...variableNames], attributeAccesses };
}
