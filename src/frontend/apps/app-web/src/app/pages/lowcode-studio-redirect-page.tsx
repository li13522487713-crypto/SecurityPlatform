import { useEffect } from "react";
import { PageShell } from "../_shared";
import { navigateToLowcodeStudio } from "../navigation/lowcode-studio-navigator";

export function LowcodeStudioRedirectPage({ appId }: { appId: string }) {
  useEffect(() => {
    if (!appId) {
      return;
    }
    navigateToLowcodeStudio(appId);
  }, [appId]);

  return <PageShell loading testId="coze-lowcode-redirect-loading" />;
}
