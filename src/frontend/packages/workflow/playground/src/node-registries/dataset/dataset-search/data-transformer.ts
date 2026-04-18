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

import { nanoid } from 'nanoid';
import { isNil, set } from 'lodash-es';
import { BlockInput, ViewVariableType } from '@coze-workflow/base';

/**
 * v5 §38 / 计划 G7：Atlas v5 高级检索设置可选地存放在 datasetParam 的特殊条目里。
 * 这些字段独立于 Coze 的 datasetSetting，避免破坏现有数据。
 */
const ATLAS_V5_PARAM_NAME = 'atlasV5';

export function transformOnInit(value) {
  // New drag-in node initialization
  if (!value) {
    return {
      nodeMeta: undefined,
      inputs: {
        inputParameters: {
          Query: { type: 'ref', content: '' },
        },
        datasetParameters: {
          datasetParam: [],
          datasetSetting: {},
          atlasV5: {},
        },
      },
      outputs: [
        {
          key: nanoid(),
          name: 'outputList',
          type: ViewVariableType.ArrayObject,
          children: [
            {
              key: nanoid(),
              name: 'output',
              type: ViewVariableType.String,
            },
          ],
        },
      ],
    };
  }

  const { inputParameters, datasetParam } = value.inputs;
  const formData = {
    ...value,
    inputs: {
      datasetParameters: {},
    },
  };

  formData.inputs.inputParameters = inputParameters.reduce(
    (map, obj: { name: string | number; input: unknown }) => {
      map[obj.name] = obj.input;
      return map;
    },
    {},
  );
  formData.inputs.datasetParameters.datasetParam = datasetParam[0]?.input.value
    .content as string[];
  // In the case of initial creation/stock data, the top_k and min_score are empty, and the initial default value is processed in the dataset-settings component
  formData.inputs.datasetParameters.datasetSetting = {
    top_k: datasetParam.find(item => item.name === 'topK')?.input.value
      .content as number,

    min_score: datasetParam.find(item => item.name === 'minScore')?.input.value
      .content as number,

    strategy: datasetParam.find(item => item.name === 'strategy')?.input.value
      .content as number,

    use_nl2sql: datasetParam.find(item => item.name === 'useNl2sql')?.input
      .value.content as boolean,
    use_rerank: datasetParam.find(item => item.name === 'useRerank')?.input
      .value.content as boolean,
    use_rewrite: datasetParam.find(item => item.name === 'useRewrite')?.input
      .value.content as boolean,
    is_personal_only: datasetParam.find(item => item.name === 'isPersonalOnly')
      ?.input.value.content as boolean,
  };

  // v5 §38 / 计划 G7：从 datasetParam 还原 atlasV5（若存在）
  const atlasEntry = datasetParam.find(item => item.name === ATLAS_V5_PARAM_NAME);
  if (atlasEntry?.input?.value?.content) {
    try {
      const raw = atlasEntry.input.value.content;
      formData.inputs.datasetParameters.atlasV5 = typeof raw === 'string' ? JSON.parse(raw) : raw;
    } catch {
      formData.inputs.datasetParameters.atlasV5 = {};
    }
  } else {
    formData.inputs.datasetParameters.atlasV5 = {};
  }

  return formData;
}

export function transformOnSubmit(value) {
  const { nodeMeta, inputs, outputs } = value;
  const { inputParameters = { Query: { type: 'ref' } }, datasetParameters } =
    inputs ?? {};
  const { datasetParam, datasetSetting, atlasV5 } = datasetParameters ?? {};
  const actualData = {
    nodeMeta,
    outputs,
    inputs: {
      datasetParam: [] as unknown[],
    },
  };

  set(
    actualData.inputs,
    'inputParameters',
    Object.entries(inputParameters).map(([key, mapValue]) => ({
      name: key,
      input: mapValue,
    })) || [],
  );

  set(actualData.inputs, 'datasetParam', [
    {
      name: 'datasetList',
      input: {
        type: 'list',
        schema: {
          type: 'string',
        },
        value: {
          type: 'literal',
          content: datasetParam || [],
        },
      },
    },
    {
      name: 'topK',
      input: {
        type: 'integer',
        value: {
          type: 'literal',
          content: datasetSetting?.top_k,
        },
      },
    },
    BlockInput.createBoolean('useRerank', datasetSetting?.use_rerank),
    BlockInput.createBoolean('useRewrite', datasetSetting?.use_rewrite),
    BlockInput.createBoolean(
      'isPersonalOnly',
      datasetSetting?.is_personal_only,
    ),
  ]);

  // No fields are passed without a table knowledge base use_nl2sql
  if (!isNil(datasetSetting?.use_nl2sql)) {
    actualData.inputs.datasetParam.push(
      BlockInput.createBoolean('useNl2sql', datasetSetting?.use_nl2sql),
    );
  }

  // Strategy may be fulltext
  if (datasetSetting?.min_score) {
    actualData.inputs.datasetParam.push({
      name: 'minScore',
      input: {
        type: 'float',
        value: {
          type: 'literal',
          content: datasetSetting?.min_score,
        },
      },
    });
  }

  // Added search policy configuration, there may be no strategy data not in grey release
  // Strategy may be 0
  if (!isNil(datasetSetting?.strategy)) {
    actualData.inputs.datasetParam.push({
      name: 'strategy',
      input: {
        type: 'integer',
        value: {
          type: 'literal',
          content: datasetSetting?.strategy,
        },
      },
    });
  }

  // v5 §38 / 计划 G7：把 atlasV5 设置序列化为 string，存为 datasetParam 的 atlasV5 条目。
  // 后端 KnowledgeRetrieverNodeExecutor 会按 retrievalProfile / filters / callerContextOverride / debug 字段读取。
  const hasAtlasV5 = atlasV5 && (
    atlasV5.retrievalProfile ||
    atlasV5.filters ||
    atlasV5.callerContextOverride ||
    !isNil(atlasV5.debug)
  );
  if (hasAtlasV5) {
    actualData.inputs.datasetParam.push({
      name: 'atlasV5',
      input: {
        type: 'string',
        value: {
          type: 'literal',
          content: JSON.stringify(atlasV5),
        },
      },
    });
  }

  return actualData;
}
