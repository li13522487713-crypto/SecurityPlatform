import { expect, type Locator, type Page, test } from "@playwright/test";
import { loadSeedState } from "../helpers/test-helpers";

const seedState = loadSeedState();
const sysadmin = seedState.accounts.sysadmin;
const password = process.env.E2E_TEST_PASSWORD ?? "P@ssw0rd!";

async function humanPause(page: Page, ms = 400) {
  await page.waitForTimeout(ms);
}

async function humanClick(page: Page, target: Locator, pause = 350) {
  await target.scrollIntoViewIfNeeded();
  await expect(target).toBeVisible();
  await target.click();
  await humanPause(page, pause);
}

async function humanType(page: Page, target: Locator, value: string, pause = 120) {
  await target.scrollIntoViewIfNeeded();
  await expect(target).toBeVisible();
  await target.click();
  await target.press(process.platform === "darwin" ? "Meta+A" : "Control+A");
  await target.press("Backspace");
  await target.pressSequentially(value, { delay: 60 });
  await humanPause(page, pause);
}

async function fillFormItem(page: Page, drawer: Locator, label: string, value: string) {
  const item = drawer.locator(".ant-form-item").filter({ hasText: label }).first();
  await expect(item).toBeVisible();

  const textarea = item.locator("textarea").first();
  if ((await textarea.count()) > 0) {
    await humanType(page, textarea, value);
    return;
  }

  const input = item.locator("input").first();
  await humanType(page, input, value);
}

async function uiLogin(page: Page) {
  await page.context().clearCookies();
  await page.addInitScript(() => {
    localStorage.clear();
    sessionStorage.clear();
  });
  await page.goto("/login");
  await page.waitForLoadState("domcontentloaded");

  await expect(page.getByText("账号登录")).toBeVisible();
  await humanType(page, page.getByPlaceholder("手机号 / 邮箱 / 用户名"), sysadmin.username);
  await humanType(page, page.getByPlaceholder("请输入密码"), password);
  await humanClick(page, page.getByRole("button", { name: "登录" }), 800);

  await expect(page).toHaveURL(/settings\/org\/users|system\/notifications|console/);
}

async function openSystemMenu(page: Page, menuTestId: string, expectedUrl: RegExp) {
  await humanClick(page, page.getByTestId("e2e-menu-system"));
  await humanClick(page, page.getByTestId(menuTestId), 700);
  await expect(page).toHaveURL(expectedUrl);
  await expect(page.getByTestId("e2e-crud-toolbar")).toBeVisible();
}

async function searchKeyword(page: Page, keyword: string) {
  await humanType(page, page.getByTestId("e2e-crud-search-input"), keyword);
  await humanClick(page, page.getByTestId("e2e-crud-search-submit"), 700);
}

async function openCreate(page: Page, buttonName: string) {
  await humanClick(page, page.getByRole("button", { name: buttonName }));
  const drawer = page.locator(".ant-drawer-open").last();
  await expect(drawer).toBeVisible();
  return drawer;
}

async function openEdit(page: Page, rowKeyword: string) {
  const row = page.locator(".ant-table-tbody tr").filter({ hasText: rowKeyword }).first();
  await expect(row).toBeVisible();
  await humanClick(page, row.getByRole("button", { name: "编辑" }));
  const drawer = page.locator(".ant-drawer-open").last();
  await expect(drawer).toBeVisible();
  return { row, drawer };
}

async function deleteRow(page: Page, rowKeyword: string) {
  const row = page.locator(".ant-table-tbody tr").filter({ hasText: rowKeyword }).first();
  await expect(row).toBeVisible();
  await humanClick(page, row.getByRole("button", { name: "删除" }));

  const confirm = page.locator(".ant-popconfirm-buttons").getByRole("button", { name: "删除" }).last();
  await humanClick(page, confirm, 700);
  await expect(page.getByText("删除成功")).toBeVisible();
}

test.describe("可视化人工路径 CRUD", () => {
  test.setTimeout(10 * 60 * 1000);

  test("sysadmin 通过菜单执行角色/职位/项目 CRUD", async ({ page }) => {
    const suffix = Date.now().toString();

    await uiLogin(page);

    const roleName = `GUI角色${suffix}`;
    const roleEditedName = `${roleName}-改`;
    const roleCode = `GUI_ROLE_${suffix}`;

    await openSystemMenu(page, "e2e-menu-settings-auth-roles", /settings\/auth\/roles/);
    let drawer = await openCreate(page, "新增角色");
    await fillFormItem(page, drawer, "角色名称", roleName);
    await fillFormItem(page, drawer, "角色编码", roleCode);
    await fillFormItem(page, drawer, "描述", "Playwright 可视化 CRUD 角色");
    await humanClick(page, page.getByTestId("e2e-crud-drawer-submit"), 900);
    await expect(page.getByText("创建成功")).toBeVisible();

    await searchKeyword(page, roleName);
    ({ drawer } = await openEdit(page, roleName));
    await fillFormItem(page, drawer, "角色名称", roleEditedName);
    await humanClick(page, page.getByTestId("e2e-crud-drawer-submit"), 900);
    await expect(page.getByText("更新成功")).toBeVisible();

    await searchKeyword(page, roleEditedName);
    await deleteRow(page, roleEditedName);
    await searchKeyword(page, roleEditedName);
    await expect(page.locator(".ant-table-tbody tr").filter({ hasText: roleEditedName })).toHaveCount(0);

    const positionName = `GUI职位${suffix}`;
    const positionEditedName = `${positionName}-改`;
    const positionCode = `GUI_POS_${suffix}`;

    await openSystemMenu(page, "e2e-menu-settings-org-positions", /settings\/org\/positions/);
    drawer = await openCreate(page, "新增职位");
    await fillFormItem(page, drawer, "职位名称", positionName);
    await fillFormItem(page, drawer, "编码", positionCode);
    await fillFormItem(page, drawer, "描述", "Playwright 可视化 CRUD 职位");
    await humanClick(page, page.getByTestId("e2e-crud-drawer-submit"), 900);
    await expect(page.getByText("创建成功")).toBeVisible();

    await searchKeyword(page, positionName);
    ({ drawer } = await openEdit(page, positionName));
    await fillFormItem(page, drawer, "职位名称", positionEditedName);
    await humanClick(page, page.getByTestId("e2e-crud-drawer-submit"), 900);
    await expect(page.getByText("更新成功")).toBeVisible();

    await searchKeyword(page, positionEditedName);
    await deleteRow(page, positionEditedName);
    await searchKeyword(page, positionEditedName);
    await expect(page.locator(".ant-table-tbody tr").filter({ hasText: positionEditedName })).toHaveCount(0);

    const projectName = `GUI项目${suffix}`;
    const projectEditedName = `${projectName}-改`;
    const projectCode = `GUI_PROJ_${suffix}`;

    await openSystemMenu(page, "e2e-menu-settings-projects", /settings\/projects/);
    drawer = await openCreate(page, "新增项目");
    await fillFormItem(page, drawer, "项目编码", projectCode);
    await fillFormItem(page, drawer, "项目名称", projectName);
    await fillFormItem(page, drawer, "描述", "Playwright 可视化 CRUD 项目");
    await humanClick(page, page.getByTestId("e2e-crud-drawer-submit"), 900);
    await expect(page.getByText("创建成功")).toBeVisible();

    await searchKeyword(page, projectName);
    ({ drawer } = await openEdit(page, projectName));
    await fillFormItem(page, drawer, "项目名称", projectEditedName);
    await humanClick(page, page.getByTestId("e2e-crud-drawer-submit"), 900);
    await expect(page.getByText("更新成功")).toBeVisible();

    await searchKeyword(page, projectEditedName);
    await deleteRow(page, projectEditedName);
    await searchKeyword(page, projectEditedName);
    await expect(page.locator(".ant-table-tbody tr").filter({ hasText: projectEditedName })).toHaveCount(0);
  });
});
