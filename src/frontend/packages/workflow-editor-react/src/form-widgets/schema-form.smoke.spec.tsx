// @vitest-environment jsdom
import { describe, expect, it, vi } from "vitest";
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { SchemaForm } from "./SchemaForm";
import type { FormSectionSchema } from "../node-registry";

let latestMonacoProps: { value: string; onChange?: (value: string) => void; language?: string } | null = null;

vi.mock("@monaco-editor/react", () => ({
  default: (props: { value: string; onChange?: (value: string) => void; language?: string }) => {
    latestMonacoProps = props;
    return (
      <textarea
        data-testid="mock-monaco"
        data-language={props.language}
        value={props.value}
        onChange={(event) => props.onChange?.(event.target.value)}
      />
    );
  }
}));

const sections: FormSectionSchema[] = [
  {
    key: "basic",
    title: "代码执行",
    fields: [
      {
        key: "codeBody",
        label: "代码",
        kind: "code",
        path: "code.source",
        languagePath: "code.language",
        rows: 8,
        required: true
      },
      {
        key: "mapping",
        label: "输入映射",
        kind: "keyValue",
        path: "inputMappings"
      }
    ]
  }
];

describe("SchemaForm smoke", () => {
  it("renders code field and writes back config changes", async () => {
    (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
    Object.defineProperty(window, "matchMedia", {
      writable: true,
      value: (query: string) => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: () => undefined,
        removeListener: () => undefined,
        addEventListener: () => undefined,
        removeEventListener: () => undefined,
        dispatchEvent: () => false
      })
    });

    const container = document.createElement("div");
    document.body.appendChild(container);
    let root: Root | undefined;

    let nextConfig: Record<string, unknown> = {
      code: {
        language: "python",
        source: "print('hello')"
      },
      inputMappings: {}
    };

    const onChange = vi.fn((next: Record<string, unknown>) => {
      nextConfig = next;
    });

    await act(async () => {
      root = createRoot(container);
      root.render(
        <SchemaForm
          sections={sections}
          config={nextConfig}
          onChange={onChange}
          variableSuggestions={[{ value: "{{entry_1.input}}", label: "entry_1.input" }]}
        />
      );
    });

    const monaco = container.querySelector<HTMLTextAreaElement>('[data-testid="mock-monaco"]');
    expect(monaco).toBeTruthy();
    expect(monaco?.getAttribute("data-language")).toBe("python");

    await act(async () => {
      latestMonacoProps?.onChange?.("print('world')");
    });

    const hasExpectedUpdate = onChange.mock.calls.some((call) => {
      const payload = call[0] as Record<string, unknown> | undefined;
      if (!payload || typeof payload !== "object") {
        return false;
      }
      const code = payload.code as Record<string, unknown> | undefined;
      return Boolean(code && code.source === "print('world')");
    });
    expect(hasExpectedUpdate).toBe(true);

    await act(async () => {
      root?.unmount();
    });
    container.remove();
  });
});

