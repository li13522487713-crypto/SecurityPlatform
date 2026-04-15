import { describe, expect, it } from "vitest";
import { toBackendCanvasJson, toEditorCanvasJson } from "./canvas-schema-adapter";

describe("canvas-schema-adapter", () => {
  it("应将编辑器 Coze 形态 schema 转为 Atlas 后端 payload 并保留映射", () => {
    const editorSchema = JSON.stringify({
      nodes: [
        {
          key: "entry_1",
          type: "Entry",
          title: "开始",
          layout: { x: 120, y: 80, width: 160, height: 60 },
          configs: {
            entryVariable: "incident",
            entryAutoSaveHistory: true
          },
          inputMappings: {
            incident: "ticket.payload"
          }
        },
        {
          key: "loop_1",
          type: "Loop",
          title: "循环",
          layout: { x: 420, y: 80, width: 200, height: 80 },
          configs: {
            mode: "forEach",
            collectionPath: "{{items}}"
          },
          childCanvas: {
            nodes: [
              {
                key: "text_1",
                type: "TextProcessor",
                title: "文本",
                layout: { x: 32, y: 40, width: 160, height: 60 },
                configs: {
                  template: "{{loop_item}}",
                  outputKey: "line"
                },
                inputMappings: {}
              }
            ],
            connections: []
          }
        }
      ],
      connections: [
        {
          fromNode: "entry_1",
          fromPort: "output",
          toNode: "loop_1",
          toPort: "input",
          condition: null
        }
      ],
      schemaVersion: 2,
      globals: {
        severity: "high"
      },
      viewport: {
        x: 0,
        y: 0,
        zoom: 100
      }
    });

    const backendPayload = JSON.parse(toBackendCanvasJson(editorSchema)) as {
      nodes: Array<{ key: string; type: number; config: Record<string, unknown>; childCanvas?: { nodes: Array<{ key: string }> } }>;
      connections: Array<{ sourceNodeKey: string; targetNodeKey: string }>;
      globals: Record<string, unknown>;
    };

    expect(backendPayload.nodes).toHaveLength(2);
    expect(backendPayload.nodes[0]?.type).toBe(1);
    expect((backendPayload.nodes[0]?.config?.inputMappings as Record<string, string>)?.incident).toBe("ticket.payload");
    expect(backendPayload.nodes[1]?.childCanvas?.nodes[0]?.key).toBe("text_1");
    expect(backendPayload.connections[0]?.sourceNodeKey).toBe("entry_1");
    expect(backendPayload.connections[0]?.targetNodeKey).toBe("loop_1");
    expect(backendPayload.globals?.severity).toBe("high");
  });

  it("应将 Atlas 后端 payload 转回编辑器形态并保持可逆等价", () => {
    const backendSchema = JSON.stringify({
      nodes: [
        {
          key: "entry_1",
          type: 1,
          label: "开始",
          config: {
            entryVariable: "USER_INPUT",
            inputMappings: {
              incident: "ticket.payload"
            }
          },
          layout: { x: 120, y: 80, width: 160, height: 60 },
          inputSources: [{ field: "incident", path: "ticket.payload" }]
        },
        {
          key: "exit_1",
          type: 2,
          label: "结束",
          config: {
            exitTerminateMode: "return",
            exitTemplate: "{{incident.summary}}"
          },
          layout: { x: 420, y: 80, width: 160, height: 60 }
        }
      ],
      connections: [
        {
          sourceNodeKey: "entry_1",
          sourcePort: "output",
          targetNodeKey: "exit_1",
          targetPort: "input",
          condition: null
        }
      ],
      schemaVersion: 2
    });

    const editorPayload = JSON.parse(toEditorCanvasJson(backendSchema)) as {
      nodes: Array<{ key: string; type: string; configs: Record<string, unknown>; inputMappings: Record<string, string>; inputSources?: Array<{ field: string; path: string }> }>;
      connections: Array<{ fromNode: string; toNode: string }>;
    };

    expect(editorPayload.nodes[0]?.type).toBe("Entry");
    expect(editorPayload.nodes[0]?.inputMappings?.incident).toBe("ticket.payload");
    expect(editorPayload.nodes[0]?.inputSources?.[0]?.path).toBe("ticket.payload");
    expect(editorPayload.connections[0]?.fromNode).toBe("entry_1");
    expect(editorPayload.connections[0]?.toNode).toBe("exit_1");

    const roundtripBack = JSON.parse(toBackendCanvasJson(JSON.stringify(editorPayload))) as {
      nodes: Array<{ key: string; type: number; config: Record<string, unknown> }>;
    };
    expect(roundtripBack.nodes[0]?.type).toBe(1);
    expect((roundtripBack.nodes[0]?.config?.inputMappings as Record<string, string>)?.incident).toBe("ticket.payload");
  });
});
