using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services.AiPlatform;

namespace Atlas.SecurityPlatform.Tests.Workflows;

public sealed class CozeWorkflowPlanCompilerTests
{
    private readonly CozeWorkflowPlanCompiler _compiler = new();

    [Fact]
    public void Compile_ShouldAcceptCozeNativeCanvas()
    {
        const string json = """
            {
              "nodes": [
                {
                  "id": "entry_1",
                  "type": 1,
                  "meta": { "position": { "x": 120, "y": 80 } },
                  "data": {
                    "nodeMeta": { "title": "开始" },
                    "outputs": [{ "name": "incident", "type": "string", "required": true }]
                  }
                },
                {
                  "id": "code_1",
                  "type": 5,
                  "meta": { "position": { "x": 360, "y": 80 } },
                  "data": {
                    "nodeMeta": { "title": "代码执行" },
                    "inputs": {
                      "language": "javascript",
                      "code": "function main(args){ return { result: args.params.incident }; }",
                      "inputParameters": [{ "name": "incident" }]
                    },
                    "outputs": [{ "name": "result", "type": "string" }]
                  }
                },
                {
                  "id": "exit_1",
                  "type": 2,
                  "meta": { "position": { "x": 660, "y": 80 } },
                  "data": {
                    "nodeMeta": { "title": "结束" },
                    "inputs": {
                      "terminatePlan": "returnVariables",
                      "outputEmitter": {
                        "streamingOutput": false,
                        "content": { "value": "{{code_1.result}}" }
                      },
                      "inputParameters": [
                        {
                          "name": "result",
                          "input": { "value": { "content": "{{code_1.result}}" } }
                        }
                      ]
                    }
                  }
                }
              ],
              "edges": [
                { "sourceNodeID": "entry_1", "targetNodeID": "code_1" },
                { "source": "code_1", "target": "exit_1" }
              ]
            }
            """;

        var result = _compiler.Compile(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Canvas);
        Assert.Equal(3, result.Canvas!.Nodes.Count);
        Assert.Equal(WorkflowNodeType.Entry, result.Canvas.Nodes[0].Type);
        Assert.Equal("incident", result.Canvas.Nodes[0].Config["entryVariable"].GetString());
        Assert.Equal(WorkflowNodeType.CodeRunner, result.Canvas.Nodes[1].Type);
        Assert.Equal("result", result.Canvas.Nodes[1].Config["outputKey"].GetString());
        Assert.Equal("{{code_1.result}}", result.Canvas.Nodes[2].Config["exitTemplate"].GetString());
    }

    [Fact]
    public void Compile_ShouldRejectNodeWithoutId()
    {
        const string json = """
            {
              "nodes": [
                {
                  "type": 1,
                  "meta": { "position": { "x": 0, "y": 0 } },
                  "data": { "nodeMeta": { "title": "开始" } }
                }
              ],
              "edges": []
            }
            """;

        var result = _compiler.Compile(json);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == "COZE_NODE_ID_MISSING");
    }

    [Fact]
    public void Compile_ShouldRejectUnknownNodeType()
    {
        const string json = """
            {
              "nodes": [
                {
                  "id": "entry_1",
                  "type": "NoSuchType",
                  "meta": { "position": { "x": 0, "y": 0 } },
                  "data": { "nodeMeta": { "title": "开始" } }
                }
              ],
              "edges": []
            }
            """;

        var result = _compiler.Compile(json);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == "COZE_NODE_TYPE_INVALID");
    }

    [Theory]
    [InlineData("12", WorkflowNodeType.DatabaseCustomSql)]
    [InlineData("14", WorkflowNodeType.Imageflow)]
    [InlineData("16", WorkflowNodeType.ImageGenerate)]
    [InlineData("17", WorkflowNodeType.ImageReference)]
    [InlineData("23", WorkflowNodeType.ImageCanvas)]
    public void Compile_ShouldResolveCozeTypeIdsWithoutSemanticDrift(string cozeTypeId, WorkflowNodeType expectedType)
    {
        var json = $$"""
            {
              "nodes": [
                {
                  "id": "entry_1",
                  "type": 1,
                  "meta": { "position": { "x": 120, "y": 80 } },
                  "data": { "nodeMeta": { "title": "开始" }, "outputs": [{ "name": "input", "type": "string" }] }
                },
                {
                  "id": "target_1",
                  "type": "{{cozeTypeId}}",
                  "meta": { "position": { "x": 360, "y": 80 } },
                  "data": {
                    "nodeMeta": { "title": "目标节点" },
                    "inputs": {
                      "databaseInfoList": [{ "databaseInfoID": "9223372036854770201" }],
                      "sql": "select 1",
                      "inputParameters": [{ "name": "input" }]
                    },
                    "outputs": [{ "name": "result", "type": "string" }]
                  }
                }
              ],
              "edges": [
                { "sourceNodeID": "entry_1", "targetNodeID": "target_1" }
              ]
            }
            """;

        var result = _compiler.Compile(json);

        Assert.True(result.IsSuccess);
        var targetNode = Assert.Single(result.Canvas!.Nodes, node => node.Key == "target_1");
        Assert.Equal(expectedType, targetNode.Type);
    }

    [Fact]
    public void Compile_ShouldAdaptP0CozeNodeConfigs()
    {
        const string json = """
            {
              "nodes": [
                { "id": "entry_1", "type": 1, "meta": { "position": { "x": 0, "y": 0 } }, "data": { "nodeMeta": { "title": "开始" }, "outputs": [{ "name": "query", "type": "string" }] } },
                { "id": "plugin_1", "type": 4, "meta": { "position": { "x": 1, "y": 0 } }, "data": { "nodeMeta": { "title": "插件" }, "inputs": { "pluginFrom": "library", "apiParam": [
                  { "name": "pluginID", "input": { "value": { "type": "literal", "content": "9223372036854770001" } } },
                  { "name": "apiID", "input": { "value": { "type": "literal", "content": "9223372036854770002" } } },
                  { "name": "pluginVersion", "input": { "value": { "type": "literal", "content": "v1" } } }
                ] }, "outputs": [{ "name": "plugin_result", "type": "string" }] } },
                { "id": "knowledge_1", "type": 6, "meta": { "position": { "x": 2, "y": 0 } }, "data": { "nodeMeta": { "title": "知识" }, "inputs": { "datasetParam": [
                  { "name": "datasetList", "input": { "value": { "type": "literal", "content": ["9223372036854770101"] } } },
                  { "name": "topK", "input": { "value": { "type": "literal", "content": 5 } } }
                ] }, "outputs": [{ "name": "documents", "type": "array" }] } },
                { "id": "sub_1", "type": 9, "meta": { "position": { "x": 3, "y": 0 } }, "data": { "nodeMeta": { "title": "子工作流" }, "inputs": { "workflowId": "9223372036854775807", "workflowVersion": "draft", "inputDefs": [{ "name": "query" }], "batch": { "enable": false } }, "outputs": [{ "name": "sub_result", "type": "string" }] } },
                { "id": "output_1", "type": 13, "meta": { "position": { "x": 4, "y": 0 } }, "data": { "nodeMeta": { "title": "输出" }, "inputs": { "content": { "type": "literal", "content": "hello" }, "streamingOutput": false }, "outputs": [{ "name": "message", "type": "string" }] } },
                { "id": "question_1", "type": 18, "meta": { "position": { "x": 5, "y": 0 } }, "data": { "nodeMeta": { "title": "问题" }, "inputs": { "answer_type": "text", "options": [] }, "outputs": [{ "name": "answer", "type": "string" }] } },
                { "id": "intent_1", "type": 22, "meta": { "position": { "x": 6, "y": 0 } }, "data": { "nodeMeta": { "title": "意图" }, "inputs": { "intents": [{ "name": "query_asset", "description": "Query asset" }] }, "outputs": [{ "name": "intent", "type": "string" }] } },
                { "id": "batch_1", "type": 28, "meta": { "position": { "x": 7, "y": 0 } }, "data": { "nodeMeta": { "title": "批处理" }, "inputs": { "batch": { "concurrency": 5, "itemVariable": "item" } }, "outputs": [{ "name": "batch_result", "type": "array" }] } },
                { "id": "input_1", "type": 30, "meta": { "position": { "x": 8, "y": 0 } }, "data": { "nodeMeta": { "title": "输入" }, "inputs": { "outputSchema": "{\"type\":\"object\"}" }, "outputs": [{ "name": "query", "type": "string" }] } }
              ],
              "edges": [
                { "sourceNodeID": "entry_1", "targetNodeID": "plugin_1" },
                { "sourceNodeID": "plugin_1", "targetNodeID": "knowledge_1" },
                { "sourceNodeID": "knowledge_1", "targetNodeID": "sub_1" },
                { "sourceNodeID": "sub_1", "targetNodeID": "output_1" },
                { "sourceNodeID": "output_1", "targetNodeID": "question_1" },
                { "sourceNodeID": "question_1", "targetNodeID": "intent_1" },
                { "sourceNodeID": "intent_1", "targetNodeID": "batch_1" },
                { "sourceNodeID": "batch_1", "targetNodeID": "input_1", "sourcePortID": "batch-output" }
              ]
            }
            """;

        var result = _compiler.Compile(json);

        Assert.True(result.IsSuccess);
        var nodes = result.Canvas!.Nodes.ToDictionary(node => node.Key);
        Assert.Equal("9223372036854770001", nodes["plugin_1"].Config["plugin_id"].GetString());
        Assert.Equal("library", nodes["plugin_1"].Config["plugin_from"].GetString());
        Assert.True(nodes["knowledge_1"].Config.ContainsKey("knowledgeIds"));
        Assert.Equal("9223372036854775807", nodes["sub_1"].Config["workflow_id"].GetString());
        Assert.False(nodes["output_1"].Config["streamingOutput"].GetBoolean());
        Assert.Equal("text", nodes["question_1"].Config["answer_type"].GetString());
        Assert.True(nodes["intent_1"].Config.ContainsKey("intents"));
        Assert.True(nodes["batch_1"].Config.ContainsKey("batch"));
        Assert.Equal("{\"type\":\"object\"}", nodes["input_1"].Config["outputSchema"].GetString());
    }

    [Fact]
    public void Compile_ShouldAdaptDatabaseAndVariableNodeConfigs()
    {
        const string json = """
            {
              "nodes": [
                { "id": "entry_1", "type": 1, "meta": { "position": { "x": 0, "y": 0 } }, "data": { "nodeMeta": { "title": "开始" }, "outputs": [{ "name": "input", "type": "string" }] } },
                { "id": "db_sql", "type": 12, "meta": { "position": { "x": 1, "y": 0 } }, "data": { "nodeMeta": { "title": "SQL" }, "inputs": { "databaseInfoList": [{ "databaseInfoID": "9223372036854770201" }], "sql": "select 1" }, "outputs": [{ "name": "rows", "type": "array" }] } },
                { "id": "db_query", "type": 43, "meta": { "position": { "x": 2, "y": 0 } }, "data": { "nodeMeta": { "title": "查询" }, "inputs": { "databaseInfoList": [{ "databaseInfoID": "9223372036854770202" }], "selectParam": { "limit": 20 } }, "outputs": [{ "name": "rows", "type": "array" }] } },
                { "id": "var_merge", "type": 32, "meta": { "position": { "x": 3, "y": 0 } }, "data": { "nodeMeta": { "title": "变量聚合" }, "inputs": { "mergeGroups": [{ "name": "merged", "variables": [] }] }, "outputs": [{ "name": "merged", "type": "object" }] } },
                { "id": "var_assign", "type": 40, "meta": { "position": { "x": 4, "y": 0 } }, "data": { "nodeMeta": { "title": "变量赋值" }, "inputs": { "inputParameters": [{ "name": "target", "left": { "type": "ref" }, "input": { "type": "literal" } }] }, "outputs": [{ "name": "assigned", "type": "string" }] } }
              ],
              "edges": [
                { "sourceNodeID": "entry_1", "targetNodeID": "db_sql" },
                { "sourceNodeID": "db_sql", "targetNodeID": "db_query" },
                { "sourceNodeID": "db_query", "targetNodeID": "var_merge" },
                { "sourceNodeID": "var_merge", "targetNodeID": "var_assign" }
              ]
            }
            """;

        var result = _compiler.Compile(json);

        Assert.True(result.IsSuccess);
        var nodes = result.Canvas!.Nodes.ToDictionary(node => node.Key);
        Assert.Equal("9223372036854770201", nodes["db_sql"].Config["databaseInfoId"].GetString());
        Assert.Equal("select 1", nodes["db_sql"].Config["sqlTemplate"].GetString());
        Assert.Equal("9223372036854770202", nodes["db_query"].Config["databaseInfoId"].GetString());
        Assert.True(nodes["db_query"].Config.ContainsKey("select_param"));
        Assert.True(nodes["var_merge"].Config.ContainsKey("merge_groups"));
        Assert.True(nodes["var_assign"].Config.ContainsKey("variable_assign_pairs"));
    }

    [Theory]
    [InlineData("19", WorkflowNodeType.Break)]
    [InlineData("27", WorkflowNodeType.KnowledgeIndexer)]
    [InlineData("29", WorkflowNodeType.Continue)]
    [InlineData("34", WorkflowNodeType.TriggerUpsert)]
    [InlineData("37", WorkflowNodeType.MessageList)]
    [InlineData("39", WorkflowNodeType.CreateConversation)]
    [InlineData("51", WorkflowNodeType.ConversationUpdate)]
    [InlineData("55", WorkflowNodeType.CreateMessage)]
    [InlineData("58", WorkflowNodeType.JsonSerialization)]
    [InlineData("59", WorkflowNodeType.JsonDeserialization)]
    public void Compile_ShouldPassThroughRemainingCozeNodeInputsAndOutputs(string cozeTypeId, WorkflowNodeType expectedType)
    {
        var json = $$"""
            {
              "nodes": [
                { "id": "entry_1", "type": 1, "meta": { "position": { "x": 0, "y": 0 } }, "data": { "nodeMeta": { "title": "开始" }, "outputs": [{ "name": "input", "type": "string" }] } },
                { "id": "target_1", "type": "{{cozeTypeId}}", "meta": { "position": { "x": 1, "y": 0 } }, "data": { "nodeMeta": { "title": "目标" }, "inputs": { "fixtureField": "fixture-value" }, "outputs": [{ "name": "result", "type": "string" }] } }
              ],
              "edges": [
                { "sourceNodeID": "entry_1", "targetNodeID": "target_1" }
              ]
            }
            """;

        var result = _compiler.Compile(json);

        Assert.True(result.IsSuccess);
        var target = Assert.Single(result.Canvas!.Nodes, node => node.Key == "target_1");
        Assert.Equal(expectedType, target.Type);
        Assert.Equal("fixture-value", target.Config["fixtureField"].GetString());
        Assert.True(target.Config.ContainsKey("outputs"));
    }

    [Fact]
    public void Compile_ResultCanvas_ShouldBeAcceptedByAtlasValidator()
    {
        const string json = """
            {
              "nodes": [
                {
                  "id": "entry_1",
                  "type": 1,
                  "meta": { "position": { "x": 120, "y": 80 } },
                  "data": {
                    "nodeMeta": { "title": "开始" },
                    "outputs": [{ "name": "incident", "type": "string", "required": true }]
                  }
                },
                {
                  "id": "exit_1",
                  "type": 2,
                  "meta": { "position": { "x": 360, "y": 80 } },
                  "data": {
                    "nodeMeta": { "title": "结束" },
                    "inputs": {
                      "terminatePlan": "returnVariables",
                      "inputParameters": [
                        {
                          "name": "incident",
                          "input": { "value": { "content": "{{incident}}" } }
                        }
                      ]
                    }
                  }
                }
              ],
              "edges": [
                { "sourceNodeID": "entry_1", "targetNodeID": "exit_1" }
              ]
            }
            """;

        var compileResult = _compiler.Compile(json);
        Assert.True(compileResult.IsSuccess);
        var serialized = JsonSerializer.Serialize(compileResult.Canvas);
        var validator = new Atlas.Infrastructure.Services.WorkflowEngine.CanvasValidator();
        var validationResult = validator.ValidateCanvas(serialized);
        Assert.True(validationResult.IsValid);
    }

    [Fact]
    public void Compile_ShouldMergeAtlasRuntimeConfigExtension()
    {
        const string json = """
            {
              "nodes": [
                {
                  "id": "entry_1",
                  "type": 1,
                  "meta": { "position": { "x": 120, "y": 80 } },
                  "data": {
                    "nodeMeta": { "title": "开始" },
                    "outputs": [{ "key": "conversationName", "name": "conversationName", "type": "string", "required": true }]
                  }
                },
                {
                  "id": "conversation_1",
                  "type": 39,
                  "meta": { "position": { "x": 360, "y": 80 } },
                  "data": {
                    "nodeMeta": { "title": "创建会话" },
                    "inputs": {
                      "inputParameters": [
                        { "name": "conversationName", "input": { "type": "string", "value": { "type": "ref", "content": { "source": "block-output", "blockID": "entry_1", "name": "conversationName" } } } }
                      ]
                    },
                    "atlasRuntimeConfig": {
                      "userId": 1001,
                      "agentId": 2002,
                      "title": "{{conversationName}}"
                    },
                    "outputs": [{ "key": "conversation_id", "name": "conversation_id", "type": "string" }]
                  }
                }
              ],
              "edges": [
                { "sourceNodeID": "entry_1", "targetNodeID": "conversation_1" }
              ]
            }
            """;

        var result = _compiler.Compile(json);

        Assert.True(result.IsSuccess);
        var node = Assert.Single(result.Canvas!.Nodes, x => x.Key == "conversation_1");
        Assert.Equal(WorkflowNodeType.CreateConversation, node.Type);
        Assert.Equal(1001, node.Config["userId"].GetInt64());
        Assert.Equal(2002, node.Config["agentId"].GetInt64());
        Assert.Equal("{{conversationName}}", node.Config["title"].GetString());
    }
}
