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

import {
  type DragNodesOperationValue,
  FreeOperationType,
  WorkflowDocument,
} from '@flowgram-adapter/free-layout-editor';
import { type PluginContext } from '@flowgram-adapter/free-layout-editor';
import { type OperationMeta } from '@flowgram-adapter/free-layout-editor';

export const moveNodeOperationMeta: OperationMeta<
  DragNodesOperationValue,
  PluginContext,
  void
> = {
  type: FreeOperationType.dragNodes,
  inverse: op => ({
    ...op,
    value: {
      nodes: op.value.nodes.map(node => ({
        ...node,
        position: node.prevPosition,
      })),
    },
  }),
  apply: (operation, ctx: PluginContext) => {
    const document = ctx.get<WorkflowDocument>(WorkflowDocument);

    operation.value.nodes.forEach(nodeInfo => {
      const node = document.getNode(nodeInfo.id);
      if (node) {
        const { position } = document.layout.getLayout(node);
        const targetPosition = {
          x: nodeInfo.position.x,
          y: nodeInfo.position.y,
        };
        document.layout.transform(node, {
          position: {
            x: targetPosition.x - position.x,
            y: targetPosition.y - position.y,
          },
        });
      }
    });
  },
};
