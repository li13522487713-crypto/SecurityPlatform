import { describe, expect, it } from "vitest";
import { buildDefaultTriggerFormValues, normalizeTriggerConfigJson } from "./coze-trigger-form-helpers";

describe("coze-trigger-form-helpers", () => {
  it("builds stable defaults", () => {
    expect(buildDefaultTriggerFormValues()).toEqual({
      name: "",
      triggerType: "schedule",
      configJson: "{\"cron\":\"0 8 * * *\"}",
      enabled: true
    });
  });

  it("normalizes empty config to object json", () => {
    expect(normalizeTriggerConfigJson("")).toBe("{}");
    expect(normalizeTriggerConfigJson("   ")).toBe("{}");
    expect(normalizeTriggerConfigJson("{\"cron\":\"0 8 * * *\"}")).toBe("{\"cron\":\"0 8 * * *\"}");
  });
});
