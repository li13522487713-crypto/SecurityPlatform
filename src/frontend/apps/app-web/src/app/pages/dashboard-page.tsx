import { Button, Typography } from "@douyinfe/semi-ui";
import { useNavigate, useParams } from "react-router-dom";
import { useAppI18n } from "../i18n";
import { getAppRuntimeMode } from "@/services/api-core";

export function DashboardPage() {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const { appKey = "" } = useParams();

  return (
    <div className="atlas-dashboard">
      <div className="atlas-page-section">
        <Typography.Title heading={2}>{t("dashboardTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("dashboardSubtitle")}</Typography.Text>
      </div>

      <div className="atlas-metric-grid">
        <div className="atlas-metric-card">
          <span>{t("summaryLibrary")}</span>
          <strong>Library / Knowledge</strong>
        </div>
        <div className="atlas-metric-card">
          <span>{t("summaryKnowledge")}</span>
          <strong>Text / Table / Image</strong>
        </div>
        <div className="atlas-metric-card">
          <span>{t("summaryMode")}</span>
          <strong>{getAppRuntimeMode() === "direct" ? t("runtimeDirect") : t("runtimePlatform")}</strong>
        </div>
        <div className="atlas-metric-card">
          <span>{t("summaryModule")}</span>
          <strong>Library Module</strong>
        </div>
      </div>

      <div className="atlas-action-grid">
        <div className="atlas-action-card">
          <Typography.Title heading={4}>{t("dashboardGoLibrary")}</Typography.Title>
          <Button type="primary" onClick={() => navigate(`/apps/${encodeURIComponent(appKey)}/library`)}>
            {t("dashboardGoLibrary")}
          </Button>
        </div>
        <div className="atlas-action-card">
          <Typography.Title heading={4}>{t("dashboardGoAgents")}</Typography.Title>
          <Button onClick={() => navigate(`/apps/${encodeURIComponent(appKey)}/ai/agents`)}>
            {t("dashboardGoAgents")}
          </Button>
        </div>
        <div className="atlas-action-card">
          <Typography.Title heading={4}>{t("dashboardGoWorkflow")}</Typography.Title>
          <Button onClick={() => navigate(`/apps/${encodeURIComponent(appKey)}/workflows`)}>
            {t("dashboardGoWorkflow")}
          </Button>
        </div>
      </div>
    </div>
  );
}
