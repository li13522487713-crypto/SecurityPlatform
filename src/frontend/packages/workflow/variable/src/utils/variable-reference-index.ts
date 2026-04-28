/*
 * Copyright 2025 coze-dev Authors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

import { type WorkflowJSON, type WorkflowNodeJSON } from '@coze-workflow/base';

export interface VariableReferenceRecord {
  nodeId: string;
  path: string;
  source: string;
  blockID?: string;
  name?: string;
}

export interface WorkflowVariableReferenceIndex {
  byNodeId: Map<string, VariableReferenceRecord[]>;
  byUpstreamNodeId: Map<string, VariableReferenceRecord[]>;
  records: VariableReferenceRecord[];
}

function collectNodes(nodes: WorkflowNodeJSON[]): WorkflowNodeJSON[] {
  return nodes.flatMap(node => [node, ...collectNodes(node.blocks ?? [])]);
}

function visitValue(
  value: unknown,
  path: string,
  nodeId: string,
  records: VariableReferenceRecord[],
): void {
  if (!value || typeof value !== 'object') {
    return;
  }

  if (Array.isArray(value)) {
    value.forEach((item, index) => visitValue(item, `${path}[${index}]`, nodeId, records));
    return;
  }

  const objectValue = value as Record<string, unknown>;
  const refType = objectValue.type;
  const content = objectValue.content;

  if (
    refType === 'ref' &&
    content &&
    typeof content === 'object' &&
    'source' in content
  ) {
    const refContent = content as {
      source?: string;
      blockID?: string;
      name?: string;
    };

    records.push({
      nodeId,
      path,
      source: refContent.source ?? 'unknown',
      blockID: refContent.blockID,
      name: refContent.name,
    });
  }

  for (const [key, child] of Object.entries(objectValue)) {
    visitValue(child, `${path}.${key}`, nodeId, records);
  }
}

export function buildVariableReferenceIndex(schema: WorkflowJSON): WorkflowVariableReferenceIndex {
  const records: VariableReferenceRecord[] = [];

  for (const node of collectNodes(schema.nodes)) {
    visitValue(node.data, `nodes.${node.id}.data`, node.id, records);
  }

  const byNodeId = new Map<string, VariableReferenceRecord[]>();
  const byUpstreamNodeId = new Map<string, VariableReferenceRecord[]>();

  for (const record of records) {
    const nodeRecords = byNodeId.get(record.nodeId) ?? [];
    nodeRecords.push(record);
    byNodeId.set(record.nodeId, nodeRecords);

    if (record.blockID) {
      const upstreamRecords = byUpstreamNodeId.get(record.blockID) ?? [];
      upstreamRecords.push(record);
      byUpstreamNodeId.set(record.blockID, upstreamRecords);
    }
  }

  return {
    byNodeId,
    byUpstreamNodeId,
    records,
  };
}
