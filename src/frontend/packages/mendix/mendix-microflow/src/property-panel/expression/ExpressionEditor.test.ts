import { describe, expect, it } from "vitest";

import { insertExpressionToken } from "./expression-token-insert";

describe("ExpressionEditor expression token insertion", () => {
  it("inserts composite condition tokens with stable spacing", () => {
    expect(insertExpressionToken("", "and")).toBe("and");
    expect(insertExpressionToken("$amount > 10", "and")).toBe("$amount > 10 and");
    expect(insertExpressionToken("$amount > 10 ", "or")).toBe("$amount > 10 or");
    expect(insertExpressionToken("$flag", ")")).toBe("$flag )");
  });
});
