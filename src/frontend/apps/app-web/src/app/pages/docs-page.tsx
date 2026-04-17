import { Button, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../i18n";

export function DocsPage() {
  const { t } = useAppI18n();
  return (
    <div className="coze-page" data-testid="coze-docs-page">
      <header className="coze-page__header">
        <Typography.Title heading={3} style={{ margin: 0 }}>{t("cozeDocsTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("cozeDocsSubtitle")}</Typography.Text>
      </header>
      <section className="coze-page__body">
        <Typography.Paragraph>
          {t("cozeDocsSubtitle")}
        </Typography.Paragraph>
        <Button
          theme="solid"
          type="primary"
          onClick={() => window.open("https://www.coze.cn/docs", "_blank", "noreferrer")}
        >
          {t("cozeDocsOpenWelcome")}
        </Button>
      </section>
    </div>
  );
}
