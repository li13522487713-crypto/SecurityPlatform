import { Banner, Button, Space, Typography } from "@douyinfe/semi-ui";
import { useNavigate, useParams } from "react-router-dom";
import { workspaceDevelopPath } from "@atlas/app-shell-shared";
import { useBootstrap } from "../bootstrap-context";

export function EntryGatewayPage() {
  const { appKey = "" } = useParams();
  const navigate = useNavigate();
  const bootstrap = useBootstrap();

  return (
    <section className="module-admin__page" data-testid="app-entry-gateway-page">
      <div className="module-admin__page-header">
        <div>
          <Typography.Title heading={4} style={{ margin: 0 }}>
            Entry Gateway
          </Typography.Title>
          <Typography.Text type="tertiary">
            当前应用级运行态入口尚未切换到独立 runtime shell，先给出稳定回退提示。
          </Typography.Text>
        </div>
      </div>
      <div className="module-admin__surface">
        <Banner
          type="warning"
          data-testid="app-entry-gateway-warning"
          title="Runtime entry is not available yet"
          description="请先从应用工作空间进入开发台、资源库或管理页。"
        />
        <Space style={{ marginTop: 16 }}>
          <Button onClick={() => navigate(workspaceDevelopPath(appKey, bootstrap.spaceId))}>
            打开工作空间
          </Button>
        </Space>
      </div>
    </section>
  );
}
