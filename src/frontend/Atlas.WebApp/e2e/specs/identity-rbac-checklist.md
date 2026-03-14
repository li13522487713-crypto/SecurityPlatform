# Playwright RBAC 用例清单

> 本清单用于将 RBAC、菜单权限、功能权限、数据范围、部门权限等场景，直接拆分为可落地的 Playwright `spec` 文件与测试用例。
> 目标是替换当前过于轻量的 `identity-rbac.spec.ts`，形成可持续维护的 RBAC E2E 套件。

---

## 1. 推荐拆分结构

| 文件 | 目标 | 优先级 |
|---|---|---|
| `e2e/specs/rbac/rbac-menu-visibility.spec.ts` | 多账号菜单可见性与隐藏校验 | P0 |
| `e2e/specs/rbac/rbac-route-guard.spec.ts` | 无权限 URL 直访与重定向校验 | P0 |
| `e2e/specs/rbac/rbac-role-assignment.spec.ts` | 角色、菜单、功能权限分配与回归 | P0 |
| `e2e/specs/rbac/rbac-data-scope.spec.ts` | 角色数据范围与部门权限校验 | P0 |
| `e2e/specs/rbac/rbac-user-assignment.spec.ts` | 给用户分配角色、部门、职位后的权限变化 | P1 |
| `e2e/specs/rbac/rbac-readonly-actions.spec.ts` | 只读角色的按钮与写操作禁用校验 | P1 |
| `e2e/specs/rbac/rbac-cross-account-regression.spec.ts` | 权限调整后刷新、重登、旧标签页回归 | P1 |

## 2. 建议补充的共享能力

### 2.1 Fixture

建议扩展现有 `e2e/fixtures/auth.fixture.ts`，至少增加：

- `loginAs(accountKey: string)`
- `loginAsSuperAdmin()`
- `loginAsSysAdmin()`
- `loginAsDeptAdminA()`
- `loginAsReadonlyUser()`
- `logout()`

### 2.2 测试账号映射

建议通过环境变量统一管理：

| 环境变量 | 含义 |
|---|---|
| `E2E_SUPERADMIN_USERNAME` | 平台超级管理员账号 |
| `E2E_SUPERADMIN_PASSWORD` | 平台超级管理员密码 |
| `E2E_SYSADMIN_USERNAME` | 系统管理员账号 |
| `E2E_SYSADMIN_PASSWORD` | 系统管理员密码 |
| `E2E_DEPTADMIN_A_USERNAME` | 部门管理员 A 账号 |
| `E2E_DEPTADMIN_A_PASSWORD` | 部门管理员 A 密码 |
| `E2E_DEPTADMIN_B_USERNAME` | 部门管理员 B 账号 |
| `E2E_DEPTADMIN_B_PASSWORD` | 部门管理员 B 密码 |
| `E2E_READONLY_USERNAME` | 只读审计账号 |
| `E2E_READONLY_PASSWORD` | 只读审计密码 |
| `E2E_USER_A_USERNAME` | 普通用户 A |
| `E2E_USER_A_PASSWORD` | 普通用户 A 密码 |
| `E2E_USER_B_USERNAME` | 普通用户 B |
| `E2E_USER_B_PASSWORD` | 普通用户 B 密码 |

### 2.3 Page Helper

建议增加：

- `e2e/helpers/navigation.ts`
- `e2e/helpers/rbac.ts`
- `e2e/helpers/assertions.ts`

建议封装的通用动作：

- 打开角色管理页
- 搜索角色
- 打开“角色配置”抽屉
- 分配菜单
- 分配功能权限
- 设置数据范围
- 给角色绑定自定义部门
- 打开用户管理页
- 给用户分配角色
- 给用户分配部门
- 断言菜单存在/不存在
- 断言按钮存在/不存在
- 断言 URL 被重定向

## 3. 统一前置数据

### 3.1 组织结构

- `总部`
- `研发部`
- `研发一组`
- `研发二组`
- `安全运营部`
- `财务部`

### 3.2 角色

- `SuperAdmin`
- `Admin`
- `ApprovalAdmin`
- `SecurityAdmin`
- `AiAdmin`
- `AppAdmin`
- `DeptAdminA`
- `DeptAdminB`
- `ReadOnlyAuditor`

### 3.3 用户归属

| 账号 | 角色 | 部门 |
|---|---|---|
| `superadmin.e2e` | `SuperAdmin` | `总部` |
| `sysadmin.e2e` | `Admin` | `总部` |
| `deptadmin.a.e2e` | `DeptAdminA` | `研发部` |
| `deptadmin.b.e2e` | `DeptAdminB` | `安全运营部` |
| `readonly.e2e` | `ReadOnlyAuditor` | `总部` |
| `user.a.e2e` | 普通用户 | `研发一组` |
| `user.b.e2e` | 普通用户 | `研发二组` |

## 4. 文件级用例清单

## 4.1 `rbac-menu-visibility.spec.ts`

### `describe("RBAC 菜单可见性")`

| 用例 ID | 执行账号 | 核心步骤 | 断言 |
|---|---|---|---|
| RBAC-MENU-001 | `superadmin.e2e` | 登录后展开全部一级菜单 | 所有主菜单可见 |
| RBAC-MENU-002 | `sysadmin.e2e` | 登录后检查系统管理、运维监控 | 菜单可见 |
| RBAC-MENU-003 | `securityadmin.e2e` | 登录后检查安全中心 | 资产、审计、告警可见 |
| RBAC-MENU-004 | `securityadmin.e2e` | 检查系统管理、数据源、工作流设计 | 不可见 |
| RBAC-MENU-005 | `approvaladmin.e2e` | 登录后检查流程中心 | 流程定义、发起审批、待办等可见 |
| RBAC-MENU-006 | `readonly.e2e` | 登录后检查只读审计菜单 | 审计日志、登录日志、通知中心可见 |
| RBAC-MENU-007 | `readonly.e2e` | 检查系统配置、菜单管理、用户管理 | 不可见 |
| RBAC-MENU-008 | `deptadmin.a.e2e` | 检查员工管理、部门管理、通知中心 | 仅业务授权菜单可见 |
| RBAC-MENU-009 | `user.a.e2e` | 检查普通用户菜单 | 待办、通知、运行态入口可见 |
| RBAC-MENU-010 | `user.a.e2e` | 检查角色管理、菜单管理、数据源管理 | 不可见 |

## 4.2 `rbac-route-guard.spec.ts`

### `describe("RBAC 路由拦截")`

| 用例 ID | 执行账号 | 目标 URL | 断言 |
|---|---|---|---|
| RBAC-ROUTE-001 | `readonly.e2e` | `/settings/auth/roles` | 跳回 `/console` 或无权限 |
| RBAC-ROUTE-002 | `readonly.e2e` | `/settings/system/configs` | 跳回 `/console` 或无权限 |
| RBAC-ROUTE-003 | `deptadmin.a.e2e` | `/settings/system/datasources` | 无权限 |
| RBAC-ROUTE-004 | `securityadmin.e2e` | `/monitor/message-queue` | 无权限 |
| RBAC-ROUTE-005 | `approvaladmin.e2e` | `/settings/auth/menus` | 无权限 |
| RBAC-ROUTE-006 | `user.a.e2e` | `/settings/org/users` | 无权限 |
| RBAC-ROUTE-007 | 未登录用户 | `/settings/auth/roles` | 跳转 `/login?redirect=...` |
| RBAC-ROUTE-008 | `sysadmin.e2e` | `/console/datasources` | 可访问 |
| RBAC-ROUTE-009 | `approvaladmin.e2e` | `/process/manage/instances` | 可访问 |
| RBAC-ROUTE-010 | `superadmin.e2e` | 任意受限页 | 可访问 |

## 4.3 `rbac-role-assignment.spec.ts`

### `describe("角色菜单与功能权限分配")`

| 用例 ID | 执行账号 | 核心步骤 | 断言 |
|---|---|---|---|
| RBAC-ROLE-001 | `superadmin.e2e` | 创建 `ReadOnlyAuditor` 角色 | 创建成功 |
| RBAC-ROLE-002 | `superadmin.e2e` | 给 `ReadOnlyAuditor` 分配审计日志、登录日志、通知中心菜单 | 保存成功 |
| RBAC-ROLE-003 | `superadmin.e2e` | 给 `ReadOnlyAuditor` 分配对应查询 API 权限 | 保存成功 |
| RBAC-ROLE-004 | `superadmin.e2e` | 给 `user.a.e2e` 分配 `ReadOnlyAuditor` 角色 | 保存成功 |
| RBAC-ROLE-005 | `user.a.e2e` | 重新登录后检查菜单 | 审计日志、登录日志出现 |
| RBAC-ROLE-006 | `superadmin.e2e` | 从 `ReadOnlyAuditor` 回收“登录日志”菜单 | 保存成功 |
| RBAC-ROLE-007 | `user.a.e2e` | 重新登录 | 登录日志菜单消失 |
| RBAC-ROLE-008 | `superadmin.e2e` | 给 `DeptAdminA` 分配用户查询菜单但不分配写权限 | 保存成功 |
| RBAC-ROLE-009 | `deptadmin.a.e2e` | 进入员工管理 | 页面可打开，但新增/删除按钮隐藏 |
| RBAC-ROLE-010 | `superadmin.e2e` | 给 `DeptAdminA` 增加 `UsersCreate` 权限 | 保存成功 |
| RBAC-ROLE-011 | `deptadmin.a.e2e` | 重新登录后进入员工管理 | 新增按钮出现 |
| RBAC-ROLE-012 | `superadmin.e2e` | 仅分配菜单不给 API 权限 | 菜单出现但核心写操作失败或按钮被隐藏 |

## 4.4 `rbac-data-scope.spec.ts`

### `describe("角色数据范围与部门权限")`

| 用例 ID | 执行账号 | 核心步骤 | 断言 |
|---|---|---|---|
| RBAC-SCOPE-001 | `superadmin.e2e` | 给 `DeptAdminA` 设置数据范围为“自定义部门” | 保存成功 |
| RBAC-SCOPE-002 | `superadmin.e2e` | 为 `DeptAdminA` 选择 `研发部`、`研发一组` | 保存成功并回显 |
| RBAC-SCOPE-003 | `deptadmin.a.e2e` | 登录后打开员工管理 | 只能看到 `研发部`、`研发一组` 用户 |
| RBAC-SCOPE-004 | `deptadmin.a.e2e` | 搜索 `user.b.e2e` | 搜索无结果 |
| RBAC-SCOPE-005 | `superadmin.e2e` | 将 `DeptAdminA` 自定义部门改为 `研发二组` | 保存成功 |
| RBAC-SCOPE-006 | `deptadmin.a.e2e` | 重新登录后查询员工 | 可见 `研发二组`，不可见 `研发一组` |
| RBAC-SCOPE-007 | `superadmin.e2e` | 给 `DeptAdminB` 设置数据范围为“本部门” | 保存成功 |
| RBAC-SCOPE-008 | `deptadmin.b.e2e` | 查询员工 | 仅看到 `安全运营部` 用户 |
| RBAC-SCOPE-009 | `superadmin.e2e` | 给 `DeptAdminB` 设置为“本部门及下级”并补一个下级部门 | 保存成功 |
| RBAC-SCOPE-010 | `deptadmin.b.e2e` | 重新登录并查询员工 | 能看到本部门及下级部门用户 |
| RBAC-SCOPE-011 | `superadmin.e2e` | 给 `ReadOnlyAuditor` 设置“仅本人” | 保存成功 |
| RBAC-SCOPE-012 | `readonly.e2e` | 查询员工或审计相关列表 | 仅显示本人或最小范围数据 |

## 4.5 `rbac-user-assignment.spec.ts`

### `describe("给用户分配角色、部门、职位后的权限变化")`

| 用例 ID | 执行账号 | 核心步骤 | 断言 |
|---|---|---|---|
| RBAC-USER-001 | `superadmin.e2e` | 创建测试用户 `temp.rbac.e2e` | 创建成功 |
| RBAC-USER-002 | `superadmin.e2e` | 给该用户分配 `DeptAdminA` 角色 | 保存成功 |
| RBAC-USER-003 | `superadmin.e2e` | 给该用户分配 `研发部` | 保存成功 |
| RBAC-USER-004 | `temp.rbac.e2e` | 登录查看菜单 | 仅看到 `DeptAdminA` 对应菜单 |
| RBAC-USER-005 | `superadmin.e2e` | 给该用户追加 `ReadOnlyAuditor` 角色 | 保存成功 |
| RBAC-USER-006 | `temp.rbac.e2e` | 重新登录 | 看到两类角色叠加后的菜单 |
| RBAC-USER-007 | `superadmin.e2e` | 移除 `DeptAdminA`，保留 `ReadOnlyAuditor` | 保存成功 |
| RBAC-USER-008 | `temp.rbac.e2e` | 重新登录 | 员工管理消失，只保留只读审计菜单 |
| RBAC-USER-009 | `superadmin.e2e` | 将用户部门从 `研发部` 改为 `财务部` | 保存成功 |
| RBAC-USER-010 | `temp.rbac.e2e` | 查询列表 | 数据范围按新部门生效 |

## 4.6 `rbac-readonly-actions.spec.ts`

### `describe("只读角色的写操作限制")`

| 用例 ID | 执行账号 | 页面 | 断言 |
|---|---|---|---|
| RBAC-READ-001 | `readonly.e2e` | 审计日志 | 无新增、编辑、删除按钮 |
| RBAC-READ-002 | `readonly.e2e` | 登录日志 | 无写操作按钮 |
| RBAC-READ-003 | `readonly.e2e` | 通知中心 | 仅允许已读，不允许公告发布 |
| RBAC-READ-004 | `readonly.e2e` | 直调新增 URL 或点击新增按钮位置 | 被拦截 |
| RBAC-READ-005 | `deptadmin.a.e2e` | 员工管理 | 若无 `UsersCreate`，则新增按钮不存在 |
| RBAC-READ-006 | `deptadmin.a.e2e` | 用户详情页 | 若无 `UsersUpdate`，保存按钮禁用或不可见 |

## 4.7 `rbac-cross-account-regression.spec.ts`

### `describe("权限变更后的刷新、重登、旧标签页回归")`

| 用例 ID | 场景 | 断言 |
|---|---|---|
| RBAC-REG-001 | 角色菜单调整后仅刷新页面 | 权限结果与重登一致 |
| RBAC-REG-002 | 角色权限调整后重新登录 | 菜单和按钮立即生效 |
| RBAC-REG-003 | 用户保留旧标签页后被回收权限 | 旧页刷新后不可继续操作 |
| RBAC-REG-004 | 两个浏览器上下文同时登录同账号 | 权限回收后两个上下文表现一致 |
| RBAC-REG-005 | 数据范围调整后不清缓存直接搜索 | 不应看到旧权限数据 |

## 5. 推荐 `describe` 组织方式

```ts
test.describe("RBAC - 菜单可见性", () => {
  test("RBAC-MENU-001 超级管理员可见全部菜单", async () => {});
  test("RBAC-MENU-004 安全管理员不可见系统配置菜单", async () => {});
});

test.describe("RBAC - 路由拦截", () => {
  test("RBAC-ROUTE-001 只读账号直访角色管理被拦截", async () => {});
});

test.describe("RBAC - 数据范围", () => {
  test("RBAC-SCOPE-003 自定义部门范围仅返回研发一组数据", async () => {});
});
```

## 6. 建议优先实现顺序

1. `rbac-menu-visibility.spec.ts`
2. `rbac-route-guard.spec.ts`
3. `rbac-role-assignment.spec.ts`
4. `rbac-data-scope.spec.ts`
5. `rbac-readonly-actions.spec.ts`
6. `rbac-cross-account-regression.spec.ts`
7. `rbac-user-assignment.spec.ts`

## 7. 最低落地集

如果只先做第一批高价值用例，建议至少实现以下 12 条：

- `RBAC-MENU-001`
- `RBAC-MENU-004`
- `RBAC-MENU-006`
- `RBAC-ROUTE-001`
- `RBAC-ROUTE-006`
- `RBAC-ROLE-004`
- `RBAC-ROLE-005`
- `RBAC-ROLE-009`
- `RBAC-SCOPE-001`
- `RBAC-SCOPE-003`
- `RBAC-SCOPE-006`
- `RBAC-REG-002`

## 8. 与现有文件的关系

- 现有 [identity-rbac.spec.ts](/E:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/e2e/specs/identity-rbac.spec.ts) 仅保留了最基础入口校验。
- 建议将该文件升级为目录入口，或改造成 `rbac-menu-visibility.spec.ts` 的第一批实现。
- RBAC 套件完成后，原有 `gui-tests.spec.ts` 中的身份与权限相关场景应逐步迁移出去，避免 GUI 大杂烩文件继续膨胀。

