#!/usr/bin/env node

import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const T = {
  Start: '1',
  End: '2',
  LLM: '3',
  Api: '4',
  Code: '5',
  Dataset: '6',
  If: '8',
  SubWorkflow: '9',
  Database: '12',
  Output: '13',
  Question: '18',
  SetVariable: '20',
  Loop: '21',
  Intent: '22',
  DatasetWrite: '27',
  Batch: '28',
  Input: '30',
  VariableMerge: '32',
  VariableAssign: '40',
  DatabaseQuery: '43',
  Http: '45',
  CreateMessage: '55'
};

function pos(index) {
  return {
    x: (index % 10) * 320,
    y: Math.floor(index / 10) * 180
  };
}

function nodeMeta(title, type) {
  return {
    title,
    description: `${title} fixture node`,
    icon: '',
    subTitle: `type ${type}`,
    mainColor: '#4D53E8'
  };
}

function literal(content, type = 'string') {
  return {
    type: 'literal',
    content,
    rawMeta: { type }
  };
}

function ref(blockID, name) {
  return {
    type: 'ref',
    content: {
      source: 'block-output',
      blockID,
      name
    }
  };
}

function variable(name, type = 'String', extra = {}) {
  return {
    name,
    type,
    required: false,
    ...extra
  };
}

function blockInput(name, input) {
  return { name, input };
}

function baseData(id, type, title) {
  return {
    nodeMeta: nodeMeta(title, type),
    inputs: {},
    outputs: [variable('result')]
  };
}

function makeNode(id, type, index, title = `${id}`) {
  const data = baseData(id, type, title);

  switch (type) {
    case T.Start:
      data.outputs = [variable('query'), variable('user_id')];
      data.inputs = { auto_save_history: false };
      break;
    case T.End:
      data.inputs = {
        terminatePlan: 'returnVariables',
        outputEmitter: {
          streamingOutput: false,
          content: { value: `{{${id}.answer}}` }
        },
        inputParameters: [blockInput('answer', ref(previousId(id), 'result'))]
      };
      data.outputs = [];
      break;
    case T.LLM:
      data.inputs = {
        llmParam: {
          prompt: 'Summarize upstream result.',
          model: 'fixture-model'
        },
        inputParameters: [blockInput('query', ref('start', 'query'))]
      };
      data.outputs = [variable('answer')];
      break;
    case T.Code:
      data.inputs = {
        code: 'async function main(params) { return { result: params.input }; }',
        inputParameters: [blockInput('input', ref(previousId(id), 'result'))]
      };
      data.outputs = [variable('result')];
      break;
    case T.Http:
      data.inputs = {
        method: 'POST',
        url: 'https://example.com/api/fixture',
        headers: [],
        body: { type: 'json', value: '{"ok":true}' },
        inputParameters: [blockInput('payload', ref(previousId(id), 'result'))]
      };
      data.outputs = [variable('status', 'Integer'), variable('body')];
      break;
    case T.Output:
      data.inputs = {
        content: literal('Fixture output'),
        streamingOutput: false,
        inputParameters: [blockInput('content', ref(previousId(id), 'result'))]
      };
      data.outputs = [variable('message')];
      break;
    case T.Api:
      data.inputs = {
        pluginFrom: 'library',
        apiParam: [
          blockInput('pluginID', literal('9223372036854770001')),
          blockInput('apiID', literal('9223372036854770002')),
          blockInput('pluginVersion', literal('v1'))
        ],
        inputParameters: [blockInput('query', ref('start', 'query'))]
      };
      data.outputs = [variable('plugin_result')];
      break;
    case T.Dataset:
      data.inputs = {
        datasetParam: [
          blockInput('datasetList', literal(['9223372036854770101'], 'array')),
          blockInput('topK', literal(5, 'integer')),
          blockInput('minScore', literal(0.3, 'number'))
        ],
        inputParameters: [blockInput('query', ref('start', 'query'))]
      };
      data.outputs = [variable('documents', 'Array')];
      break;
    case T.Database:
      data.inputs = {
        databaseInfoList: [{ databaseInfoID: '9223372036854770201', name: 'fixture_db' }],
        sql: 'select * from fixture_table where owner = {{user_id}}',
        inputParameters: [blockInput('user_id', ref('start', 'user_id'))]
      };
      data.outputs = [variable('rows', 'Array')];
      break;
    case T.DatabaseQuery:
      data.inputs = {
        databaseInfoList: [{ databaseInfoID: '9223372036854770201', name: 'fixture_db' }],
        selectParam: { limit: 20, conditionList: [], orderByList: [], fieldList: [] },
        inputParameters: [blockInput('user_id', ref('start', 'user_id'))]
      };
      data.outputs = [variable('rows', 'Array')];
      break;
    case T.SubWorkflow:
      data.inputs = {
        workflowId: '9223372036854775807',
        workflowVersion: 'draft',
        inputDefs: [variable('query')],
        inputParameters: [blockInput('query', ref('start', 'query'))],
        batch: { enable: false }
      };
      data.outputs = [variable('sub_result')];
      break;
    case T.If:
      data.inputs = {
        branches: [0, 1, 2, 3, 4].map(i => ({
          name: `branch_${i}`,
          condition: {
            logic: 2,
            conditions: [
              {
                operator: 1,
                left: ref('start', 'query'),
                right: literal(`case-${i}`)
              }
            ]
          }
        }))
      };
      data.outputs = [];
      break;
    case T.Loop:
      data.inputs = {
        loopArray: [blockInput('items', ref('start', 'query'))],
        loopVariables: [variable('item'), variable('index', 'Integer')],
        breakCondition: literal(false, 'boolean')
      };
      data.outputs = [variable('loop_result', 'Array')];
      break;
    case T.Batch:
      data.inputs = {
        batch: {
          inputArray: ref('start', 'query'),
          itemVariable: 'item',
          indexVariable: 'index',
          concurrency: 5
        },
        inputParameters: [blockInput('items', ref('start', 'query'))]
      };
      data.outputs = [variable('batch_result', 'Array')];
      break;
    case T.VariableAssign:
      data.inputs = {
        inputParameters: [
          {
            name: 'assign_result',
            left: ref('start', 'query'),
            input: ref(previousId(id), 'result')
          }
        ]
      };
      data.outputs = [variable('assigned')];
      break;
    case T.VariableMerge:
      data.inputs = {
        mergeGroups: [
          {
            name: 'merged',
            variables: [ref('start', 'query'), ref(previousId(id), 'result')]
          }
        ]
      };
      data.outputs = [variable('merged', 'Object')];
      break;
    case T.Intent:
      data.inputs = {
        intents: [
          { name: 'create_ticket', description: 'Create a security ticket.' },
          { name: 'query_asset', description: 'Query an asset.' }
        ],
        inputParameters: [blockInput('utterance', ref('start', 'query'))]
      };
      data.outputs = [variable('intent')];
      break;
    case T.Question:
      data.inputs = {
        answer_type: 'text',
        options: [],
        inputParameters: [blockInput('question', ref('start', 'query'))]
      };
      data.outputs = [variable('answer')];
      break;
    case T.DatasetWrite:
      data.inputs = {
        datasetParam: [blockInput('datasetID', literal('9223372036854770102'))],
        inputParameters: [blockInput('document', ref(previousId(id), 'result'))]
      };
      data.outputs = [variable('written', 'Boolean')];
      break;
    case T.CreateMessage:
      data.inputs = {
        conversationID: literal('9223372036854770301'),
        content: literal('fixture message'),
        inputParameters: [blockInput('content', ref(previousId(id), 'result'))]
      };
      data.outputs = [variable('message_id')];
      break;
    default:
      data.inputs = { inputParameters: [blockInput('input', ref('start', 'query'))] };
  }

  return {
    id,
    type,
    meta: {
      position: pos(index)
    },
    data
  };
}

function previousId(id) {
  const match = /(\d+)$/.exec(id);
  if (!match) {
    return 'start';
  }

  const previous = Number(match[1]) - 1;
  return previous <= 0 ? 'start' : `n${previous}`;
}

function edge(sourceNodeID, targetNodeID, sourcePortID, targetPortID) {
  return {
    sourceNodeID,
    targetNodeID,
    ...(sourcePortID ? { sourcePortID } : {}),
    ...(targetPortID ? { targetPortID } : {})
  };
}

function chain(types, namePrefix = 'n') {
  const nodes = [makeNode('start', T.Start, 0, 'Start')];
  const edges = [];
  let previous = 'start';

  types.forEach((type, index) => {
    const id = `${namePrefix}${index + 1}`;
    nodes.push(makeNode(id, type, index + 1, `${namePrefix}-${index + 1}`));
    edges.push(edge(previous, id));
    previous = id;
  });

  nodes.push(makeNode('end', T.End, nodes.length, 'End'));
  edges.push(edge(previous, 'end'));

  return { nodes, edges };
}

function longChain30() {
  const repeated = [T.LLM, T.Code, T.Http, T.Output];
  const types = Array.from({ length: 28 }, (_, i) => repeated[i % repeated.length]);
  return chain(types);
}

function largeGraph100() {
  const palette = [
    T.LLM,
    T.Code,
    T.Http,
    T.Output,
    T.Api,
    T.Dataset,
    T.DatabaseQuery,
    T.VariableMerge,
    T.VariableAssign,
    T.Intent,
    T.Question,
    T.CreateMessage
  ];
  const graph = chain(Array.from({ length: 98 }, (_, i) => palette[i % palette.length]));

  for (let i = 1; i <= 22; i += 1) {
    graph.edges.push(edge(`n${i}`, `n${i + 10}`));
  }

  return graph;
}

function conditionBranches() {
  const nodes = [
    makeNode('start', T.Start, 0, 'Start'),
    makeNode('condition', T.If, 1, 'Condition'),
    ...[T.LLM, T.Code, T.Http, T.Api, T.Output].map((type, i) =>
      makeNode(`branch_${i}`, type, i + 2, `Branch ${i}`),
    ),
    makeNode('else_node', T.Code, 8, 'Else'),
    makeNode('end', T.End, 9, 'End')
  ];
  const edges = [edge('start', 'condition')];
  ['true', 'true_1', 'true_2', 'true_3', 'true_4'].forEach((port, i) => {
    edges.push(edge('condition', `branch_${i}`, port));
    edges.push(edge(`branch_${i}`, 'end'));
  });
  edges.push(edge('condition', 'else_node', 'false'));
  edges.push(edge('else_node', 'end'));
  return { nodes, edges };
}

function loopNested() {
  const loop = makeNode('loop', T.Loop, 1, 'Loop');
  loop.blocks = [
    makeNode('loop_code', T.Code, 0, 'Loop Code'),
    makeNode('loop_llm', T.LLM, 1, 'Loop LLM'),
    makeNode('loop_assign', T.VariableAssign, 2, 'Loop Assign')
  ];
  loop.edges = [
    edge('loop_code', 'loop_llm'),
    edge('loop_llm', 'loop_assign')
  ];
  return {
    nodes: [
      makeNode('start', T.Start, 0, 'Start'),
      loop,
      makeNode('after_loop', T.Code, 2, 'After Loop'),
      makeNode('end', T.End, 3, 'End')
    ],
    edges: [
      edge('start', 'loop'),
      edge('loop', 'after_loop', 'loop-output'),
      edge('after_loop', 'end')
    ]
  };
}

function batchProcessing() {
  const batch = makeNode('batch', T.Batch, 1, 'Batch');
  batch.blocks = [
    makeNode('batch_http', T.Http, 0, 'Batch HTTP'),
    makeNode('batch_code', T.Code, 1, 'Batch Code'),
    makeNode('batch_merge', T.VariableMerge, 2, 'Batch Merge')
  ];
  batch.edges = [
    edge('batch_http', 'batch_code'),
    edge('batch_code', 'batch_merge')
  ];
  return {
    nodes: [
      makeNode('start', T.Start, 0, 'Start'),
      batch,
      makeNode('end', T.End, 2, 'End')
    ],
    edges: [
      edge('start', 'batch'),
      edge('batch', 'end', 'batch-output')
    ]
  };
}

function subWorkflowBigId() {
  return {
    nodes: [
      makeNode('start', T.Start, 0, 'Start'),
      makeNode('sub_a', T.SubWorkflow, 1, 'Sub Workflow A'),
      makeNode('sub_b', T.SubWorkflow, 2, 'Sub Workflow B'),
      makeNode('end', T.End, 3, 'End')
    ],
    edges: [edge('start', 'sub_a'), edge('sub_a', 'sub_b'), edge('sub_b', 'end')]
  };
}

function externalResourcesMixed() {
  return {
    nodes: [
      makeNode('start', T.Start, 0, 'Start'),
      makeNode('plugin', T.Api, 1, 'Plugin'),
      makeNode('database', T.DatabaseQuery, 2, 'Database Query'),
      makeNode('knowledge', T.Dataset, 3, 'Knowledge'),
      makeNode('http', T.Http, 4, 'HTTP'),
      makeNode('message', T.CreateMessage, 5, 'Message'),
      makeNode('end', T.End, 6, 'End')
    ],
    edges: [
      edge('start', 'plugin'),
      edge('plugin', 'database'),
      edge('database', 'knowledge'),
      edge('knowledge', 'http'),
      edge('http', 'message'),
      edge('message', 'end')
    ]
  };
}

function variableHeavy() {
  const graph = chain(
    Array.from({ length: 48 }, (_, i) =>
      [T.Code, T.VariableAssign, T.VariableMerge, T.If][i % 4],
    ),
    'v',
  );
  const start = graph.nodes.find(item => item.id === 'start');
  start.data.outputs = Array.from({ length: 50 }, (_, i) => variable(`var_${i}`));
  return graph;
}

const fixtures = {
  'long-chain-30.json': longChain30(),
  'large-graph-100.json': largeGraph100(),
  'condition-branches.json': conditionBranches(),
  'loop-nested.json': loopNested(),
  'batch-processing.json': batchProcessing(),
  'sub-workflow-big-id.json': subWorkflowBigId(),
  'external-resources-mixed.json': externalResourcesMixed(),
  'variable-heavy.json': variableHeavy()
};

await mkdir(__dirname, { recursive: true });
for (const [fileName, fixture] of Object.entries(fixtures)) {
  await writeFile(path.join(__dirname, fileName), `${JSON.stringify(fixture, null, 2)}\n`);
}

console.log(`Generated ${Object.keys(fixtures).length} workflow fixtures in ${__dirname}`);
