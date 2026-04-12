import { useEffect, useState } from "react";
import { Button, Empty, Typography } from "@douyinfe/semi-ui";
import { WorkflowEditor } from "@atlas/workflow-editor-react";
import type { WorkflowPageProps, WorkflowListItem } from "./types";

export function WorkflowListPage({
  api,
  onOpenEditor
}: WorkflowPageProps & { onOpenEditor: (workflowId: string) => void }) {
  const [items, setItems] = useState<WorkflowListItem[]>([]);

  const load = async () => {
    const result = await api.listWorkflows();
    setItems(result.items);
  };

  useEffect(() => {
    void load();
  }, []);

  return (
    <section className="module-workflow__page" data-testid="app-workflows-page">
      <div className="module-workflow__header">
        <div>
          <Typography.Title heading={4} style={{ margin: 0 }}>Workflow</Typography.Title>
          <Typography.Text type="tertiary">应用工作流列表与编辑入口</Typography.Text>
        </div>
        <Button
          type="primary"
          {...({ ["data-testid"]: "app-workflows-create" })}
          onClick={() => void api.createWorkflow().then(onOpenEditor)}
        >
          Create
        </Button>
      </div>
      <div className="module-workflow__surface">
        {items.length === 0 ? <Empty title="No workflows" image={null} /> : null}
        {items.map((item: WorkflowListItem) => (
          <article key={item.id} className="module-workflow__item" data-row-key={item.id}>
            <div>
              <strong>{item.name}</strong>
              <p>{item.updatedAt || item.code || "-"}</p>
            </div>
            <Button {...({ ["data-testid"]: `app-workflows-open-${item.id}` })} onClick={() => onOpenEditor(item.id)}>Open</Button>
          </article>
        ))}
      </div>
    </section>
  );
}

export function WorkflowEditorPage({
  api,
  workflowId,
  onBack
}: WorkflowPageProps & { workflowId: string; onBack: () => void }) {
  return (
    <section className="module-workflow__page" data-testid="app-workflow-editor-page">
      <WorkflowEditor workflowId={workflowId} locale="zh-CN" apiClient={api.apiClient} onBack={onBack} />
    </section>
  );
}
