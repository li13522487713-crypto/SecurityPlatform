import { Card, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../i18n";

export function ForbiddenPage() {
  const { t } = useAppI18n();

  return (
    <Card className="atlas-placeholder-card">
      <Typography.Title heading={3}>{t("forbiddenTitle")}</Typography.Title>
      <Typography.Text type="tertiary">{t("forbiddenDesc")}</Typography.Text>
    </Card>
  );
}
