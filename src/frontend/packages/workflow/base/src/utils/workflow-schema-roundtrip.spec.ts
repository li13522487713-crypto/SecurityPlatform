import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { describe, expect, it } from 'vitest';

import { stableStringifyWorkflowSchema } from './stable-json';

interface WorkflowNodeFixture {
  id: string;
  type: string;
  meta?: unknown;
  data: Record<string, unknown>;
  blocks?: WorkflowNodeFixture[];
  edges?: WorkflowEdgeFixture[];
}

interface WorkflowEdgeFixture {
  sourceNodeID: string;
  targetNodeID: string;
  sourcePortID?: string | number;
  targetPortID?: string | number;
}

interface WorkflowFixture {
  nodes: WorkflowNodeFixture[];
  edges: WorkflowEdgeFixture[];
}

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const fixturesDir = path.resolve(__dirname, '../../../__fixtures__/workflow-large');

function readFixture(fileName: string): WorkflowFixture {
  return JSON.parse(
    readFileSync(path.join(fixturesDir, fileName), 'utf8'),
  ) as WorkflowFixture;
}

function collectNodes(nodes: WorkflowNodeFixture[]): WorkflowNodeFixture[] {
  return nodes.flatMap(node => [
    node,
    ...collectNodes(node.blocks ?? []),
  ]);
}

function collectEdges(fixture: WorkflowFixture): WorkflowEdgeFixture[] {
  return [
    ...fixture.edges,
    ...collectNodes(fixture.nodes).flatMap(node => node.edges ?? []),
  ];
}

function cloneThroughJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

describe('large workflow schema roundtrip fixtures', () => {
  const fixtureNames = readdirSync(fixturesDir)
    .filter(file => file.endsWith('.json'))
    .sort();

  it.each(fixtureNames)('%s keeps semantic counts and stable hash', fileName => {
    const original = readFixture(fileName);
    const hashA = stableStringifyWorkflowSchema(original);

    const hydrated = cloneThroughJson(original);
    const rebuilt = cloneThroughJson(hydrated);
    const hashB = stableStringifyWorkflowSchema(rebuilt);

    expect(collectNodes(rebuilt.nodes).length).toBe(
      collectNodes(original.nodes).length,
    );
    expect(collectEdges(rebuilt).length).toBe(collectEdges(original).length);
    expect(hashB).toBe(hashA);
  });

  it('large-graph-100 covers the expected size floor', () => {
    const fixture = readFixture('large-graph-100.json');

    expect(fixture.nodes).toHaveLength(100);
    expect(fixture.edges.length).toBeGreaterThanOrEqual(120);
    expect(new Set(fixture.nodes.map(node => node.type)).size).toBeGreaterThanOrEqual(
      10,
    );
  });

  it('large-graph-100 stable stringify stays within baseline budget', () => {
    const fixture = readFixture('large-graph-100.json');
    const startedAt = performance.now();
    const serialized = stableStringifyWorkflowSchema(fixture);
    const elapsed = performance.now() - startedAt;

    expect(serialized.length).toBeGreaterThan(10_000);
    expect(elapsed).toBeLessThan(1_000);
  });

  it('long-chain-30 covers the expected size floor', () => {
    const fixture = readFixture('long-chain-30.json');

    expect(fixture.nodes).toHaveLength(30);
    expect(fixture.edges).toHaveLength(29);
  });

  it('sub-workflow fixture keeps big workflow ids as strings', () => {
    const fixture = readFixture('sub-workflow-big-id.json');
    const subWorkflowNodes = fixture.nodes.filter(node => node.type === '9');

    expect(subWorkflowNodes).toHaveLength(2);
    for (const node of subWorkflowNodes) {
      const inputs = node.data.inputs as Record<string, unknown>;

      expect(typeof inputs.workflowId).toBe('string');
      expect(inputs.workflowId).toBe('9223372036854775807');
    }
  });

  it('external resource fixture keeps resource ids separate from display data', () => {
    const fixture = readFixture('external-resources-mixed.json');
    const plugin = fixture.nodes.find(node => node.id === 'plugin');
    const database = fixture.nodes.find(node => node.id === 'database');
    const knowledge = fixture.nodes.find(node => node.id === 'knowledge');

    expect(JSON.stringify(plugin?.data)).toContain('9223372036854770001');
    expect(JSON.stringify(database?.data)).toContain('9223372036854770201');
    expect(JSON.stringify(knowledge?.data)).toContain('9223372036854770101');
  });
});
