import { mkdirSync, writeFileSync } from "node:fs";
import { performance } from "node:perf_hooks";
import { resolve } from "node:path";
import { findWorkspaceRoot } from "./microflow-production-gate-lib.ts";

const root = findWorkspaceRoot();
const outputDir = resolve(root, "artifacts/microflow-performance");
mkdirSync(outputDir, { recursive: true });

const nodeCounts = [100, 300, 500] as const;
const samplesPerCase = 5;

interface MicroflowObject {
  id: string;
  stableId: string;
  kind: string;
  officialType: string;
  caption: string;
  relativeMiddlePoint: { x: number; y: number };
  size: { width: number; height: number };
  editor: Record<string, unknown>;
  action?: Record<string, unknown>;
}

interface MicroflowFlow {
  id: string;
  stableId: string;
  kind: string;
  officialType: string;
  originObjectId: string;
  destinationObjectId: string;
  originConnectionIndex: number;
  destinationConnectionIndex: number;
  caseValues: unknown[];
  isErrorHandler: boolean;
  line: Record<string, unknown>;
  editor: Record<string, unknown>;
}

function createFlow(id: string, originObjectId: string, destinationObjectId: string): MicroflowFlow {
  return {
    id,
    stableId: id,
    kind: "sequence",
    officialType: "Microflows$SequenceFlow",
    originObjectId,
    destinationObjectId,
    originConnectionIndex: 0,
    destinationConnectionIndex: 0,
    caseValues: [],
    isErrorHandler: false,
    line: {
      kind: "orthogonal",
      points: [],
      routing: { mode: "auto", bendPoints: [] },
      style: { strokeType: "solid", strokeWidth: 2, arrow: "target" }
    },
    editor: { edgeKind: "sequence" }
  };
}

function createLogNode(index: number): MicroflowObject {
  const id = `perf-log-${index}`;
  return {
    id,
    stableId: id,
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: `Perf Log ${index}`,
    relativeMiddlePoint: { x: 320 + (index % 20) * 180, y: 160 + Math.floor(index / 20) * 120 },
    size: { width: 152, height: 72 },
    editor: { iconKey: "logMessage" },
    action: {
      id: `action-${id}`,
      kind: "logMessage",
      officialType: "Microflows$LogMessageAction",
      errorHandlingType: "rollback",
      editor: { category: "logging", iconKey: "logMessage", availability: "supported" },
      level: "info",
      logNodeName: "MicroflowPerformance",
      template: { text: `sample ${index}`, arguments: [] },
      includeContextVariables: false,
      includeTraceId: true
    }
  };
}

function createSchema(nodeCount: number) {
  const objects: MicroflowObject[] = [
    {
      id: "start",
      stableId: "start",
      kind: "startEvent",
      officialType: "Microflows$StartEvent",
      caption: "Start",
      relativeMiddlePoint: { x: 80, y: 160 },
      size: { width: 132, height: 70 },
      editor: { iconKey: "startEvent" }
    },
    ...Array.from({ length: nodeCount }, (_, index) => createLogNode(index)),
    {
      id: "end",
      stableId: "end",
      kind: "endEvent",
      officialType: "Microflows$EndEvent",
      caption: "End",
      relativeMiddlePoint: { x: 320 + nodeCount * 10, y: 160 },
      size: { width: 132, height: 70 },
      editor: { iconKey: "endEvent" }
    }
  ];
  const chain = ["start", ...Array.from({ length: nodeCount }, (_, index) => `perf-log-${index}`), "end"];
  const flows = chain.slice(0, -1).map((origin, index) => createFlow(`perf-flow-${index}`, origin, chain[index + 1]));
  return {
    schemaVersion: "1.0.0",
    mendixProfile: "mx10",
    id: `perf-${nodeCount}`,
    stableId: `perf-${nodeCount}`,
    name: `Perf_${nodeCount}`,
    displayName: `Perf ${nodeCount}`,
    moduleId: "performance",
    moduleName: "Performance",
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: {
      id: "root-collection",
      officialType: "Microflows$MicroflowObjectCollection",
      objects
    },
    flows,
    security: { applyEntityAccess: true, allowedModuleRoleIds: [] },
    concurrency: { allowConcurrentExecution: true, errorMicroflowId: null },
    exposure: { exportLevel: "module", markAsUsed: true },
    variables: { schemaId: `perf-${nodeCount}`, builtAt: new Date().toISOString(), all: [], parameters: {}, localVariables: {}, objectOutputs: {}, listOutputs: {}, loopVariables: {}, errorVariables: {}, systemVariables: {} },
    validation: { issues: [] },
    editor: { zoom: 1, selection: {}, viewport: { x: 0, y: 0, zoom: 1 } },
    audit: { version: "0.1.0", status: "draft", createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() }
  };
}

function measure<T>(work: () => T): { durationMs: number; value: T } {
  const started = performance.now();
  const value = work();
  return { durationMs: Number((performance.now() - started).toFixed(3)), value };
}

function renderGraph(schema: ReturnType<typeof createSchema>) {
  const objectById = new Map(schema.objectCollection.objects.map(object => [object.id, object]));
  return schema.flows.map(flow => ({
    id: flow.id,
    source: objectById.get(flow.originObjectId)?.relativeMiddlePoint,
    target: objectById.get(flow.destinationObjectId)?.relativeMiddlePoint
  }));
}

function validateGraph(schema: ReturnType<typeof createSchema>) {
  const objectIds = new Set(schema.objectCollection.objects.map(object => object.id));
  const issues: string[] = [];
  for (const flow of schema.flows) {
    if (!objectIds.has(flow.originObjectId)) issues.push(`missing origin ${flow.originObjectId}`);
    if (!objectIds.has(flow.destinationObjectId)) issues.push(`missing destination ${flow.destinationObjectId}`);
  }
  if (!objectIds.has("start")) issues.push("missing start");
  if (!objectIds.has("end")) issues.push("missing end");
  return issues;
}

function runPlan(schema: ReturnType<typeof createSchema>) {
  const nextByOrigin = new Map(schema.flows.map(flow => [flow.originObjectId, flow.destinationObjectId]));
  const visited: string[] = [];
  let cursor = "start";
  for (let guard = 0; guard < schema.objectCollection.objects.length + 2 && cursor; guard += 1) {
    visited.push(cursor);
    if (cursor === "end") break;
    cursor = nextByOrigin.get(cursor) ?? "";
  }
  return visited;
}

const cases = nodeCounts.map(nodeCount => {
  const schema = createSchema(nodeCount);
  const serialized = JSON.stringify(schema);
  const samples = Array.from({ length: samplesPerCase }, () => {
    const load = measure(() => JSON.parse(serialized));
    const render = measure(() => renderGraph(load.value));
    const save = measure(() => JSON.stringify(load.value));
    const validate = measure(() => validateGraph(load.value));
    const run = measure(() => runPlan(load.value));
    return {
      loadMs: load.durationMs,
      renderMs: render.durationMs,
      saveMs: save.durationMs,
      validateMs: validate.durationMs,
      runPlanMs: run.durationMs,
      renderedEdges: render.value.length,
      validationIssues: validate.value.length,
      visitedNodes: run.value.length
    };
  });
  const average = Object.fromEntries(
    ["loadMs", "renderMs", "saveMs", "validateMs", "runPlanMs"].map(key => [
      key,
      Number((samples.reduce((sum, sample) => sum + Number(sample[key as keyof typeof sample]), 0) / samples.length).toFixed(3))
    ])
  );
  return {
    nodeCount,
    flowCount: schema.flows.length,
    payloadBytes: Buffer.byteLength(serialized, "utf8"),
    samples,
    average
  };
});

const summary = {
  generatedAt: new Date().toISOString(),
  samplesPerCase,
  cases,
  thresholds: {
    loadMs: 50,
    renderMs: 75,
    saveMs: 75,
    validateMs: 75,
    runPlanMs: 75
  }
};

writeFileSync(resolve(outputDir, "microflow-performance-baseline.json"), `${JSON.stringify(summary, null, 2)}\n`, "utf8");
writeFileSync(
  resolve(outputDir, "microflow-performance-baseline.md"),
  [
    "# Microflow Performance Baseline",
    "",
    `- GeneratedAt: ${summary.generatedAt}`,
    `- Samples per case: ${samplesPerCase}`,
    "",
    "| Nodes | Flows | Payload bytes | Load avg(ms) | Render avg(ms) | Save avg(ms) | Validate avg(ms) | Run plan avg(ms) |",
    "|---:|---:|---:|---:|---:|---:|---:|---:|",
    ...cases.map(item => `| ${item.nodeCount} | ${item.flowCount} | ${item.payloadBytes} | ${item.average.loadMs} | ${item.average.renderMs} | ${item.average.saveMs} | ${item.average.validateMs} | ${item.average.runPlanMs} |`),
    "",
    "## Scope",
    "",
    "This baseline measures deterministic schema load, graph projection, save serialization, structural validation, and run-plan traversal for 100/300/500-node microflows. Browser paint and live backend execution are covered by the production gate and Playwright suites."
  ].join("\n"),
  "utf8"
);

console.log(`Wrote ${resolve(outputDir, "microflow-performance-baseline.json")}`);
