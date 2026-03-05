import { describe, it, expect } from "vitest";
import {
  pad2,
  formatTimeSpan,
  normalizeTimeSpanInput,
  normalizeDateTimeInput,
  normalizeJsonValue,
  formatLocalIsoSeconds
} from "@/composables/useTimeNormalize";

describe("pad2", () => {
  it("pads single-digit numbers with leading zero", () => {
    expect(pad2(5)).toBe("05");
    expect(pad2(0)).toBe("00");
  });

  it("leaves double-digit numbers unchanged", () => {
    expect(pad2(10)).toBe("10");
    expect(pad2(59)).toBe("59");
  });
});

describe("formatTimeSpan", () => {
  it("formats zero seconds as 00:00:00", () => {
    expect(formatTimeSpan(0)).toBe("00:00:00");
  });

  it("formats seconds into HH:mm:ss", () => {
    expect(formatTimeSpan(3661)).toBe("01:01:01");
    expect(formatTimeSpan(3600)).toBe("01:00:00");
    expect(formatTimeSpan(60)).toBe("00:01:00");
    expect(formatTimeSpan(1)).toBe("00:00:01");
  });

  it("formats days when >= 86400 seconds", () => {
    expect(formatTimeSpan(86400)).toBe("1.00:00:00");
    expect(formatTimeSpan(90061)).toBe("1.01:01:01");
  });

  it("handles negative values with minus sign", () => {
    expect(formatTimeSpan(-3600)).toBe("-01:00:00");
  });
});

describe("normalizeTimeSpanInput", () => {
  it("passes through HH:mm:ss format unchanged", () => {
    expect(normalizeTimeSpanInput("01:30:00")).toBe("01:30:00");
  });

  it("appends :00 for HH:mm format", () => {
    expect(normalizeTimeSpanInput("01:30")).toBe("01:30:00");
  });

  it("converts unit notation (1h, 30m, 90s) to HH:mm:ss", () => {
    expect(normalizeTimeSpanInput("1h")).toBe("01:00:00");
    expect(normalizeTimeSpanInput("30m")).toBe("00:30:00");
    expect(normalizeTimeSpanInput("90s")).toBe("00:01:30");
  });

  it("returns non-string values unchanged", () => {
    expect(normalizeTimeSpanInput(42)).toBe(42);
    expect(normalizeTimeSpanInput(null)).toBeNull();
    expect(normalizeTimeSpanInput(undefined)).toBeUndefined();
  });
});

describe("normalizeDateTimeInput", () => {
  it("returns non-string values unchanged", () => {
    expect(normalizeDateTimeInput(null)).toBeNull();
    expect(normalizeDateTimeInput(42)).toBe(42);
  });

  it("passes through ISO format with T separator unchanged", () => {
    const iso = "2024-01-15T10:30:00";
    expect(normalizeDateTimeInput(iso)).toBe(iso);
  });

  it("converts YYYY-MM-DD to ISO format with midnight time", () => {
    expect(normalizeDateTimeInput("2024-01-15")).toBe("2024-01-15T00:00:00");
  });

  it("converts YYYY-MM-DD HH:mm:ss to ISO format with T separator", () => {
    expect(normalizeDateTimeInput("2024-01-15 10:30:45")).toBe("2024-01-15T10:30:45");
  });
});

describe("normalizeJsonValue", () => {
  it("returns null/undefined values unchanged", () => {
    expect(normalizeJsonValue(null)).toBeNull();
    expect(normalizeJsonValue(undefined)).toBeUndefined();
  });

  it("returns primitive values (numbers, booleans) unchanged", () => {
    expect(normalizeJsonValue(42)).toBe(42);
    expect(normalizeJsonValue(true)).toBe(true);
  });

  it("normalizes date strings in objects", () => {
    const obj = { scheduledAt: "2024-01-15 10:30:00", count: 5 };
    const result = normalizeJsonValue(obj) as Record<string, unknown>;
    expect(result.scheduledAt).toBe("2024-01-15T10:30:00");
    expect(result.count).toBe(5);
  });

  it("normalizes dates in array elements", () => {
    const arr = ["2024-01-15", "not-a-date"];
    const result = normalizeJsonValue(arr) as unknown[];
    expect(result[0]).toBe("2024-01-15T00:00:00");
  });
});

describe("formatLocalIsoSeconds", () => {
  it("formats a Date to local ISO string in YYYY-MM-DDTHH:mm:ss format", () => {
    const d = new Date(2024, 0, 15, 10, 30, 45);
    const result = formatLocalIsoSeconds(d);
    expect(result).toBe(`2024-01-15T10:30:45`);
  });

  it("pads single-digit month/day/hour/minute/second", () => {
    const d = new Date(2024, 0, 5, 9, 8, 7); // Jan 5, 09:08:07
    const result = formatLocalIsoSeconds(d);
    expect(result).toBe("2024-01-05T09:08:07");
  });
});
