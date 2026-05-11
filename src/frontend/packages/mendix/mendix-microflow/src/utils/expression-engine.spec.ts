import { describe, expect, it } from "vitest";

import type { MicroflowVariable } from "../types/mendix-types";
import { parseExpression } from "./expression-engine";

describe("expression-engine", () => {
  const vars: MicroflowVariable[] = [
    { name: "$order", type: "Object", attributes: ["Amount", "Customer", "Total" ] },
    { name: "$count", type: "Integer" },
    { name: "$price", type: "Decimal" },
  ];

  it("rejects arithmetic division using '/' and suggests div", () => {
    const result = parseExpression("$price / $count", vars);

    expect(result.valid).toBe(false);
    expect(result.diagnostics.some(item => item.code === "MF_EXPR_INVALID_DIVISION")).toBe(true);
    expect(result.errorMessage).toBe("除法请使用 div 而不是 /。");
  });

  it("allows slash in attribute access", () => {
    const result = parseExpression("$order/Amount", vars);

    expect(result.valid).toBe(true);
    expect(result.usedVariables).toContain("order");
  });
});
