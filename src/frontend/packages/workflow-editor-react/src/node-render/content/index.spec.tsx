import { describe, expect, it } from "vitest";
import { NodeContentMap } from "./index";
import { CommonContent } from "./common-content";
import { ConversationContent } from "./conversation-content";
import { DatabaseContent } from "./database-content";
import { JsonContent } from "./json-content";
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
