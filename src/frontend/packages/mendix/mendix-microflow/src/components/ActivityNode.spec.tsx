// @vitest-environment jsdom

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { ActivityNode } from "./ActivityNode";

afterEach(() => cleanup());

describe("ActivityNode", () => {
  it("renders title/subtitle and runtime error dot", () => {
    const { container } = render(
      <ActivityNode
        title="Create Object"
        subtitle="Microflows$CreateObjectAction"
        icon={<span data-testid="activity-icon">A</span>}
        showRuntimeErrorDot
      />,
    );

    expect(screen.getByText("Create Object")).toBeTruthy();
    expect(screen.getByText("Microflows$CreateObjectAction")).toBeTruthy();
    expect(screen.getByTestId("activity-icon").textContent).toBe("A");
    expect(container.querySelector(".microflow-node-runtime-error-dot")).toBeTruthy();
  });
});
