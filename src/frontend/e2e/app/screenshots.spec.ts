import { expect, test } from "../fixtures/single-session";
import {
  captureEvidenceScreenshot,
  ensureAppSetup,
  navigateBySidebar
} from "./helpers";

test.describe.serial("App Screenshot E2E", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("should capture develop and users page screenshots in real browser", async ({ page }, testInfo) => {
    await navigateBySidebar(page, "develop", {
      pageTestId: "app-develop-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/space/[^/]+/develop(?:\\?.*)?$`)
    });
    await captureEvidenceScreenshot(page, testInfo, "develop-fullpage");

    await navigateBySidebar(page, "users", {
      pageTestId: "app-users-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/users(?:\\?.*)?$`)
    });
    await captureEvidenceScreenshot(page, testInfo, "users-fullpage");
  });
});

