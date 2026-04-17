import { test } from "@playwright/test";

test("debug setup console rendering", async ({ page }) => {
  const errors: string[] = [];
  page.on("console", (msg) => {
    if (msg.type() === "error") {
      errors.push(msg.text());
    }
  });
  page.on("pageerror", (err) => {
    errors.push(`PAGE ERROR: ${err.message}`);
  });

  await page.goto("http://127.0.0.1:5181/setup-console", { waitUntil: "domcontentloaded" });
  await page.waitForTimeout(5000);

  const appHtml = await page.locator("#app").innerHTML();
  const truncated = appHtml.length > 3000 ? appHtml.slice(0, 3000) : appHtml;
  console.log("APP HTML:", truncated);
  console.log("ERRORS:", JSON.stringify(errors, null, 2));
  console.log("URL:", page.url());

  const allTestIds = await page.locator("[data-testid]").evaluateAll((nodes) =>
    nodes.map((n) => n.getAttribute("data-testid"))
  );
  console.log("TEST IDS:", JSON.stringify(allTestIds));
});
