import { describe, expect, it } from "vitest";
import {
  collectExpressionDependencies,
  evaluateExpression,
  inferExpressionType,
  parseExpression,
  validateExpression
} from "./index";

describe("mendix-expression", () => {
  it("should parse and infer boolean expression", () => {
    const parsed = parseExpression("$Request/Amount > 50000");
    const inferred = inferExpressionType(parsed.ast, {
      variables: {
        $Request: { kind: "Object", entityRef: { kind: "entity", id: "ent_purchase_request" } }
      },
      enumerations: {}
    });
    expect(inferred.kind).toBe("Boolean");
  });

  it("should collect dependencies", () => {
    const parsed = parseExpression("$Request/Amount > 50000");
    const deps = collectExpressionDependencies(parsed.ast);
    expect(deps.some(dep => dep.kind === "variable" && dep.id === "$Request")).toBe(true);
  });

  it("should evaluate contains function", () => {
    const parsed = parseExpression("contains($Request/Reason, \"采购\")");
    const result = evaluateExpression(parsed, {
      variables: {
        $Request: {
          Reason: "采购办公设备"
        }
      }
    });
    expect(result).toBe(true);
  });

  it("should validate expression type", () => {
    const parsed = parseExpression("100");
    const result = validateExpression(parsed, { kind: "Integer" }, { variables: {}, enumerations: {} });
    expect(result.valid).toBe(true);
  });
});
