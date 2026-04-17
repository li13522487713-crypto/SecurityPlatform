import { Avatar, Button, Typography } from "@douyinfe/semi-ui";
import { useNavigate } from "react-router-dom";
import { meSettingsPath } from "@atlas/app-shell-shared";
import { useAppI18n } from "../i18n";
import { useAuth } from "../auth-context";

export function MeProfilePage() {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const auth = useAuth();
  const profile = auth.profile;

  return (
    <div className="coze-page coze-me-profile-page" data-testid="coze-me-profile-page">
      <section className="coze-me-profile-page__hero">
        <Avatar size="large" color="light-blue">
          {(profile?.displayName || profile?.username || "A").slice(0, 1).toUpperCase()}
        </Avatar>
        <div>
          <Typography.Title heading={3} style={{ margin: 0 }}>
            {profile?.displayName || profile?.username || "Atlas"}
          </Typography.Title>
          <Typography.Text type="tertiary">{t("cozeMeProfileSignaturePlaceholder")}</Typography.Text>
        </div>
        <div className="coze-me-profile-page__actions">
          <Button theme="light" onClick={() => navigate(meSettingsPath("account"))}>
            {t("cozeMeProfileEdit")}
          </Button>
          <Button theme="borderless" type="danger">
            {t("cozeMeProfileDeleteAccount")}
          </Button>
        </div>
      </section>

      <section className="coze-me-profile-page__panels">
        <div className="coze-me-profile-page__panel">
          <Typography.Text type="tertiary">{t("cozeMeProfileUidLabel")}</Typography.Text>
          <strong>{profile?.id ?? "-"}</strong>
        </div>
        <div className="coze-me-profile-page__panel">
          <Typography.Text type="tertiary">{t("cozeMeProfilePhoneLabel")}</Typography.Text>
          <Button theme="borderless" type="primary" onClick={() => navigate(meSettingsPath("account"))}>
            {t("cozeMeProfilePhoneView")}
          </Button>
        </div>
      </section>
    </div>
  );
}
