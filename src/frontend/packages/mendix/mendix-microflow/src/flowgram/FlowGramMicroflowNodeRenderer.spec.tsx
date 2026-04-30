// @vitest-environment happy-dom
import { afterEach, describe, expect, it } from "vitest";

import {
  focusMicroflowNodeDragRoot,
  isMicroflowNodeDragBlockedTarget,
} from "./flowgram-node-drag";

describe("FlowGramMicroflowNodeRenderer drag helpers", () => {
  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("focuses the node root before FlowGram startDrag checks document.activeElement", () => {
    document.body.innerHTML = `
      <input id="search" />
      <div id="node" tabindex="0"><span>Start</span></div>
    `;
    const search = document.querySelector<HTMLInputElement>("#search");
    const node = document.querySelector<HTMLElement>("#node");
    if (!search || !node) {
      throw new Error("Expected test elements.");
    }

    search.focus();
    expect(document.activeElement).toBe(search);
    focusMicroflowNodeDragRoot(node);

    expect(document.activeElement).toBe(node);
  });

  it("blocks drag from interactive ports and controls but allows the node body", () => {
    document.body.innerHTML = `
      <div id="node" tabindex="0">
        <span id="body">Start</span>
        <button id="button">Edit</button>
        <span id="tag" data-flow-editor-selectable="false">Beta</span>
        <span id="port" class="workflow-port-render"></span>
      </div>
    `;

    expect(isMicroflowNodeDragBlockedTarget(document.querySelector("#body"))).toBe(false);
    expect(isMicroflowNodeDragBlockedTarget(document.querySelector("#button"))).toBe(true);
    expect(isMicroflowNodeDragBlockedTarget(document.querySelector("#tag"))).toBe(true);
    expect(isMicroflowNodeDragBlockedTarget(document.querySelector("#port"))).toBe(true);
  });
});
