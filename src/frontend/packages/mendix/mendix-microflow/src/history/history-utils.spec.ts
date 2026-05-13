import { describe, expect, it } from "vitest";

import { labelForHistoryReason } from "./history-utils";

describe("history-utils", () => {
  it("returns reconnect label for reconnectEdge reason", () => {
    expect(labelForHistoryReason("reconnectEdge")).toBe("Reconnect edge");
  });
});

