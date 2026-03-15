import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import type { SeedState, StoredAuthState, E2ERole } from "../catalog/seed-types";

const e2eRoot = path.resolve(process.cwd(), "e2e");
const authRoot = path.join(e2eRoot, ".auth");
const testResultsRoot = path.resolve(process.cwd(), "test-results");
const seedStateFile = path.join(testResultsRoot, "seed-state.json");

export function ensureE2eStateDirs() {
  mkdirSync(authRoot, { recursive: true });
  mkdirSync(testResultsRoot, { recursive: true });
}

export function getAuthStatePath(role: E2ERole) {
  return path.join(authRoot, `${role}.json`);
}

export function writeAuthState(role: E2ERole, state: StoredAuthState) {
  ensureE2eStateDirs();
  writeFileSync(getAuthStatePath(role), JSON.stringify(state, null, 2), "utf8");
}

export function readAuthState(role: E2ERole): StoredAuthState {
  return JSON.parse(readFileSync(getAuthStatePath(role), "utf8")) as StoredAuthState;
}

export function writeSeedState(state: SeedState) {
  ensureE2eStateDirs();
  writeFileSync(seedStateFile, JSON.stringify(state, null, 2), "utf8");
}

export function readSeedState(): SeedState {
  return JSON.parse(readFileSync(seedStateFile, "utf8")) as SeedState;
}

export function getSeedStatePath() {
  return seedStateFile;
}
