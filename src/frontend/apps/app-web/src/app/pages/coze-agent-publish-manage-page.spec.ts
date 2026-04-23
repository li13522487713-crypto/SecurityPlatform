import { describe, expect, it } from "vitest";
import {
  formatPublishTime,
  getConnectorStatusKey,
  getPublishStatusKey,
  normalizePublishManageTab,
  summarizeConnectors
} from "./coze-agent-publish-manage-helpers";

describe("coze-agent-publish-manage-page helpers", () => {
  it("normalizes unsupported tabs to analysis", () => {
    expect(normalizePublishManageTab(null)).toBe("analysis");
    expect(normalizePublishManageTab("analysis")).toBe("analysis");
    expect(normalizePublishManageTab("logs")).toBe("logs");
    expect(normalizePublishManageTab("unknown")).toBe("analysis");
  });

  it("maps publish status to app-web i18n keys", () => {
    expect(getPublishStatusKey(5)).toBe("cozePublishManageStatusSuccess");
    expect(getPublishStatusKey(1)).toBe("cozePublishManageStatusFailed");
    expect(getPublishStatusKey(4)).toBe("cozePublishManageStatusPublishing");
    expect(getPublishStatusKey(undefined)).toBeNull();
  });

  it("maps connector status to app-web i18n keys", () => {
    expect(getConnectorStatusKey(2)).toBe("cozePublishManageStatusSuccess");
    expect(getConnectorStatusKey(3)).toBe("cozePublishManageStatusFailed");
    expect(getConnectorStatusKey(4)).toBe("cozePublishManageStatusDisabled");
    expect(getConnectorStatusKey(undefined)).toBeNull();
  });

  it("summarizes connector counters", () => {
    const summary = summarizeConnectors([
      { connector_publish_status: 2 },
      { connector_publish_status: 2 },
      { connector_publish_status: 3 }
    ]);

    expect(summary).toEqual({
      total: 3,
      successCount: 2,
      failedCount: 1
    });
  });

  it("formats publish time safely", () => {
    expect(formatPublishTime(undefined)).toBe("-");
    expect(formatPublishTime("not-a-number")).toBe("not-a-number");
    expect(formatPublishTime("1710000000000")).toContain("2024");
  });
});
