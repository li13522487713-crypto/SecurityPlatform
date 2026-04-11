import { AssignVariableContent } from "./assign-variable-content";
import { BatchContent } from "./batch-content";
import { CodeContent } from "./code-content";
import { CommonContent } from "./common-content";
import { EndContent } from "./end-content";
import { HttpContent } from "./http-content";
import { IfContent } from "./if-content";
import { IntentContent } from "./intent-content";
import { KnowledgeContent } from "./knowledge-content";
import { LlmContent } from "./llm-content";
import { LoopContent } from "./loop-content";
import { PluginContent } from "./plugin-content";
import { QaContent } from "./qa-content";
import { StartContent } from "./start-content";
import { SubWorkflowContent } from "./subworkflow-content";

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
  const http = asRecord(configs.http);
  const plugin = asRecord(configs.plugin);
  const assign = asRecord(configs.assign);
  const loop = asRecord(configs.loop);
  const batch = asRecord(configs.batch);
  const selectorConditions = Array.isArray(configs.conditions) ? configs.conditions : [];
  const intents = Array.isArray(configs.intents) ? configs.intents.map((item) => String(item)) : [];
  const knowledgeIds = Array.isArray(configs.knowledgeIds) ? configs.knowledgeIds : [];

  const readText = (...candidates: unknown[]): string => {
    for (const candidate of candidates) {
      if (typeof candidate === "string" && candidate.trim()) {
        return candidate.trim();
      }
    }
    return "";
  };

  const readNumber = (...candidates: unknown[]): number | undefined => {
    for (const candidate of candidates) {
      if (typeof candidate === "number" && Number.isFinite(candidate)) {
        return candidate;
      }
    }
    return undefined;
  };

  if (props.type === "Selector") {
    return <IfContent conditions={selectorConditions} />;
  }

  if (props.type === "Loop") {
    return (
      <LoopContent
        mode={readText(configs.mode, loop.mode)}
        maxIterations={readNumber(configs.maxIterations, loop.maxIterations)}
        collectionPath={readText(configs.collectionPath, loop.collectionPath)}
        condition={readText(configs.condition, loop.condition)}
      />
    );
  }

  if (props.type === "Batch") {
    return (
      <BatchContent
        concurrentSize={readNumber(configs.concurrentSize, batch.concurrentSize)}
        batchSize={readNumber(configs.batchSize, batch.batchSize)}
        inputArrayPath={readText(configs.inputArrayPath, batch.inputArrayPath)}
        outputKey={readText(configs.outputKey, batch.outputKey)}
      />
    );
  }

  if (props.type === "Llm") {
    return <LlmContent provider={readText(configs.provider, llm.provider)} model={readText(configs.model, llm.model)} />;
  }

  if (props.type === "IntentDetector") {
    return <IntentContent model={readText(configs.model)} intents={intents} />;
  }

  if (props.type === "QuestionAnswer") {
    return <QaContent answerType={readText(configs.answerType)} answerPath={readText(configs.answerPath)} />;
  }

  if (props.type === "Entry") {
    return <StartContent variable={readText(configs.entryVariable, configs.variable, entry.entryVariable, entry.variable)} />;
  }

  if (props.type === "Exit") {
    return <EndContent terminateMode={readText(configs.terminateMode, exit.terminateMode)} />;
  }

  if (props.type === "CodeRunner") {
    return <CodeContent language={readText(configs.language)} />;
  }

  if (props.type === "Plugin") {
    return <PluginContent pluginId={readText(configs.pluginId, plugin.pluginId)} action={readText(configs.action, plugin.action)} />;
  }

  if (props.type === "HttpRequester") {
    return <HttpContent method={readText(configs.method, http.method)} url={readText(configs.url, http.url)} timeoutMs={readNumber(configs.timeoutMs, http.timeoutMs)} />;
  }

  if (props.type === "SubWorkflow") {
    return (
      <SubWorkflowContent
        workflowId={readText(configs.workflowId)}
        maxDepth={readNumber(configs.maxDepth)}
        outputKey={readText(configs.outputKey)}
      />
    );
  }

  if (props.type === "KnowledgeRetriever") {
    return (
      <KnowledgeContent
        topK={readNumber(configs.topK)}
        minScore={readNumber(configs.minScore)}
        knowledgeCount={knowledgeIds.length}
      />
    );
  }

  if (props.type === "AssignVariable" || props.type === "VariableAssignerWithinLoop") {
    return (
      <AssignVariableContent
        variableName={readText(configs.variableName, configs.target)}
        expression={readText(configs.expression, assign.expression)}
      />
    );
  }

  return <CommonContent summary={String(props.data.title ?? props.type)} />;
}
