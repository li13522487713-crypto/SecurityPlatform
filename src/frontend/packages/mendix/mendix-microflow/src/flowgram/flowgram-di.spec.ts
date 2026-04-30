import { describe, expect, it, vi } from "vitest";
import { Container } from "inversify";
import { WorkflowDocumentOptions, type WorkflowPortEntity } from "@flowgram-adapter/free-layout-editor";

vi.mock("./FlowGramMicroflowNodeRenderer", () => ({
  FlowGramMicroflowNodeRenderer: () => null,
}));

import { FlowGramMicroflowDocumentOptions } from "./FlowGramMicroflowEvents";

type TestPort = WorkflowPortEntity & {
  disabled?: boolean;
  node?: {
    id?: string;
    parent?: { id?: string };
  };
};

function port(options: {
  portType: "input" | "output";
  nodeId: string;
  parentId?: string;
  disabled?: boolean;
}): WorkflowPortEntity {
  return {
    portType: options.portType,
    disabled: options.disabled,
    node: {
      id: options.nodeId,
      parent: options.parentId ? { id: options.parentId } : undefined,
    },
  } as TestPort;
}

describe("FlowGram microflow DI", () => {
  it("binds document options without a runtime schema bridge service", () => {
    const container = new Container({ defaultScope: "Singleton" });
    container.bind(FlowGramMicroflowDocumentOptions).to(FlowGramMicroflowDocumentOptions).inSingletonScope();
    container.bind(WorkflowDocumentOptions).toService(FlowGramMicroflowDocumentOptions);

    const options = container.get<FlowGramMicroflowDocumentOptions>(FlowGramMicroflowDocumentOptions);
    const workflowOptions = container.get<WorkflowDocumentOptions>(WorkflowDocumentOptions);

    expect(options).toBe(workflowOptions);
    expect(options.canAddLine(
      port({ portType: "output", nodeId: "start", parentId: "root" }),
      port({ portType: "input", nodeId: "end", parentId: "root" }),
    )).toBe(true);
    expect(options.canAddLine(
      port({ portType: "input", nodeId: "start", parentId: "root" }),
      port({ portType: "input", nodeId: "end", parentId: "root" }),
    )).toBe(false);
    expect(options.canAddLine(
      port({ portType: "output", nodeId: "start", parentId: "root" }),
      port({ portType: "input", nodeId: "end", parentId: "nested" }),
    )).toBe(false);
  });
});
