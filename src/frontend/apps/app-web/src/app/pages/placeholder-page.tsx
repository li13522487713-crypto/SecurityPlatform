import { Button, Card, Typography } from "@douyinfe/semi-ui";
import { useNavigate } from "react-router-dom";
import { useAppI18n } from "../i18n";

export function PlaceholderPage({ title }: { title: string }) {
  const { t } = useAppI18n();
  const navigate = useNavigate();

  return (
    <Card className="atlas-placeholder-card">
      <Typography.Title heading={3}>{title}</Typography.Title>
      <Typography.Text type="tertiary">{t("placeholderComingSoon")}</Typography.Text>
      <div style={{ marginTop: 16 }}>
        <Button onClick={() => navigate(-1)}>{t("placeholderBack")}</Button>
      </div>
    </Card>
  );
}

export function ForbiddenPage() {
  const { t } = useAppI18n();
  return (
    <Card className="atlas-placeholder-card">
      <Typography.Title heading={3}>{t("forbiddenTitle")}</Typography.Title>
      <Typography.Text type="tertiary">{t("forbiddenDesc")}</Typography.Text>
    </Card>
  );
}
