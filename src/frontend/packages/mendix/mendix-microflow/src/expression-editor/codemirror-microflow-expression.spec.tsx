// @vitest-environment jsdom

import { cleanup, render, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

vi.stubGlobal("ResizeObserver", ResizeObserverStub);

import CodemirrorMicroflowExpression from "./codemirror-microflow-expression";

afterEach(() => cleanup());

describe("codemirror-microflow-expression", () => {
  it("mounts CodeMirror and shows initial doc", async () => {
    const onChange = vi.fn();
    render(<CodemirrorMicroflowExpression value="$sample + 1" onChange={onChange} minRows={2} />);
    await waitFor(() => {
      expect(document.querySelector(".cm-content")).toBeTruthy();
    });
    expect(document.querySelector(".cm-content")?.textContent ?? "").toContain("$sample");
  });
});
