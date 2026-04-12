import { Button, Card, Space, Typography } from "@douyinfe/semi-ui";
import { useNavigate } from "react-router-dom";
import { useAppI18n } from "../i18n";
import { useBootstrap } from "../bootstrap-context";

export function PlatformNotReadyPage() {
  const { t } = useAppI18n();
  const { refresh } = useBootstrap();

  return (
    <div className="atlas-centered-page">
      <Card className="atlas-status-card">
        <Typography.Title heading={2}>{t("platformNotReadyTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("platformNotReadyDesc")}</Typography.Text>
        <div className="atlas-form-stack">
          <Button type="primary" onClick={() => void refresh()}>{t("refresh")}</Button>
        </div>
      </Card>
    </div>
  );
}

export function AppSetupPage() {
  const { t } = useAppI18n();
  const { refresh } = useBootstrap();
  const navigate = useNavigate();

  return (
    <div className="atlas-centered-page">
      <Card className="atlas-status-card">
        <Typography.Title heading={2}>{t("appSetupTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("appSetupDesc")}</Typography.Text>
        <Space style={{ marginTop: 16 }}>
          <Button theme="light" onClick={() => navigate("/")}>{t("backHome")}</Button>
          <Button type="primary" onClick={() => void refresh()}>{t("refresh")}</Button>
        </Space>
      </Card>
    </div>
  );
}
