import { describe, expect, it } from "vitest";

import type { MicroflowWorkflowEdgeJSON, MicroflowWorkflowJSON } from "../schema/types";
import { validateInlineLineLabelCommit } from "./inline-line-label-validation";

function edge(patch: Partial<MicroflowWorkflowEdgeJSON>): MicroflowWorkflowEdgeJSON {
  return {
    id: "flow-1",
    sourceNodeID: "node-a",
    targetNodeID: "node-b",
    data: { flowId: "flow-1", label: "" },
    ...patch,
  } as MicroflowWorkflowEdgeJSON;
}

function workflow(edges: MicroflowWorkflowEdgeJSON[]): MicroflowWorkflowJSON {
  return {
    nodes: [],
    edges,
  } as unknown as MicroflowWorkflowJSON;
}

describe("validateInlineLineLabelCommit", () => {
  it("rejects illegal decision labels", () => {
    const result = validateInlineLineLabelCommit({
      workflow: workflow([
        edge({
          caseValues: [{ kind: "boolean" }] as never,
        }),
      ]),
      flowId: "flow-1",
      nextLabel: "maybe",
    });
    expect(result.ok).toBe(false);
  });

  it("normalizes allowed decision label", () => {
    const result = validateInlineLineLabelCommit({
      workflow: workflow([
        edge({
          caseValues: [{ kind: "boolean" }] as never,
        }),
      ]),
      flowId: "flow-1",
      nextLabel: " TRUE ",
    });
    expect(result).toEqual({ ok: true, normalizedLabel: "true" });
  });

  it("detects duplicate scoped labels from same source node", () => {
    const result = validateInlineLineLabelCommit({
      workflow: workflow([
        edge({
          id: "flow-1",
          data: { flowId: "flow-1", label: "false" },
          caseValues: [{ kind: "boolean" }] as never,
        }),
        edge({
          id: "flow-2",
          targetNodeID: "node-c",
          data: { flowId: "flow-2", label: "true" },
          caseValues: [{ kind: "boolean" }] as never,
        }),
      ]),
      flowId: "flow-1",
      nextLabel: " true ",
    });
    expect(result.ok).toBe(false);
  });

  it("treats approval labels as approval scope without sourceActionKind", () => {
    const result = validateInlineLineLabelCommit({
      workflow: workflow([
        edge({
          data: { flowId: "flow-1", label: "approved" },
        }),
      ]),
      flowId: "flow-1",
      nextLabel: "invalid",
    });
    expect(result.ok).toBe(false);
  });

  it("validates loop scope labels", () => {
    const illegal = validateInlineLineLabelCommit({
      workflow: workflow([
        edge({
          data: { flowId: "flow-1", label: "" },
          sourcePortID: "loop-1:loopOut:1",
        }),
      ]),
      flowId: "flow-1",
      nextLabel: "next",
    });
    expect(illegal.ok).toBe(false);
    const legal = validateInlineLineLabelCommit({
      workflow: workflow([
        edge({
          data: { flowId: "flow-1", label: "" },
          sourcePortID: "loop-1:loopOut:1",
        }),
      ]),
      flowId: "flow-1",
      nextLabel: " done ",
    });
    expect(legal).toEqual({ ok: true, normalizedLabel: "done" });
  });

  it("validates error scope labels", () => {
    const illegal = validateInlineLineLabelCommit({
      workflow: workflow([
        edge({
          data: { flowId: "flow-1", label: "" },
          sourcePortID: "action-1:errorOut:99",
        }),
      ]),
      flowId: "flow-1",
      nextLabel: "recover",
    });
    expect(illegal.ok).toBe(false);
    const legal = validateInlineLineLabelCommit({
      workflow: workflow([
        edge({
          data: { flowId: "flow-1", label: "" },
          sourcePortID: "action-1:errorOut:99",
        }),
      ]),
      flowId: "flow-1",
      nextLabel: " fallback ",
    });
    expect(legal).toEqual({ ok: true, normalizedLabel: "fallback" });
  });

  it("allows custom scope labels and empty labels", () => {
    const custom = validateInlineLineLabelCommit({
      workflow: workflow([
        edge({
          data: { flowId: "flow-1", label: "" },
        }),
      ]),
      flowId: "flow-1",
      nextLabel: " custom branch ",
    });
    expect(custom).toEqual({ ok: true, normalizedLabel: "custom branch" });
    const empty = validateInlineLineLabelCommit({
      workflow: workflow([
        edge({
          data: { flowId: "flow-1", label: "" },
        }),
      ]),
      flowId: "flow-1",
      nextLabel: "   ",
    });
    expect(empty).toEqual({ ok: true, normalizedLabel: "" });
  });

  it("allows when flowId is missing from workflow", () => {
    const result = validateInlineLineLabelCommit({
      workflow: workflow([
        edge({
          id: "flow-1",
          data: { flowId: "flow-1", label: "true" },
          caseValues: [{ kind: "boolean" }] as never,
        }),
      ]),
      flowId: "flow-missing",
      nextLabel: "anything",
    });
    expect(result).toEqual({ ok: true, normalizedLabel: "anything" });
  });

  it("does not conflict across different source nodes or scopes", () => {
    const crossSource = validateInlineLineLabelCommit({
      workflow: workflow([
        edge({
          id: "flow-1",
          sourceNodeID: "node-a",
          data: { flowId: "flow-1", label: "false" },
          caseValues: [{ kind: "boolean" }] as never,
        }),
        edge({
          id: "flow-2",
          sourceNodeID: "node-b",
          data: { flowId: "flow-2", label: "true" },
          caseValues: [{ kind: "boolean" }] as never,
        }),
      ]),
      flowId: "flow-1",
      nextLabel: "true",
    });
    expect(crossSource).toEqual({ ok: true, normalizedLabel: "true" });

    const crossScope = validateInlineLineLabelCommit({
      workflow: workflow([
        edge({
          id: "flow-1",
          sourceNodeID: "node-a",
          data: { flowId: "flow-1", label: "" },
          caseValues: [{ kind: "boolean" }] as never,
        }),
        edge({
          id: "flow-2",
          sourceNodeID: "node-a",
          data: { flowId: "flow-2", label: "fallback" },
          sourcePortID: "action-1:errorOut:99",
        }),
      ]),
      flowId: "flow-2",
      nextLabel: "fallback",
    });
    expect(crossScope).toEqual({ ok: true, normalizedLabel: "fallback" });
  });

  it("treats fallback caseValues as decision scope", () => {
    const result = validateInlineLineLabelCommit({
      workflow: workflow([
        edge({
          caseValues: [{ kind: "fallback" }] as never,
        }),
      ]),
      flowId: "flow-1",
      nextLabel: "other",
    });
    expect(result.ok).toBe(false);
  });
});
