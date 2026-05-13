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
  DEFAULT_NODE_META_PATH,
  type WorkflowNodeRegistry,
} from '@coze-workflow/nodes';
import { StandardNodeType, type WorkflowNodeJSON } from '@coze-workflow/base';

import { type NodeTestMeta } from '@/test-run-kit';
import { type WorkflowPlaygroundContext } from '@/workflow-playground-context';
import { WorkflowModelsService } from '@/services';

import { test } from './node-test';
import { LLM_FORM_META } from './llm-form-meta';

export const LLM_NODE_REGISTRY: WorkflowNodeRegistry<NodeTestMeta> = {
  type: StandardNodeType.LLM,
  meta: {
    nodeDTOType: StandardNodeType.LLM,
    style: {
      width: 360,
    },
    size: { width: 360, height: 130.7 },
    test,
    nodeMetaPath: DEFAULT_NODE_META_PATH,
    batchPath: '/batch',
    inputParametersPath: '/$$input_decorator$$/inputParameters',
    getLLMModelIdsByNodeJSON: nodeJSON =>
      nodeJSON.data.inputs.llmParam.find(p => p.name === 'modelType')?.input
        .value.content,
    helpLink: '/open/docs/guides/llm_node',
  },
  formMeta: LLM_FORM_META,

  onInit: async (nodeJson: WorkflowNodeJSON, context: WorkflowPlaygroundContext) => {
    if (!nodeJson) {
      return;
    }

    const modelIds = nodeJson.data?.inputs?.llmParam?.find(p => p.name === 'modelType')?.input?.value?.content;
    if (modelIds?.length) {
      await context.entityManager.getService(WorkflowModelsService)?.loadModels?.(modelIds);
    }
  },

  checkError: (nodeJson: WorkflowNodeJSON, context: WorkflowPlaygroundContext) => {
    if (!nodeJson) {
      return undefined;
    }

    const llmParam = nodeJson.data?.inputs?.llmParam;
    if (!llmParam?.length) {
      return undefined;
    }

    const modelType = llmParam.find(p => p.name === 'modelType');
    if (!modelType?.input?.value?.content?.length) {
      return undefined;
    }

    return undefined;
  },

  onDispose: (nodeJson: WorkflowNodeJSON, context: WorkflowPlaygroundContext) => {
    if (!nodeJson) {
      return;
    }
  },
};
