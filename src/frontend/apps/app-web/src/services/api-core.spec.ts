import { describe, expect, it } from "vitest";
import { extractResourceId } from "./api-core";

describe("extractResourceId", () => {
  it("supports lowercase id payloads", () => {
    expect(extractResourceId({ id: "123" })).toBe("123");
  });

  it("supports uppercase Id payloads", () => {
    expect(extractResourceId({ Id: "456" })).toBe("456");
  });

  it("normalizes numeric ids", () => {
    expect(extractResourceId({ Id: 789 })).toBe("789");
  });

  it("returns null when payload does not contain a usable id", () => {
    expect(extractResourceId({ id: "" })).toBeNull();
    expect(extractResourceId(undefined)).toBeNull();
  });
});
