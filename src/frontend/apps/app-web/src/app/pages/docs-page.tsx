import { Button, Typography } from "@douyinfe/semi-ui";
import { useNavigate, useParams } from "react-router-dom";
import { useAppI18n } from "../i18n";
import {
  workspaceHomePath,
  workspaceProjectsPath,
  workspaceResourcesPath
} from "@atlas/app-shell-shared";
import { useWorkspaceContext } from "../workspace-context";

/**
 * 文档中心 - `/docs[/:slug]`。
 *
 * 默认（无 slug 或 slug == "welcome"）展示一组快速入口卡 + 跳转外部文档；
 * 其它 slug 暂时复用同一组占位（第二批不接入完整文档系统）。
 */
export function DocsPage() {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const params = useParams<{ slug?: string }>();
  const workspace = useWorkspaceContext();

  const isWelcome = !params.slug || params.slug === "welcome";

  if (!isWelcome) {
    return (
      <div className="coze-page" data-testid="coze-docs-page">
        <header className="coze-page__header">
          <Typography.Title heading={3} style={{ margin: 0 }}>{t("cozeDocsTitle")}</Typography.Title>
          <Typography.Text type="tertiary">{t("cozeDocsSubtitle")}</Typography.Text>
        </header>
        <section className="coze-page__body">
          <Typography.Paragraph>{t("cozeCommonComingSoon")}</Typography.Paragraph>
        </section>
      </div>
    );
  }

  return (
    <div className="coze-page" data-testid="coze-docs-page">
      <header className="coze-page__header">
        <Typography.Title heading={3} style={{ margin: 0 }}>{t("cozeDocsWelcomeTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("cozeDocsWelcomeSubtitle")}</Typography.Text>
      </header>

      <section className="coze-card-grid coze-card-grid--3">
        <button
          type="button"
          className="coze-tutorial-card"
          onClick={() => navigate(workspaceHomePath(workspace.id || ""))}
          data-testid="coze-docs-card-overview"
        >
          <div className="coze-tutorial-card__icon" aria-hidden>?</div>
          <strong>{t("cozeDocsWelcomeCardOverview")}</strong>
          <span>{t("cozeDocsWelcomeCardOverviewDesc")}</span>
        </button>
        <button
          type="button"
          className="coze-tutorial-card"
          onClick={() => navigate(workspaceProjectsPath(workspace.id || ""))}
          data-testid="coze-docs-card-quickstart"
        >
          <div className="coze-tutorial-card__icon" aria-hidden>{">"}</div>
          <strong>{t("cozeDocsWelcomeCardQuickStart")}</strong>
          <span>{t("cozeDocsWelcomeCardQuickStartDesc")}</span>
        </button>
        <button
          type="button"
          className="coze-tutorial-card"
          onClick={() => navigate(workspaceResourcesPath(workspace.id || "", "workflows"))}
          data-testid="coze-docs-card-workflow"
        >
          <div className="coze-tutorial-card__icon" aria-hidden>W</div>
          <strong>{t("cozeDocsWelcomeCardWorkflow")}</strong>
          <span>{t("cozeDocsWelcomeCardWorkflowDesc")}</span>
        </button>
      </section>

      <footer className="coze-page__footer">
        <Button
          theme="solid"
          type="primary"
          onClick={() => window.open("https://www.coze.cn/docs", "_blank", "noreferrer")}
        >
          {t("cozeDocsOpenWelcome")}
        </Button>
      </footer>
    </div>
  );
}
