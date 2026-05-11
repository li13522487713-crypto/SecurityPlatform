import { describe, expect, it, vi } from "vitest";
import type { WorkflowPortEntity } from "@flowgram-adapter/free-layout-editor";

vi.mock("./FlowGramMicroflowNodeRenderer", () => ({
  FlowGramMicroflowNodeRenderer: () => null,
}));

import { FlowGramMicroflowDocumentOptions } from "./FlowGramMicroflowEvents";

function port(input: {
  nodeId: string;
  parentId?: string;
  portType: "input" | "output";
  disabled?: boolean;
}): WorkflowPortEntity {
  return {
    portType: input.portType,
    disabled: input.disabled,
    node: {
      id: input.nodeId,
      parent: input.parentId ? { id: input.parentId } : undefined,
    },
  } as unknown as WorkflowPortEntity;
}

describe("FlowGramMicroflowDocumentOptions", () => {
  it("allows same-parent lines and rejects loop child to root crossing", () => {
    const options = new FlowGramMicroflowDocumentOptions();

    expect(options.canAddLine(
      port({ nodeId: "root-a", portType: "output" }),
      port({ nodeId: "root-b", portType: "input" }),
    )).toBe(true);
    expect(options.canAddLine(
      port({ nodeId: "loop-child", parentId: "loop-1", portType: "output" }),
      port({ nodeId: "root-b", portType: "input" }),
    )).toBe(false);
  });

  it("allows a loop node to connect to its own body entry", () => {
    const options = new FlowGramMicroflowDocumentOptions();

    expect(options.canAddLine(
      port({ nodeId: "loop-1", portType: "output" }),
      port({ nodeId: "loop-child", parentId: "loop-1", portType: "input" }),
    )).toBe(true);
  });

  it("disallows nested-to-root transitions while allowing same-container and loop-exit transitions", () => {
    const options = new FlowGramMicroflowDocumentOptions();

    expect(options.canAddLine(
      port({ nodeId: "loop-child", parentId: "loop-1", portType: "output" }),
      port({ nodeId: "root-node", portType: "input" }),
    )).toBe(false);
    expect(options.canAddLine(
      port({ nodeId: "loop-child-a", parentId: "loop-1", portType: "output" }),
      port({ nodeId: "loop-child-b", parentId: "loop-1", portType: "input" }),
    )).toBe(true);
    expect(options.canAddLine(
      port({ nodeId: "loop-1", portType: "output" }),
      port({ nodeId: "outside", portType: "input" }),
    )).toBe(true);
  });
});
