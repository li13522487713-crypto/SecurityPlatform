import { Button, Card, Input, Space, Toast, Typography } from "@douyinfe/semi-ui";
import { useState } from "react";
import { useNavigate, useSearchParams, useParams } from "react-router-dom";
import { useAuth } from "../auth-context";
import { useAppI18n } from "../i18n";

export function LoginPage() {
  const { appKey = "" } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { login, loading } = useAuth();
  const { t } = useAppI18n();
  const [tenantId, setTenantId] = useState("00000000-0000-0000-0000-000000000001");
  const [username, setUsername] = useState("admin");
  const [password, setPassword] = useState("P@ssw0rd!");

  return (
    <div className="atlas-centered-page">
      <Card className="atlas-login-card">
        <Typography.Title heading={2}>{t("loginTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("loginSubtitle")}</Typography.Text>
        <div className="atlas-form-stack">
          <Input value={tenantId} placeholder={t("tenantId")} onChange={setTenantId} />
          <Input value={username} placeholder={t("username")} onChange={setUsername} />
          <Input value={password} type="password" placeholder={t("password")} onChange={setPassword} />
          <Space>
            <Button theme="light" onClick={() => navigate("/")}>{t("backHome")}</Button>
            <Button
              type="primary"
              loading={loading}
              onClick={async () => {
                try {
                  await login(appKey, tenantId, username, password);
                  navigate(searchParams.get("redirect") || `/apps/${encodeURIComponent(appKey)}/dashboard`, {
                    replace: true
                  });
                } catch (error) {
                  Toast.error((error as Error).message);
                }
              }}
            >
              {t("login")}
            </Button>
          </Space>
        </div>
      </Card>
    </div>
  );
}
