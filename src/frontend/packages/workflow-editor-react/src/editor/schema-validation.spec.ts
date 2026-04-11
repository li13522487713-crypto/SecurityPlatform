import { describe, expect, it } from "vitest";
import { validateConfigBySchema } from "./schema-validation";

describe("schema-validation", () => {
  const schema = JSON.stringify({
    type: "object",
    required: ["provider", "maxTokens"],
    properties: {
      provider: { type: "string", minLength: 1 },
      maxTokens: { type: "integer", minimum: 1, maximum: 4096 },
      stream: { type: "boolean" }
    }
  });

  it("校验 required 与范围规则", () => {
    const result = validateConfigBySchema({ provider: "", maxTokens: 0 }, schema);
    expect(result.issues.length).toBeGreaterThan(0);
    expect(result.issues.some((issue) => issue.path.includes("provider"))).toBe(true);
    expect(result.issues.some((issue) => issue.path.includes("maxTokens"))).toBe(true);
  });

  it("合法配置通过", () => {
    const result = validateConfigBySchema({ provider: "openai", maxTokens: 1024, stream: true }, schema);
    expect(result.issues).toEqual([]);
  });
});