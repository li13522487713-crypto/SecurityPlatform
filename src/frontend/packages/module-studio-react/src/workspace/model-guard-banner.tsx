import React from "react";
import { Banner, Typography, Button } from "@douyinfe/semi-ui";
import { IconAlertTriangle } from "@douyinfe/semi-icons";
import type { StudioLocale } from "../types";
import { useStudioContext } from "../shared";
import { getStudioCopy } from "../copy";

export interface ModelGuardBannerProps {
  locale: StudioLocale;
  onConfigureModels?: () => void;
}

export function ModelGuardBanner({ locale, onConfigureModels }: ModelGuardBannerProps) {
  const { hasEnabledModel, modelConfigs } = useStudioContext();
  const copy = getStudioCopy(locale);

  if (hasEnabledModel) {
    return null;
  }

  const title = modelConfigs.length === 0 ? copy.modelGuard.noModelTitle : copy.modelGuard.noEnabledModelTitle;
  const description =
    modelConfigs.length === 0 ? copy.modelGuard.noModelDescription : copy.modelGuard.noEnabledModelDescription;

  return (
    <div style={{ marginBottom: 24 }}>
      <Banner
        type="warning"
        icon={<IconAlertTriangle />}
        title={<Typography.Title heading={6}>{title}</Typography.Title>}
        description={
          <div>
            <div style={{ marginBottom: 12 }}>{description}</div>
            {onConfigureModels && (
              <Button theme="solid" type="warning" onClick={onConfigureModels}>
                {copy.modelGuard.goToModelSettings}
              </Button>
            )}
          </div>
        }
      />
    </div>
  );
}
