import { useEffect, useState } from "react";
import { Button } from "@douyinfe/semi-ui";
import { PageShell, ResultCard } from "../_shared";
import { useAppI18n } from "../i18n";
import { navigateToLowcodeStudio } from "../navigation/lowcode-studio-navigator";

export function LowcodeStudioRedirectPage({ appId }: { appId: string }) {
  const { t } = useAppI18n();
  const [blockedTarget, setBlockedTarget] = useState<string | null>(null);

  useEffect(() => {
    if (!appId) {
      return;
    }
    setBlockedTarget(null);
    const result = navigateToLowcodeStudio(appId);
    if (!result.redirected) {
      setBlockedTarget(result.target);
    }
  }, [appId]);

  if (blockedTarget) {
    return (
      <PageShell centered maxWidth={720} testId="coze-lowcode-redirect-blocked">
        <ResultCard
          status="warning"
          title={t("lowcodeStudioRedirectBlockedTitle")}
          description={t("lowcodeStudioRedirectBlockedDescription").replace("{target}", blockedTarget)}
          actions={(
            <Button
              type="primary"
              theme="solid"
              onClick={() => {
                if (typeof window !== "undefined") {
                  window.location.assign(blockedTarget);
                }
              }}
            >
              {t("lowcodeStudioRedirectRetry")}
            </Button>
          )}
        />
      </PageShell>
    );
  }

  return <PageShell loading testId="coze-lowcode-redirect-loading" />;
}
