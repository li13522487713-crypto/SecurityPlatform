// @vitest-environment jsdom

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { AnnotationNode } from "./AnnotationNode";

afterEach(() => cleanup());

describe("AnnotationNode", () => {
  it("renders title and icon content", () => {
    render(<AnnotationNode title="Loop note" icon={<span data-testid="annotation-icon">i</span>} />);

    expect(screen.getByText("Loop note")).toBeTruthy();
    expect(screen.getByTestId("annotation-icon").textContent).toBe("i");
  });
});
