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

import { StandardNodeType, type WorkflowJSON, type WorkflowNodeJSON } from '../types';

export interface WorkflowSchemaInvariantIssue {
  code: string;
  path: string;
  message: string;
}

export interface WorkflowSchemaInvariantResult {
  valid: boolean;
  issues: WorkflowSchemaInvariantIssue[];
}

function issue(code: string, path: string, message: string): WorkflowSchemaInvariantIssue {
  return { code, path, message };
}

function inspectJsonValue(
  value: unknown,
  path: string,
  issues: WorkflowSchemaInvariantIssue[],
  seen: WeakSet<object>,
): void {
  if (value === undefined) {
    issues.push(issue('SCHEMA_UNDEFINED_VALUE', path, 'Workflow schema must not contain undefined.'));
    return;
  }

  if (typeof value === 'function') {
    issues.push(issue('SCHEMA_FUNCTION_VALUE', path, 'Workflow schema must not contain Function values.'));
    return;
  }

  if (value === null || typeof value !== 'object') {
    return;
  }

  if (value instanceof Date || value instanceof Map || value instanceof Set) {
    issues.push(
      issue(
        'SCHEMA_NON_JSON_OBJECT',
        path,
        'Workflow schema must not contain Date, Map, or Set instances.',
      ),
    );
    return;
  }

  if (seen.has(value)) {
    issues.push(issue('SCHEMA_CIRCULAR_REFERENCE', path, 'Workflow schema must not contain circular references.'));
    return;
  }

  seen.add(value);

  if (Array.isArray(value)) {
    value.forEach((item, index) => inspectJsonValue(item, `${path}[${index}]`, issues, seen));
  } else {
    for (const [key, child] of Object.entries(value)) {
      inspectJsonValue(child, `${path}.${key}`, issues, seen);
    }
  }

  seen.delete(value);
}

function collectNodes(nodes: WorkflowNodeJSON[]): WorkflowNodeJSON[] {
  return nodes.flatMap(node => [node, ...collectNodes(node.blocks ?? [])]);
}

function nodePathById(nodes: WorkflowNodeJSON[]): Map<string, WorkflowNodeJSON> {
  return new Map(collectNodes(nodes).map(node => [node.id, node]));
}

function isValidSelectorPort(port: string): boolean {
  return port === 'false' || port === 'true' || /^true_\d+$/.test(port) || port === 'branch_error';
}

function validateEdgePorts(
  edge: { sourceNodeID: string; targetNodeID: string; sourcePortID?: string | number; targetPortID?: string | number },
  sourceNode: WorkflowNodeJSON,
  path: string,
  issues: WorkflowSchemaInvariantIssue[],
): void {
  if (edge.sourcePortID === undefined) {
    return;
  }

  if (typeof edge.sourcePortID !== 'string' && typeof edge.sourcePortID !== 'number') {
    issues.push(issue('EDGE_SOURCE_PORT_INVALID', `${path}.sourcePortID`, 'sourcePortID must be string or number.'));
    return;
  }

  const sourcePort = String(edge.sourcePortID);
  if (sourceNode.type === StandardNodeType.If && !isValidSelectorPort(sourcePort)) {
    issues.push(
      issue('CONDITION_PORT_INVALID', `${path}.sourcePortID`, `Condition source port is invalid: ${sourcePort}`),
    );
  }

  if (
    sourceNode.type === StandardNodeType.Loop &&
    !['loop-output', 'loop-output-to-function', 'branch_error'].includes(sourcePort)
  ) {
    issues.push(issue('LOOP_PORT_INVALID', `${path}.sourcePortID`, `Loop source port is invalid: ${sourcePort}`));
  }

  if (
    sourceNode.type === StandardNodeType.Batch &&
    !['batch-output', 'batch-output-to-function', 'branch_error'].includes(sourcePort)
  ) {
    issues.push(issue('BATCH_PORT_INVALID', `${path}.sourcePortID`, `Batch source port is invalid: ${sourcePort}`));
  }
}

export function validateWorkflowSchemaInvariants(schema: WorkflowJSON): WorkflowSchemaInvariantResult {
  const issues: WorkflowSchemaInvariantIssue[] = [];
  inspectJsonValue(schema, '$', issues, new WeakSet<object>());

  if (!Array.isArray(schema.nodes)) {
    issues.push(issue('NODES_NOT_ARRAY', '$.nodes', 'Workflow schema nodes must be an array.'));
    return { valid: false, issues };
  }

  if (!Array.isArray(schema.edges)) {
    issues.push(issue('EDGES_NOT_ARRAY', '$.edges', 'Workflow schema edges must be an array.'));
    return { valid: false, issues };
  }

  const nodesById = nodePathById(schema.nodes);
  for (const [index, node] of collectNodes(schema.nodes).entries()) {
    if (typeof node.id !== 'string' || node.id.length === 0) {
      issues.push(issue('NODE_ID_INVALID', `$.nodes[${index}].id`, 'Node id must be a non-empty string.'));
    }

    if (typeof node.type !== 'string' || node.type.length === 0) {
      issues.push(issue('NODE_TYPE_INVALID', `$.nodes[${index}].type`, 'Node type must be a non-empty string.'));
    }
  }

  const allEdges = [
    ...schema.edges.map((edge, index) => ({ edge, path: `$.edges[${index}]` })),
    ...collectNodes(schema.nodes).flatMap(node =>
      (node.edges ?? []).map((edge, index) => ({
        edge,
        path: `$.nodes.${node.id}.edges[${index}]`,
      })),
    ),
  ];

  for (const { edge, path } of allEdges) {
    const source = nodesById.get(edge.sourceNodeID);
    const target = nodesById.get(edge.targetNodeID);

    if (!source) {
      issues.push(issue('EDGE_SOURCE_NODE_MISSING', `${path}.sourceNodeID`, `Source node not found: ${edge.sourceNodeID}`));
      continue;
    }

    if (!target) {
      issues.push(issue('EDGE_TARGET_NODE_MISSING', `${path}.targetNodeID`, `Target node not found: ${edge.targetNodeID}`));
      continue;
    }

    validateEdgePorts(edge, source, path, issues);
  }

  return {
    valid: issues.length === 0,
    issues,
  };
}
