// @vitest-environment jsdom
import { describe, expect, it, vi } from "vitest";
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { SchemaForm } from "./SchemaForm";
import type { FormSectionSchema } from "../node-registry";

vi.mock("@monaco-editor/react", () => ({
  default: (props: { value: string; onChange?: (value: string) => void; language?: string }) => (
    <textarea
      data-testid="mock-monaco"
      data-language={props.language}
      value={props.value}
      onChange={(event) => props.onChange?.(event.target.value)}
    />
  )
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
      if (monaco) {
        monaco.value = "print('world')";
        monaco.dispatchEvent(new Event("change", { bubbles: true }));
      }
    });

    const latest = onChange.mock.calls.at(-1)?.[0] as Record<string, unknown>;
    const code = latest.code as Record<string, unknown>;
    expect(code.source).toBe("print('world')");

    await act(async () => {
      root?.unmount();
    });
    container.remove();
  });
});

