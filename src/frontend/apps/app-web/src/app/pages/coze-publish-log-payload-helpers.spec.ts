import { describe, expect, it } from "vitest";
import { buildPublishLogPreview, formatPublishLogPayload } from "./coze-publish-log-payload-helpers";

describe("coze-publish-log-payload-helpers", () => {
  it("formats null-like payload safely", () => {
    expect(formatPublishLogPayload(undefined)).toBe("-");
    expect(formatPublishLogPayload(null)).toBe("-");
    expect(formatPublishLogPayload("")).toBe("-");
  });

  it("formats object payload as pretty json", () => {
    expect(formatPublishLogPayload({ foo: "bar" })).toContain("\"foo\": \"bar\"");
  });

  it("builds truncated preview when payload is long", () => {
    const preview = buildPublishLogPreview({ text: "x".repeat(400) }, 20);
    expect(preview.endsWith("...")).toBe(true);
  });
});
