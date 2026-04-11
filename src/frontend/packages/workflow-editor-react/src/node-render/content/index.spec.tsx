import { describe, expect, it } from "vitest";
import { AgentContent } from "./agent-content";
import { NodeContentMap } from "./index";
import { CommonContent } from "./common-content";
import { CommentContent } from "./comment-content";
import { ConversationContent } from "./conversation-content";
import { DatabaseContent } from "./database-content";
import { IoContent } from "./io-content";
import { JsonContent } from "./json-content";
import { KnowledgeMaintainContent } from "./knowledge-maintain-content";
import { MessageContent } from "./message-content";
import { VariableAggregatorContent } from "./variable-aggregator-content";

describe("NodeContentMap", () => {
  it("maps JsonSerialization to JsonContent", () => {
    const element = NodeContentMap({
      type: "JsonSerialization",
      data: {
        configs: {
          variableKeys: ["a", "b"],
          outputKey: "json_output"
        }
      }
    });
    expect(element.type).toBe(JsonContent);
  });

  it("maps VariableAggregator to VariableAggregatorContent", () => {
    const element = NodeContentMap({
      type: "VariableAggregator",
      data: {
        configs: {
          variableKeys: ["x"],
          outputKey: "aggregated",
          strategy: "merge"
        }
      }
    });
    expect(element.type).toBe(VariableAggregatorContent);
  });

  it("maps database nodes to DatabaseContent", () => {
    const element = NodeContentMap({
      type: "DatabaseQuery",
      data: {
        configs: {
          databaseInfoId: 1,
          outputKey: "rows"
        }
      }
    });
    expect(element.type).toBe(DatabaseContent);
  });

  it("maps conversation nodes to ConversationContent", () => {
    const element = NodeContentMap({
      type: "ConversationList",
      data: {
        configs: {
          userId: 1,
          conversationId: 2
        }
      }
    });
    expect(element.type).toBe(ConversationContent);
  });

  it("maps Agent to AgentContent", () => {
    const element = NodeContentMap({
      type: "Agent",
      data: {
        configs: {
          agentId: "agent_1",
          message: "hello"
        }
      }
    });
    expect(element.type).toBe(AgentContent);
  });

  it("maps InputReceiver to IoContent", () => {
    const element = NodeContentMap({
      type: "InputReceiver",
      data: {
        configs: {
          inputPath: "input.message"
        }
      }
    });
    expect(element.type).toBe(IoContent);
  });

  it("maps KnowledgeIndexer to KnowledgeMaintainContent", () => {
    const element = NodeContentMap({
      type: "KnowledgeIndexer",
      data: {
        configs: {
          knowledgeId: 100
        }
      }
    });
    expect(element.type).toBe(KnowledgeMaintainContent);
  });

  it("maps CreateMessage to MessageContent", () => {
    const element = NodeContentMap({
      type: "CreateMessage",
      data: {
        configs: {
          conversationId: 10
        }
      }
    });
    expect(element.type).toBe(MessageContent);
  });

  it("maps Comment to CommentContent", () => {
    const element = NodeContentMap({
      type: "Comment",
      data: {
        configs: {
          content: "note"
        }
      }
    });
    expect(element.type).toBe(CommentContent);
  });

  it("falls back to CommonContent for unknown node type", () => {
    const element = NodeContentMap({
      type: "UnknownNodeType",
      data: {
        title: "unknown"
      }
    });
    expect(element.type).toBe(CommonContent);
  });
});
