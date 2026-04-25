import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { describe, expect, it } from 'vitest';

import { validateWorkflowSchemaInvariants } from './schema-invariants';
import { type WorkflowJSON } from '../types';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const fixturesDir = path.resolve(__dirname, '../../../__fixtures__/workflow-large');

function readFixture(fileName: string): WorkflowJSON {
  return JSON.parse(readFileSync(path.join(fixturesDir, fileName), 'utf8')) as WorkflowJSON;
}

describe('validateWorkflowSchemaInvariants', () => {
  const fixtureNames = readdirSync(fixturesDir)
    .filter(file => file.endsWith('.json'))
    .sort();

  it.each(fixtureNames)('%s has valid schema invariants', fileName => {
    const result = validateWorkflowSchemaInvariants(readFixture(fileName));

    expect(result.issues).toEqual([]);
    expect(result.valid).toBe(true);
  });

  it('reports missing edge endpoints', () => {
    const fixture = readFixture('long-chain-30.json');
    fixture.edges[0] = {
      sourceNodeID: 'missing',
      targetNodeID: 'n1',
    };

    const result = validateWorkflowSchemaInvariants(fixture);

    expect(result.valid).toBe(false);
    expect(result.issues.map(item => item.code)).toContain('EDGE_SOURCE_NODE_MISSING');
  });

  it('reports non-json values before stringify', () => {
    const fixture = readFixture('long-chain-30.json') as unknown as WorkflowJSON & {
      debug?: unknown;
    };
    fixture.debug = new Map();

    const result = validateWorkflowSchemaInvariants(fixture);

    expect(result.valid).toBe(false);
    expect(result.issues.map(item => item.code)).toContain('SCHEMA_NON_JSON_OBJECT');
  });

  it.each([
    ['condition-branches.json', 'condition', 'unknown-branch', 'CONDITION_PORT_INVALID'],
    ['loop-nested.json', 'loop', 'unknown-loop-port', 'LOOP_PORT_INVALID'],
    ['batch-processing.json', 'batch', 'unknown-batch-port', 'BATCH_PORT_INVALID'],
  ])('reports invalid dynamic port in %s', (fileName, nodeId, portId, expectedCode) => {
    const fixture = readFixture(fileName);
    const edge = fixture.edges.find(item => item.sourceNodeID === nodeId);
    expect(edge).toBeTruthy();

    edge!.sourcePortID = portId;

    const result = validateWorkflowSchemaInvariants(fixture);

    expect(result.valid).toBe(false);
    expect(result.issues.map(item => item.code)).toContain(expectedCode);
  });
});
