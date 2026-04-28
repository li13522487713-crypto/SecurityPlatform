# Microflow E2E 验收清单

> 本清单对齐用户 §12 30 步流程，覆盖从登录、应用入口、画布操作、保存、运行、调用、引用保护
> 到 trace 高亮、控制台无 unhandled rejection 的全链路验收。新文件，独立于历史
> [`microflow-release-e2e-checklist.md`](microflow-release-e2e-checklist.md)（保留作为历史
> 发布版本的参考）。

## 0. 前置条件

| 条目 | 期望 |
|------|------|
| 后端启动 | `dotnet run --project src/backend/Atlas.AppHost` 端口 5002 |
| 前端启动 | `pnpm --filter atlas-app-web run dev:app-web` 端口 5181 |
| 浏览器路径 | `http://localhost:5181/space/<workspaceId>/mendix-studio/<appId>` |
| 登录账号 | 默认管理员 `admin` / `P@ssw0rd!`，租户 `00000000-0000-0000-0000-000000000001` |
| 数据库 | `src/backend/Atlas.AppHost/atlas.app.e2e.db`，`DatabaseInitializerHostedService` 自动建表 |

## 1. 验收 30 步

| # | 步骤 | 期望结果 |
|---|------|----------|
| 1 | 浏览器打开目标 URL，登录后进入 Mendix Studio | URL 命中 `MendixStudioApp`；左侧 App Explorer 加载 |
| 2 | 在 App Explorer 展开 Microflows 节点 | 真实微流列表来自 `GET /api/v1/microflows`；不再显示 `MF_*` 演示数据 |
| 3 | 在 Microflows 上点击「新建」，名称填 `MF_ValidatePurchaseRequest` | 列表出现新条目，工作台自动打开 Tab，状态徽标显示「草稿」 |
| 4 | 工作台 Tab 区出现该微流，画布与节点工具箱可见 | `MicroflowResourceEditorHost` 走 HTTP 真实 schema；节点工具箱显示完整分类 |
| 5 | 节点工具箱完整分类全部存在 | Events / Gateways / Decisions / Variables / Objects / Lists / Integration / Loop / Annotation / Other |
| 6 | 拖入 Start 节点 | 画布出现 startEvent；schema.objectCollection.objects 增加一条 |
| 7 | 拖入 Create Variable 节点，命名 `amount` | actionActivity 加入；属性面板显示 Create Variable 表单 |
| 8 | 拖入 Decision 节点 | 画布出现菱形决策节点 |
| 9 | 拖入两个 End 节点 (`endTrue` / `endFalse`) | endEvent x2，分别带不同 caption |
| 10 | 在参数面板新增 `amount: Number` 参数 | schema.parameters 更新；Toolbox 中可在表达式引用 `$amount` |
| 11 | 在 Decision 表达式输入 `amount > 100` | `MicroflowExpressionEvaluator` 在 testRun 时识别 |
| 12 | 把 Decision 的 true 边连到 endTrue、false 边连到 endFalse | flowCollection 更新；caseValues 写入布尔字面量 |
| 13 | 顶部 Workbench Toolbar 点击「保存」 | 调 `PUT /api/v1/microflows/{id}/schema`；状态徽标变为「已保存」 |
| 14 | 刷新页面 | `GET /api/v1/microflows/{id}/schema` 重新加载；节点 / 连线 / 参数 / 表达式都恢复 |
| 15 | 在 App Explorer 创建第二个微流 `MF_SubmitPurchaseRequest` | 两个 Tab 互相独立 |
| 16 | 在新微流中拖入 Call Microflow 节点 | actionActivity (`callMicroflow`) 加入 |
| 17 | 在属性面板下拉选择 target `MF_ValidatePurchaseRequest` | 目标列表来自 `GET /api/v1/microflow-metadata/microflows`；不再 mock |
| 18 | 配置参数映射：amount -> `$amount` | `parameterMappings` 写入 schema |
| 19 | 保存第二个微流 | `PUT /api/v1/microflows/{id}/schema` 成功 |
| 20 | 同时打开两个微流 Tab，dirty 状态分别显示 | `*` 前缀 + Tag 颜色更新 |
| 21 | 关闭一个 dirty Tab | Modal 弹出「Save / Discard / Cancel」三选项 |
| 22 | 点击 Save | 派发 `atlas:microflow-save-request`；保存成功后关闭 Tab |
| 23 | 在 `MF_SubmitPurchaseRequest` 工具栏点击「运行」 | 弹出 input 模态：amount 输入框 |
| 24 | amount 输入 `120` 后运行 | `POST /api/v1/microflows/{id}/test-run`；session 返回 `success`，结果 true（>100 走 high）|
| 25 | 再次运行，amount 输入 `50` | session 返回 `success`，结果 false（normal）；trace 帧的 `selectedCaseValue` 反映分支 |
| 26 | 切换到底部面板「Trace / 调试」 | Trace 列表显示 Submit -> Validate 两层调用栈；点击帧高亮画布对应节点 |
| 27 | 在 App Explorer 选中 `MF_ValidatePurchaseRequest`，点击「删除」 | 后端 422 拒绝，原因为 `MICROFLOW_HAS_REFERENCES`；底部面板「引用检查」展示 Submit 引用 |
| 28 | 校验当前微流 | 工具栏「校验」按钮调 `POST /api/v1/microflows/{id}/validate`；底部面板「验证结果」显示具体 issue |
| 29 | 浏览器控制台 | 无 uncaught promise / unhandled rejection；Toast 错误显示用户友好文案 |
| 30 | 关闭浏览器再打开 | tab 状态、dirty / 草稿 / 引用关系全部恢复（视已保存状态） |

## 2. 自动化验证

后端：

```bash
dotnet build
dotnet test tests/Atlas.AppHost.Tests --filter "FullyQualifiedName~Microflow"
```

前端：

```bash
cd src/frontend
pnpm install
pnpm --filter @atlas/microflow run typecheck
pnpm --filter atlas-app-web run lint
pnpm --filter atlas-app-web run build
pnpm exec vitest run "packages/mendix/mendix-microflow/src/node-registry/toolbox-cleanliness.spec.ts"
pnpm exec vitest run packages/mendix/mendix-studio-core/src/workbench-tabs-lifecycle.spec.ts
```

E2E（可选，需要 Playwright 准备）：

```bash
cd src/frontend
pnpm run test:e2e:app
```

## 3. 反向用例（必须全部失败）

| 场景 | 期望 |
|------|------|
| Toolbox 默认配置中出现 `Sales.*` / `MF_<Name>` | `toolbox-cleanliness.spec.ts` 失败 |
| RuntimeEngine 对 `restCall` 假成功 | `MicroflowRuntimeEngineRegistryDispatchTests.Run_RestCallAction_BlockedByDefaultSecurityPolicy` 失败 |
| TestRun 对 nanoflow only 动作返回 success | `Run_NanoflowOnlyAction_FailsWithUnsupported` 失败 |
| 删除被引用微流不报错 | `MICROFLOW_HAS_REFERENCES` 检查 |

## 4. 关联文档

- [`microflow-canvas-ui-design.md`](microflow-canvas-ui-design.md)
- [`microflow-node-registry.md`](microflow-node-registry.md)
- [`microflow-runtime-engine-design.md`](microflow-runtime-engine-design.md)
- [`microflow-release-e2e-checklist.md`](microflow-release-e2e-checklist.md)（旧版本）
