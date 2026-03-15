import { test as base, expect, type Page } from "@playwright/test";
import type { E2ERole } from "../catalog/seed-types";
import { readAuthState } from "../helpers/auth-state";
import { applyStoredSessionState } from "../helpers/test-helpers";

type AuthFixture = {
  loginAsSuperAdmin: () => Promise<void>;
  loginAsSysAdmin: () => Promise<void>;
  loginAsSecurityAdmin: () => Promise<void>;
  loginAsApprovalAdmin: () => Promise<void>;
  loginAsAiAdmin: () => Promise<void>;
  loginAsAppAdmin: () => Promise<void>;
  loginAsDeptAdminA: () => Promise<void>;
  loginAsDeptAdminB: () => Promise<void>;
  loginAsReadonly: () => Promise<void>;
  loginAsUserA: () => Promise<void>;
  loginAsUserB: () => Promise<void>;
  loginAsAdmin: () => Promise<void>;
};

async function applyRoleState(page: Page, role: E2ERole) {
  const state = readAuthState(role);
  await page.context().clearCookies();
  if (state.cookies.length > 0) {
    await page.context().addCookies(state.cookies);
  }
  await applyStoredSessionState(page, {
    localStorage: state.localStorage,
    sessionStorage: state.sessionStorage
  });
  await page.goto(state.homePath);
  await page.waitForLoadState("networkidle");
  return state;
}

function createLoginFixture(role: E2ERole) {
  return async ({ page }: { page: Page }, use: (value: () => Promise<void>) => Promise<void>) => {
    await use(async () => {
      const state = await applyRoleState(page, role);
      const pattern = new RegExp(state.homePath.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"));
      await expect(page).toHaveURL(pattern);
    });
  };
}

export const test = base.extend<AuthFixture>({
  loginAsSuperAdmin: createLoginFixture("superadmin"),
  loginAsSysAdmin: createLoginFixture("sysadmin"),
  loginAsSecurityAdmin: createLoginFixture("securityadmin"),
  loginAsApprovalAdmin: createLoginFixture("approvaladmin"),
  loginAsAiAdmin: createLoginFixture("aiadmin"),
  loginAsAppAdmin: createLoginFixture("appadmin"),
  loginAsDeptAdminA: createLoginFixture("deptadminA"),
  loginAsDeptAdminB: createLoginFixture("deptadminB"),
  loginAsReadonly: createLoginFixture("readonly"),
  loginAsUserA: createLoginFixture("userA"),
  loginAsUserB: createLoginFixture("userB"),
  loginAsAdmin: createLoginFixture("superadmin")
});

export { expect };
