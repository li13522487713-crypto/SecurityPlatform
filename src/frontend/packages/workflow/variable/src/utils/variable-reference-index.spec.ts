import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { type WorkflowJSON } from '@coze-workflow/base';
import { describe, expect, it } from 'vitest';

import { buildVariableReferenceIndex } from './variable-reference-index';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const fixturesDir = path.resolve(__dirname, '../../../__fixtures__/workflow-large');

function readFixture(fileName: string): WorkflowJSON {
  return JSON.parse(readFileSync(path.join(fixturesDir, fileName), 'utf8')) as WorkflowJSON;
}

describe('buildVariableReferenceIndex', () => {
  it('indexes refs in variable-heavy workflow without scanning per lookup', () => {
    const index = buildVariableReferenceIndex(readFixture('variable-heavy.json'));

    expect(index.records.length).toBeGreaterThanOrEqual(50);
    expect(index.byUpstreamNodeId.has('start')).toBe(true);
    expect(index.byUpstreamNodeId.get('start')?.length).toBeGreaterThan(0);
  });

  it('indexes nested loop body refs', () => {
    const index = buildVariableReferenceIndex(readFixture('loop-nested.json'));

    expect(index.records.some(record => record.nodeId === 'loop_assign')).toBe(true);
  });
});
