import { Banner, Button, Space, Typography } from "@douyinfe/semi-ui";
import { useNavigate, useParams } from "react-router-dom";
import { workspaceDevelopPath } from "@atlas/app-shell-shared";
import { useBootstrap } from "../bootstrap-context";
import { useAppI18n } from "../i18n";

export function EntryGatewayPage() {
  const { appKey = "" } = useParams();
  const navigate = useNavigate();
  const bootstrap = useBootstrap();
  const { t } = useAppI18n();

  return (
    <section className="module-admin__page" data-testid="app-entry-gateway-page">
      <div className="module-admin__page-header">
        <div>
          <Typography.Title heading={4} style={{ margin: 0 }}>
            {t("entryGatewayTitle")}
          </Typography.Title>
          <Typography.Text type="tertiary">
            {t("entryGatewaySubtitle")}
          </Typography.Text>
        </div>
      </div>
      <div className="module-admin__surface">
        <Banner
          type="warning"
          data-testid="app-entry-gateway-warning"
          title={t("entryGatewayWarningTitle")}
          description={t("entryGatewayWarningDescription")}
        />
        <Space style={{ marginTop: 16 }}>
          <Button onClick={() => navigate(workspaceDevelopPath(appKey, bootstrap.spaceId))}>
            {t("entryGatewayOpenWorkspace")}
          </Button>
        </Space>
      </div>
    </section>
  );
}
