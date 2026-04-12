import { Button, Card, Input, Toast, Typography } from "@douyinfe/semi-ui";
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAppI18n } from "../i18n";
import { useBootstrap } from "../bootstrap-context";

export function HomePage() {
  const { t } = useAppI18n();
  const { appKey, spaceId } = useBootstrap();
  const navigate = useNavigate();
  const [value, setValue] = useState(appKey);

  return (
    <div className="atlas-centered-page">
      <Card className="atlas-hero-card">
        <Typography.Title heading={2}>{t("homeTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("homeSubtitle")}</Typography.Text>
        <div className="atlas-form-stack">
          <Input
            value={value}
            placeholder={t("homeAppKeyPlaceholder")}
            onChange={setValue}
          />
          <Button
            type="primary"
            onClick={() => {
              if (!value.trim()) {
                Toast.warning(t("homeMissingAppKey"));
                return;
              }
              navigate(`/apps/${encodeURIComponent(value.trim())}/space/${encodeURIComponent(spaceId)}/develop`);
            }}
          >
            {t("homeEnter")}
          </Button>
        </div>
      </Card>
    </div>
  );
}
