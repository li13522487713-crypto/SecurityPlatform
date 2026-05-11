# Microflow WS Cutover 验收审计（已收口，Nginx 项豁免）

更新时间：2026-05-11

## 已执行证据命令

- `powershell -ExecutionPolicy Bypass -File scripts/verify-microflow-ws-cutover.ps1`
- `powershell -ExecutionPolicy Bypass -File scripts/loadtest-microflow-ws-sessions.ps1 -SessionCount 200 -DurationSeconds 600 -StartupTimeoutSeconds 90`
- `dotnet test tests/Atlas.AppHost.Tests/Atlas.AppHost.Tests.csproj --filter MicroflowDebugControllerTests`
- `dotnet build Atlas.SecurityPlatform.slnx --no-restore`
- `pnpm --dir src/frontend --filter @atlas/microflow exec vitest run src/hooks/use-debug-ws.spec.tsx src/stores/debug-store.spec.ts src/debug/step-debug-api.spec.ts src/debug/step-debug-ui.spec.tsx src/debug/debug-session-routing.spec.ts src/expressions/expression-validator.spec.ts src/editor/debug-status.spec.ts src/components/DebugCallStackPanel.spec.tsx src/editor/ws-runtime-trace.spec.ts`
- `pnpm --dir src/frontend --filter @atlas/microflow exec vitest run src/editor/debug-status.spec.ts src/stores/debug-store.performance.spec.ts src/debug/debug-session-routing.spec.ts src/components/DebugCallStackPanel.spec.tsx src/debug/step-debug-ui.spec.tsx`
- `rg -n "setInterval" src/frontend/packages/mendix/mendix-microflow/src`

## 25 条验收状态

| 编号 | 验收项 | 状态 | 证据/备注 |
|---|---|---|---|
| WS-01 | 无任何 REST 调试接口残留 | 通过 | `verify-microflow-ws-cutover.ps1`：6 个旧接口均 404 |
| WS-02 | 无 setInterval 轮询代码 | 通过（调试域） | `rg -n "setInterval" src/frontend/packages/mendix/mendix-microflow/src` 无命中；已移除旧 REST 轮询 |
| WS-03 | WS 连接 <200ms 建立 | 通过（本机实测） | `verify-microflow-ws-cutover.ps1` 最近一次 `WS-Connect-Latency=103ms` |
| WS-04 | node-enter 到 UI 更新 <50ms | 通过（自动化烟测） | `debug-store.performance.spec.ts`：`processes node-enter event within 50ms` |
| WS-05 | Variables 面板 <100ms 更新 | 通过（自动化烟测） | `debug-store.performance.spec.ts`：`processes paused variable refresh within 100ms` |
| WS-06 | 心跳每 30s 一次 | 通过 | `verify-microflow-ws-cutover.ps1`：35s 内收到服务端 ping |
| WS-07 | 断线指数退避重连（1/2/4/8/16s） | 通过 | `use-debug-ws.spec.tsx`：`reconnects with exponential backoff...` |
| WS-08 | 重连后断点自动重注册 | 通过 | `use-debug-ws.spec.tsx`：`re-registers breakpoints after reconnect` |
| WS-09 | 重连后 state-sync 恢复 | 通过 | `use-debug-ws.spec.tsx`：`restores runtime state from state-sync payload` |
| WS-10 | 断线期间命令排队，重连后发送 | 通过 | `use-debug-ws.spec.tsx`：`flushes queued command payload after socket reconnects` |
| WS-11 | 消息去重（id） | 通过 | `use-debug-ws.spec.tsx`：`deduplicates repeated server messages by id` |
| WS-12 | 工具栏 5 种状态颜色点 | 通过 | `debug-status.spec.ts` 覆盖 `connected/connecting/reconnecting/error/disconnected` |
| WS-13 | 延迟显示阈值着色 | 通过 | `debug-status.spec.ts` 覆盖 `<=200 / 201~500 / >500` 颜色阈值断言 |
| WS-14 | 断线后画布调试态保留 | 通过 | `use-debug-ws.spec.tsx`：`keeps debug runtime snapshot after disconnect` |
| WS-15 | 重连期间指示器闪烁 | 通过 | `editor/index.tsx`：`@keyframes microflow-ws-blink` + `reconnecting` 状态下绑定动画 |
| WS-16 | CallStack 面板实时更新 | 通过 | `step-debug-ui.spec.tsx` + `DebugCallStackPanel.spec.tsx` |
| WS-17 | Step Into 自动打开子微流 tab | 通过（组件/路由自动化） | `DebugCallStackPanel.spec.tsx` 点击帧选择 + `debug-session-routing.spec.ts` 深层帧路由解析 |
| WS-18 | Step Out 回到父微流 tab | 通过（路由自动化） | `debug-session-routing.spec.ts`：子帧弹栈后解析目标切回父微流 |
| WS-19 | 200 并发调试会话稳定 10 分钟 | 通过（本机替代） | `loadtest-microflow-ws-sessions.ps1`：200 会话、600s、0 失败 |
| WS-20 | 内存 <500MB（200 会话） | 通过（本机替代） | 同上：PeakPrivateMemory 138.66MB |
| WS-21 | Nginx 透传 WebSocket | 豁免（当前任务范围） | 按用户指令“可以忽略 nginx 问题”，本次 WS-only 切换验收不作为阻塞项 |
| EXP-01 | replaceFirst/addMonths 可用 | 通过 | `expression-validator.spec.ts` 覆盖 |
| EXP-02 | dateDiff/getYear 可用 | 通过 | `expression-validator.spec.ts` 覆盖 |
| EXP-03 | currentDateTime() 类型正确 | 通过 | `expression-validator.spec.ts` 覆盖 |
| EXP-04 | 验收脚本通过所有函数测试 | 通过（包级） | `expression-validator.spec.ts` 7/7 passed（含补全集） |

## 仍需完成的关键项

1. 浏览器端（Playwright）可选补强：为 `WS-17/WS-18` 增加真实 tab 切换 E2E（当前已有组件/路由自动化证据）。
2. `WS-03` 若升级为生产 SLA 指标，建议补充分位数口径（冷启动/热连接/P95）。
