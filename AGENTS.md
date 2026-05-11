# AGENTS.md（精简执行版）

## 1. 适用范围与优先级

- 优先级：`AGENTS.md` > 当前代码/配置 > `docs/contracts.md`、`docs/plan-*.md` > 其他说明文档。
- 本仓库后端统一入口为 `src/backend/Atlas.AppHost`（常用端口 `5002`）；前端为 `src/frontend/apps/app-web`（端口按启动实际输出，常见 `5181`）。
- 禁止依赖过时路径与历史宿主逻辑（如重复启动旧 `Atlas.WebApi`/旧宿主）。
- 所有回答使用中文。

## 2. 开始前（必须做）

1. 明确问题现象、影响面、预期结果。
2. 查找真实入口与调用链（`rg` 为主）。
3. 形成最小复现路径，先做最小修复，不先扩需求。
4. 明确验证计划：build/test/API/E2E 是否需要。

## 3. 变更与闭环规则（硬约束）

每个任务必须至少完成：
- 自审：需求是否满足、是否有回归风险、权限与安全风险。
- 验证：最小必要命令/测试/实测。
- 规则对照：对照本文 + 文档契约。

未完成闭环不得宣告“完成”。

最小最低线（按类型）：
- 后端/API：`dotnet build`（最小影响范围）+ 定向 `dotnet test`。
- 前端页面/组件：`pnpm run build:app-web` + 必要的 `pnpm run test:unit`。
- 前后端联动：上述全部 + API 实测 + Playwright 关键用例。

## 4. 后端启动与重启（5002）

改过以下任何后端内容，必须重启：Controller、Service、Repository、Entity、DI、appsettings、迁移。

1. 检查监听与占用进程：
```powershell
Get-NetTCPConnection -LocalPort 5002 -State Listen -ErrorAction SilentlyContinue |
  Select-Object LocalAddress, LocalPort, OwningProcess
Get-Process Atlas.AppHost -ErrorAction SilentlyContinue | Select-Object Id, ProcessName
```
2. 停止旧进程（避免 DLL 锁）：
```powershell
$conns = Get-NetTCPConnection -LocalPort 5002 -State Listen -ErrorAction SilentlyContinue
if ($conns) {
  $conns | Select-Object -ExpandProperty OwningProcess -Unique |
    ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }
}
```
3. 重启：
```powershell
dotnet run -c Debug --project src/backend/Atlas.AppHost/Atlas.AppHost.csproj
```
4. 确认监听与日志：
```powershell
Get-NetTCPConnection -LocalPort 5002 -State Listen
Get-Content "$env:TEMP\securityplatform-service-logs\apphost.debug.out.log" -Tail 180
Get-Content "$env:TEMP\securityplatform-service-logs\apphost.debug.err.log" -Tail 180
```

注意：不要用 `$pid` 做循环变量名（PS `$PID` 只读）。

## 5. 前端启动与重启（app-web）

- `vite.config.*`、`.env`、`package.json`、pnpm workspace、代理配置、路由入口改动 → 必须重启。
- 普通组件改动可依赖 HMR，但 Playwright 前需确认刷新后无缓存。
- 启动：
```powershell
cd src/frontend
pnpm install
pnpm --filter app-web dev
```
- 启动前确认端口空闲：
```powershell
Get-NetTCPConnection -State Listen |
  Where-Object { $_.LocalPort -in 5173,5181,3000 } |
  Select-Object LocalAddress, LocalPort, OwningProcess
```

## 6. API 与联动验证（关键）

- 涉及真实页面，必须实测 API，不以 401 当业务失败；先确认登录态。
- 关键查询字段需做前后兼容对照（如 `moduleId=sales` 与 `moduleId=Sales` 同步验证）。
- Microflow/Mendix 相关问题必须单独打：`GET /api/v1/microflow-metadata...`，不能只看页面遮罩。
- 推荐顺序：`build` → `test` → 后端启动确认 → API 实测 → 前端刷新 → Playwright。

## 7. Playwright E2E（失败必看）

- 运行前确认：后端监听、前端运行、测试账号、接口可通。
- 示例：
```powershell
pnpm exec playwright test -c playwright.app.config.ts e2e/app/xxx.spec.ts
```
- 失败必须读：
`src/frontend/test-results/**/error-context.md`
- 复盘顺序：页面/登录/workspace/appId/API状态码/DOM/时机/测试数据/后端版本。

## 8. 强约束（简版）

- UI 使用 Semi 体系，文案走 i18n。
- API 使用显式版本前缀（如 `/api/v1`）。
- 后端契约变更同步更新：DTO、前端 client/mock、` .http`、测试、文档。
- 强调真实链路修复，不得只改 E2E 数据。

## 9. 严禁行为

1. 改后端不重启就页面验证。
2. 端口未重启继续跑测试。
3. 因 DLL 锁跳过 build/test。
4. 只看页面报错不查接口。
5. E2E 失败不读 `error-context.md`。
6. 改断言代替修复。
7. 使用 `$pid` 变量名。

## 10. 交付汇报模板（每次任务必填）

## 修改结果
- 已修复：
- 修改文件：
- 根因：
- 验证：
  - build：
  - test：
  - API 实测：
  - E2E：
- 重启：后端（已重启/不需要）、前端（已重启/不需要）
- 风险与下一步：

## 执行约束（补充）

- 不得以“最小 MVP/临时替代/降级实现”作为默认交付路径。
- 需求必须按全局架构完整实现闭环，除非用户明确要求“PoC”或“临时降级”。
- 禁止省略关键架构链路、契约同步、前后端联动、回归验证或权限/安全控制。

## E2E 测试重启与问题判定（新增执行约束）

- 不允许盲目重启后端/前端。必须先从架构链路判断是否“代码是否已变更且链路受影响”。
- Playwright 报错时，先按顺序判断：
  1) 是否在正确的前后端版本下运行（端口监听/进程、启动日志、页面源码时间戳/URL）
  2) 调用链是否命中：URL → Route → 前端状态(store) → API Adapter → Controller → Service → Repository → DB → 响应
  3) 后端是否只改了前端可不需重启（仅前端源码变更且未改服务端接口）
  4) 前端是否需重启（vite/config、.env、package.json、proxy、路由入口、bootstrap、依赖版本变更）
  5) 后端是否需重启（Controller/Service/Repository/Entity/DI/appsettings/migration/build 相关改动）
  6) 是否为测试数据/登录态/环境问题（401/403、fixture、storageState）
- 命中上述项后再执行必要重启，优先最小范围重启：
  - 仅前端逻辑问题：先刷新页面/清理上下文后重跑 E2E；
  - 仅服务端链路变更：重启 AppHost（必要时先停止旧监听）后再验证；
  - 双端都变更：按“先后端后前端”顺序重启。
- 禁止“重启→小补丁→重跑”循环：每次修复后必须先补齐根因归因、补单测/回归，再做架构层面的下一步修正。

## 执行节奏约束（新增）

- 三思而后行：每次动代码前，先完整回顾问题链路与影响面，至少形成一次“为什么会这样、是否还有更上游原因、是否会引入新回归”的思考后再修改。
- 禁止冲动式修复：禁止看到报错立即下补丁；先从架构链路定位根因，再决定是否需要修改。
- 除非确认链路结论已明确，不得直接执行大规模改动或多次重复修补。
