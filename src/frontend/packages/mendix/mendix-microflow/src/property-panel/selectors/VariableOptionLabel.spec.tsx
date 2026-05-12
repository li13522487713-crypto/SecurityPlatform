// @vitest-environment jsdom

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { VariableOptionLabel } from "./VariableOptionLabel";

vi.mock("@douyinfe/semi-ui", () => ({
  Space: ({ children }: any) => <div>{children}</div>,
  Tag: ({ children }: any) => <span>{children}</span>,
  Tooltip: ({ children }: any) => <>{children}</>,
  Typography: {
    Text: ({ children, style }: any) => <span style={style}>{children}</span>,
  },
}));

afterEach(() => cleanup());

describe("VariableOptionLabel", () => {
  it("renders maybe variable with warning label and scope tag", () => {
    render(
      <VariableOptionLabel
        symbol={{
          name: "$customer",
          dataType: { kind: "object", entityQualifiedName: "Sales.Customer" },
          source: { kind: "actionOutput", objectId: "node-1", actionId: "act-1" },
          scope: { kind: "errorHandler", collectionId: "root", errorHandlerFlowId: "flow-err" },
          visibility: "maybe",
          readonly: false,
          maybeReason: "",
        }}
      />
    );

    expect(screen.getByText("$customer")).toBeTruthy();
    expect(screen.getByText("Error scope")).toBeTruthy();
    expect(screen.getByText("⚠ maybe")).toBeTruthy();
  });
});

