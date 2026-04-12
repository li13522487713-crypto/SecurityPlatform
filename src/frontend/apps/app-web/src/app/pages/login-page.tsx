import { Button, Card, Input, Space, Typography } from "@douyinfe/semi-ui";
import { useState } from "react";
import { Navigate, useNavigate, useSearchParams, useParams } from "react-router-dom";
import { appSignPath, workspaceDevelopPath } from "../app-paths";
import { useAuth } from "../auth-context";
import { useAppI18n } from "../i18n";
import { useBootstrap } from "../bootstrap-context";

export function LoginPage() {
  const { appKey = "" } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { login, loading } = useAuth();
  const { t } = useAppI18n();
  const { appKey: configuredAppKey, spaceId } = useBootstrap();
  const [tenantId, setTenantId] = useState("00000000-0000-0000-0000-000000000001");
  const [username, setUsername] = useState("admin");
  const [password, setPassword] = useState("P@ssw0rd!");
  const [errorMessage, setErrorMessage] = useState("");

  if (configuredAppKey && configuredAppKey !== appKey) {
    return <Navigate to={appSignPath(configuredAppKey, searchParams.get("redirect") ?? undefined)} replace />;
  }

  return (
    <div className="atlas-centered-page" data-testid="app-login-page">
      <Card className="atlas-login-card">
        <Typography.Title heading={2}>{t("loginTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("loginSubtitle")}</Typography.Text>
        <div className="atlas-form-stack">
          <Input data-testid="app-login-tenant" value={tenantId} placeholder={t("tenantId")} onChange={setTenantId} />
          <Input data-testid="app-login-username" value={username} placeholder={t("username")} onChange={setUsername} />
          <Input data-testid="app-login-password" value={password} type="password" placeholder={t("password")} onChange={setPassword} />
          {errorMessage ? (
            <Typography.Text type="danger" className="login-error" data-testid="app-login-error">
              {errorMessage}
            </Typography.Text>
          ) : null}
          <Space>
            <Button theme="light" onClick={() => navigate("/")}>{t("backHome")}</Button>
            <Button
              data-testid="app-login-submit"
              type="primary"
              loading={loading}
              onClick={async () => {
                try {
                  setErrorMessage("");
                  await login(appKey, tenantId, username, password);
                  navigate(searchParams.get("redirect") || workspaceDevelopPath(appKey, spaceId), {
                    replace: true
                  });
                } catch (error) {
                  setErrorMessage(error instanceof Error ? error.message : t("loginFailed"));
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
