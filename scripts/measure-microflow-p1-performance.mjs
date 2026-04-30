#!/usr/bin/env node

import { performance } from "node:perf_hooks";

const nodeCount = Number(process.env.MICROFLOW_P1_NODE_COUNT ?? 300);
const startedAt = performance.now();

const nodes = Array.from({ length: nodeCount }, (_, index) => ({
  id: `node-${index}`,
  name: `Node ${index}`,
  x: (index % 30) * 180,
  y: Math.floor(index / 30) * 120,
}));
const edges = nodes.slice(1).map((node, index) => ({
  id: `edge-${index}`,
  source: nodes[index].id,
  target: node.id,
}));

const indexStartedAt = performance.now();
const nodeIndex = new Map(nodes.map(node => [node.id, node]));
const edgeLookups = edges.map(edge => ({
  source: nodeIndex.get(edge.source)?.id,
  target: nodeIndex.get(edge.target)?.id,
}));
const finishedAt = performance.now();

const result = {
  scenario: "microflow-p1-synthetic-baseline",
  nodeCount,
  edgeCount: edges.length,
  buildFixtureMs: Number((indexStartedAt - startedAt).toFixed(3)),
  indexAndLookupMs: Number((finishedAt - indexStartedAt).toFixed(3)),
  totalMs: Number((finishedAt - startedAt).toFixed(3)),
  targets: {
    open300NodesP75Ms: 2500,
    panZoomP95FrameMs: 16,
    saveCompleteP95Ms: 800,
    debugFirstTraceP95Ms: 1000,
  },
  verifiedLookups: edgeLookups.length,
};

console.log(JSON.stringify(result, null, 2));
