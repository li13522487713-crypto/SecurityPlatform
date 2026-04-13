import { describe, expect, it } from "vitest";
import type { ApiResponse } from "@atlas/shared-react-core/types";
import { createWorkflowV2Api } from "./workflow-api-factory";

describe("workflow-api-factory", () => {
  it("saveDraft 会把编辑器画布转换成后端运行时画布协议", async () => {
    let capturedPath = "";
    let capturedInit: RequestInit | undefined;

    const api = createWorkflowV2Api({
      requestFn: async <T>(path: string, init?: RequestInit) => {
        capturedPath = path;
        capturedInit = init;
        return {
          success: true,
          code: "SUCCESS",
          message: "OK",
          traceId: "spec",
          data: true
        } as T;
      }
    });

    await api.saveDraft("123", {
      canvasJson: JSON.stringify({
        nodes: [
          {
            key: "entry_1",
            type: "Entry",
            title: "开始",
            layout: { x: 120, y: 80, width: 160, height: 60 },
            configs: {
              entryVariable: "incident"
            },
            inputMappings: {
              incident: "input.incident"
            }
          },
          {
            key: "text_1",
            type: "TextProcessor",
            title: "文本处理",
            layout: { x: 360, y: 80, width: 220, height: 80 },
            configs: {
              template: "{{incident}}",
              outputKey: "result"
            }
          }
        ],
        connections: [
          {
            fromNode: "entry_1",
            fromPort: "output",
            toNode: "text_1",
            toPort: "input",
            condition: null
          }
        ]
      }),
      commitId: "commit-1"
    });

    expect(capturedPath).toBe("/api/v2/workflows/123/draft");
    const requestBody = JSON.parse(String(capturedInit?.body)) as {
      canvasJson: string;
      commitId: string | null;
    };
    const normalizedCanvas = JSON.parse(requestBody.canvasJson) as {
      nodes: Array<{
        key: string;
        type: number;
        label: string;
        config: Record<string, unknown>;
      }>;
      connections: Array<{
        sourceNodeKey: string;
        sourcePort: string;
        targetNodeKey: string;
        targetPort: string;
        condition: string | null;
      }>;
    };

    expect(requestBody.commitId).toBe("commit-1");
    expect(normalizedCanvas.nodes[0]?.type).toBe(1);
    expect(normalizedCanvas.nodes[0]?.label).toBe("开始");
    expect(normalizedCanvas.nodes[0]?.config).toMatchObject({
      entryVariable: "incident",
      inputMappings: {
        incident: "input.incident"
      }
    });
    expect(normalizedCanvas.nodes[1]?.type).toBe(15);
    expect(normalizedCanvas.connections[0]).toEqual({
      sourceNodeKey: "entry_1",
      sourcePort: "output",
      targetNodeKey: "text_1",
      targetPort: "input",
      condition: null
    });
  });
});
