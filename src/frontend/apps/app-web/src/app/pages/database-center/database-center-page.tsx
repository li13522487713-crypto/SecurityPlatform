import { PageShell } from "../../_shared";
import { defaultDatabaseCenterLabels, type DatabaseCenterLabels } from "./database-center-labels";
import { DatabaseCenterShell } from "./database-center-shell";
import "./database-center.css";

export interface DatabaseCenterPageProps {
  workspaceId?: string;
  initialSourceId?: string;
  labels?: Partial<DatabaseCenterLabels>;
}

export function DatabaseCenterPage({ workspaceId, initialSourceId, labels }: DatabaseCenterPageProps) {
  return (
    <PageShell>
      <DatabaseCenterShell
        workspaceId={workspaceId}
        initialSourceId={initialSourceId}
        labels={{ ...defaultDatabaseCenterLabels, ...(labels ?? {}) }}
      />
    </PageShell>
  );
}
