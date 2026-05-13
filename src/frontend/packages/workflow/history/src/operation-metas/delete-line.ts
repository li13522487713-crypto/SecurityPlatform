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
  type AddOrDeleteLineOperationValue,
  FreeOperationType,
  WorkflowDocument,
  WorkflowLinesManager,
} from '@flowgram-adapter/free-layout-editor';
import { type PluginContext } from '@flowgram-adapter/free-layout-editor';
import { type OperationMeta } from '@flowgram-adapter/free-layout-editor';

import { shouldMerge } from '../utils/should-merge';

export const deleteLineOperationMeta: OperationMeta<
  AddOrDeleteLineOperationValue,
  PluginContext,
  void
> = {
  type: FreeOperationType.deleteLine,
  inverse: op => ({
    ...op,
    type: FreeOperationType.addLine,
  }),
  apply: (operation, ctx: PluginContext) => {
    const linesManager = ctx.get<WorkflowLinesManager>(WorkflowLinesManager);
    const document = ctx.get<WorkflowDocument>(WorkflowDocument);
    const fromNode = document.getNode(operation.value.from);

    if (!fromNode) {
      return;
    }

    const lines = linesManager.getAllLines();
    const targetLine = lines.find(
      line =>
        line.from.id === operation.value.from &&
        line.to.id === operation.value.to,
    );

    if (targetLine) {
      linesManager.deleteLine(targetLine);
    }
  },
  shouldMerge,
};
