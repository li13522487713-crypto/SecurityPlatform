import type { MicroflowTestRunSample } from "./trace-types";

const storageKey = "atlas_mendix_microflow_test_run_samples";

export type MicroflowTestRunSamplesByMicroflowId = Record<string, MicroflowTestRunSample[]>;

export function readStoredTestRunSamples(): MicroflowTestRunSamplesByMicroflowId {
  if (typeof window === "undefined") {
    return {};
  }
  try {
    const raw = window.localStorage.getItem(storageKey);
    if (!raw) {
      return {};
    }
    return normalizeSamplesByMicroflowId(JSON.parse(raw));
  } catch {
    return {};
  }
}

export function writeStoredTestRunSamples(samplesByMicroflowId: MicroflowTestRunSamplesByMicroflowId): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.localStorage.setItem(storageKey, JSON.stringify(trimSamples(samplesByMicroflowId)));
  } catch {
    // Storage quota or private-mode failures should not block test runs.
  }
}

function trimSamples(samplesByMicroflowId: MicroflowTestRunSamplesByMicroflowId): MicroflowTestRunSamplesByMicroflowId {
  return Object.fromEntries(
    Object.entries(samplesByMicroflowId)
      .filter(([microflowId]) => microflowId)
      .map(([microflowId, samples]) => [microflowId, samples.slice(0, 20)]),
  );
}

function normalizeSamplesByMicroflowId(value: unknown): MicroflowTestRunSamplesByMicroflowId {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return {};
  }
  const result: MicroflowTestRunSamplesByMicroflowId = {};
  for (const [microflowId, samples] of Object.entries(value as Record<string, unknown>)) {
    if (!microflowId || !Array.isArray(samples)) {
      continue;
    }
    result[microflowId] = samples.map(normalizeSample).filter(Boolean).slice(0, 20) as MicroflowTestRunSample[];
  }
  return result;
}

function normalizeSample(value: unknown): MicroflowTestRunSample | undefined {
  if (!value || typeof value !== "object") {
    return undefined;
  }
  const sample = value as Partial<MicroflowTestRunSample>;
  if (!sample.id || !sample.name || !sample.parameters || typeof sample.parameters !== "object") {
    return undefined;
  }
  return {
    id: String(sample.id),
    name: String(sample.name),
    parameters: sample.parameters as Record<string, unknown>,
    expectedResult: sample.expectedResult,
    lastResult: sample.lastResult,
    lastStatus: sample.lastStatus,
    lastRunId: sample.lastRunId,
    lastRunAt: sample.lastRunAt,
    previousResult: sample.previousResult,
    updatedAt: typeof sample.updatedAt === "string" ? sample.updatedAt : new Date().toISOString(),
  };
}
