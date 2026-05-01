import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  readStoredTestRunSamples,
  writeStoredTestRunSamples,
  type MicroflowTestRunSamplesByMicroflowId,
} from "../test-run-samples-store";

describe("test-run-samples-store", () => {
  const localStorageMock = createLocalStorageMock();

  beforeEach(() => {
    vi.stubGlobal("window", { localStorage: localStorageMock });
    localStorageMock.clear();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("persists samples by microflow id", () => {
    const samples: MicroflowTestRunSamplesByMicroflowId = {
      MF_A: [{
        id: "sample-1",
        name: "numbers",
        parameters: { numbers: [1, 2, 3] },
        expectedResult: 120,
        lastResult: 119,
        lastStatus: "success",
        lastRunId: "run-1",
        previousResult: 118,
        updatedAt: "2026-05-02T00:00:00.000Z",
      }],
    };

    writeStoredTestRunSamples(samples);

    expect(readStoredTestRunSamples()).toEqual(samples);
  });

  it("drops invalid entries and caps each microflow to 20 samples", () => {
    const samples = Array.from({ length: 25 }, (_item, index) => ({
      id: `sample-${index}`,
      name: `Sample ${index}`,
      parameters: { index },
      updatedAt: "2026-05-02T00:00:00.000Z",
    }));
    localStorageMock.setItem("atlas_mendix_microflow_test_run_samples", JSON.stringify({
      MF_A: [...samples, { id: "bad", name: "bad" }, null],
      "": samples,
      MF_B: "not-array",
    }));

    const stored = readStoredTestRunSamples();

    expect(Object.keys(stored)).toEqual(["MF_A"]);
    expect(stored.MF_A).toHaveLength(20);
    expect(stored.MF_A[0]?.id).toBe("sample-0");
    expect(stored.MF_A.at(-1)?.id).toBe("sample-19");
  });

  it("returns empty state when storage is unavailable or corrupted", () => {
    localStorageMock.setItem("atlas_mendix_microflow_test_run_samples", "{bad");

    expect(readStoredTestRunSamples()).toEqual({});

    vi.stubGlobal("window", undefined);

    expect(readStoredTestRunSamples()).toEqual({});
  });
});

function createLocalStorageMock() {
  let store = new Map<string, string>();
  return {
    getItem: vi.fn((key: string) => store.get(key) ?? null),
    setItem: vi.fn((key: string, value: string) => {
      store.set(key, value);
    }),
    removeItem: vi.fn((key: string) => {
      store.delete(key);
    }),
    clear: vi.fn(() => {
      store = new Map<string, string>();
    }),
  };
}
