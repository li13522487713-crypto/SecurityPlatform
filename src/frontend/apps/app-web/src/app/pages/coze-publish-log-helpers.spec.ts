import { describe, expect, it } from "vitest";
import { buildDefaultPublishLogFilters, normalizePositiveInt } from "./coze-publish-log-helpers";

describe("coze-publish-log-helpers", () => {
  it("builds stable default filters", () => {
    expect(buildDefaultPublishLogFilters()).toEqual({
      source: "",
      kind: "",
      pageIndex: 1,
      pageSize: 20
    });
  });

  it("normalizes page numbers safely", () => {
    expect(normalizePositiveInt("3", 1)).toBe(3);
    expect(normalizePositiveInt("0", 1)).toBe(1);
    expect(normalizePositiveInt("-2", 1)).toBe(1);
    expect(normalizePositiveInt("oops", 1)).toBe(1);
    expect(normalizePositiveInt(undefined, 20)).toBe(20);
  });

  it("keeps default page size aligned with app-web log query", () => {
    const defaults = buildDefaultPublishLogFilters();
    expect(defaults.pageIndex).toBe(1);
    expect(defaults.pageSize).toBe(20);
  });
});
