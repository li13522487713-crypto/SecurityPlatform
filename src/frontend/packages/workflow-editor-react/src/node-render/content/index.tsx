import { CodeContent } from "./code-content";
import { CommonContent } from "./common-content";
import { EndContent } from "./end-content";
import { IfContent } from "./if-content";
import { LlmContent } from "./llm-content";
import { StartContent } from "./start-content";

interface ContentProps {
  type: string;
  data: Record<string, unknown>;
}

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value) ? (value as Record<string, unknown>) : {};
}

export function NodeContentMap(props: ContentProps) {
  const configs = asRecord(props.data.configs);
  const llm = asRecord(configs.llm);
  const entry = asRecord(configs.entry);
  const exit = asRecord(configs.exit);

  if (props.type === "Selector") {
    return <IfContent conditions={Array.isArray(configs.conditions) ? configs.conditions : []} />;
  }

  if (props.type === "Llm") {
    return <LlmContent provider={String(llm.provider ?? "")} model={String(llm.model ?? "")} />;
  }

  if (props.type === "Entry") {
    return <StartContent variable={String(entry.variable ?? "")} />;
  }

  if (props.type === "Exit") {
    return <EndContent terminateMode={String(exit.terminateMode ?? "")} />;
  }

  if (props.type === "CodeRunner") {
    return <CodeContent language={String(configs.language ?? "")} />;
  }

  return <CommonContent summary={String(props.data.title ?? props.type)} />;
}
