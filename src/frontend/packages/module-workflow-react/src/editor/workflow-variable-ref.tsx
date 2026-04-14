import { Button, Typography } from "@douyinfe/semi-ui";
import type { WorkflowDependencies } from "../types";

export interface WorkflowVariableRefPanelCopy {
  title: string;
  globalsTitle: string;
  dependencyTitle: string;
  emptyGlobals: string;
  emptyDependencies: string;
  openVariables: string;
  sourceNodes: string;
}

export interface WorkflowVariableRefPanelProps {
  globals: Record<string, unknown>;
  dependencies: WorkflowDependencies | null;
  copy: WorkflowVariableRefPanelCopy;
  onOpenVariablesTab: () => void;
}

/**
 * Read-only side panel summarizing canvas globals and dependency variable references.
 * Editing remains in the workflow editor variable panel / variables tab.
 */
export function WorkflowVariableRefPanel(props: WorkflowVariableRefPanelProps) {
  const { globals, dependencies, copy, onOpenVariablesTab } = props;
  const globalEntries = Object.entries(globals ?? {}).sort((a, b) => a[0].localeCompare(b[0]));
  const depVars = dependencies?.variables ?? [];

  return (
    <aside className="module-workflow__variable-ref" data-testid="workflow-variable-ref-panel">
      <div className="module-workflow__variable-ref-head">
        <Typography.Text strong>{copy.title}</Typography.Text>
        <Button size="small" type="tertiary" onClick={onOpenVariablesTab}>
          {copy.openVariables}
        </Button>
      </div>
      <div className="module-workflow__variable-ref-section">
        <div className="module-workflow__variable-ref-section-title">{copy.globalsTitle}</div>
        {globalEntries.length === 0 ? (
          <div className="module-workflow__variable-ref-empty">{copy.emptyGlobals}</div>
        ) : (
          <ul className="module-workflow__variable-ref-list">
            {globalEntries.map(([key, value]) => (
              <li key={key}>
                <code className="module-workflow__variable-ref-key">{key}</code>
                <span className="module-workflow__variable-ref-value">
                  {typeof value === "string" ? value : JSON.stringify(value)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
      <div className="module-workflow__variable-ref-section">
        <div className="module-workflow__variable-ref-section-title">{copy.dependencyTitle}</div>
        {depVars.length === 0 ? (
          <div className="module-workflow__variable-ref-empty">{copy.emptyDependencies}</div>
        ) : (
          <ul className="module-workflow__variable-ref-list">
            {depVars.map((item) => (
              <li key={`${item.resourceType}-${item.resourceId}`}>
                <strong>{item.name}</strong>
                {item.sourceNodeKeys?.length ? (
                  <small>
                    {copy.sourceNodes}: {item.sourceNodeKeys.join(", ")}
                  </small>
                ) : null}
              </li>
            ))}
          </ul>
        )}
      </div>
    </aside>
  );
}
