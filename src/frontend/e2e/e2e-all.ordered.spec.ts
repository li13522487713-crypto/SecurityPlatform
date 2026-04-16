// Aggregator spec for deterministic execution order in single-session mode.
// setup -> post-setup auth -> app regression suite.
import "./setup/setup.spec";
import "./setup/z-post-setup-auth.spec";

import "./app/auth-and-routing.spec";
import "./app/console-workspace-workflow.smoke.spec";
import "./app/navigation.spec";
import "./app/studio-dashboard.spec";
import "./app/model-configs.spec";
import "./app/app-builder.spec";
import "./app/publish-center.spec";
import "./app/agent-workbench.spec";
import "./app/approval.spec";
import "./app/departments-positions.spec";
import "./app/roles.spec";
import "./app/users.spec";
import "./app/reports-dashboards.spec";
import "./app/settings-and-maintenance.spec";
import "./app/visualization-and-runtime.spec";
import "./app/screenshots.spec";
import "./app/workflow-orchestration.spec";
import "./app/workflow-editor.spec";
import "./app/workflow-collab.spec";
import "./app/workflow-publish.spec";
import "./app/workflow-run.spec";
import "./app/workflow-complete-flow.spec";
import "./app/workflow-v2-acceptance.spec";
