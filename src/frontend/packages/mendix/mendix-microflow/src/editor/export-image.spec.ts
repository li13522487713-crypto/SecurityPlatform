// @vitest-environment jsdom

import { cleanup } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { downloadBlob, exportCanvasAsPng, sanitizeExportFileName } from "./export-image";

vi.mock("html-to-image", () => ({
  toBlob: vi.fn(),
}));

afterEach(() => cleanup());

beforeEach(() => {
  vi.useFakeTimers();
  vi.clearAllMocks();
});

afterEach(() => {
  vi.runOnlyPendingTimers();
  vi.useRealTimers();
});

describe("export-image", () => {
  it("sanitizes invalid file-name characters", () => {
    expect(sanitizeExportFileName(" Order:/A*B? ")).toBe("Order_A_B_");
    expect(sanitizeExportFileName("")).toBe("microflow");
  });

  it("downloads png when html-to-image returns a blob", async () => {
    const { toBlob } = await import("html-to-image");
    const blob = new Blob(["hello"], { type: "image/png" });
    (toBlob as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(blob);

    const createUrl = vi.spyOn(URL, "createObjectURL").mockReturnValue("blob:test-url");
    const revokeUrl = vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => {});
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});

    const host = document.createElement("div");
    const result = await exportCanvasAsPng(host, "Order Flow");

    expect(result).toEqual({ ok: true, fileName: "Order Flow.png" });
    expect(createUrl).toHaveBeenCalledTimes(1);
    expect(clickSpy).toHaveBeenCalledTimes(1);
    vi.advanceTimersByTime(1_000);
    expect(revokeUrl).toHaveBeenCalledTimes(1);

    createUrl.mockRestore();
    revokeUrl.mockRestore();
    clickSpy.mockRestore();
  });

  it("returns error when html-to-image produces empty blob", async () => {
    const { toBlob } = await import("html-to-image");
    (toBlob as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(null);

    const host = document.createElement("div");
    const result = await exportCanvasAsPng(host, "MyFlow");

    expect(result).toEqual({ ok: false, error: "PNG export returned an empty image." });
  });

  it("writes blob download anchor attributes", () => {
    const createUrl = vi.spyOn(URL, "createObjectURL").mockReturnValue("blob:test-url");
    const revokeUrl = vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => {});
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});
    const blob = new Blob(["x"], { type: "image/png" });

    downloadBlob(blob, "example.png");

    expect(clickSpy).toHaveBeenCalled();
    vi.advanceTimersByTime(1_000);
    expect(revokeUrl).toHaveBeenCalledTimes(1);

    createUrl.mockRestore();
    revokeUrl.mockRestore();
    clickSpy.mockRestore();
  });
});
