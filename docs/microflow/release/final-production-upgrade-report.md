# Microflows 生产化升级最终报告

## 结论

- Production Gate 结论：conditional-go。
- 原因：R1-R5 机器门禁和关键静态验证已落地；当前执行环境缺失 `dotnet`，后端 build/test 无法在本机运行；Step Debug / Gateway / Expression 为生产化 shell 与静态门禁闭环，真实长运行分布式能力仍列入 known limitations。

## 修改范围摘要

- 后端：Microflow 授权、生产 guard、workspace ownership、schema save 并发/幂等、真实 rollback/cast/listOperation executor、descriptor normalizer、schema migration、connector stubs、branch/gateway runtime 模型、expression API、debug API。
- 前端：production no-mock guard、API error envelope spec、sample fallback purge、tab isolation spec、conflict metadata 展示、ExpressionEditor shell、Step Debug UI shell。
- Verify：matrix/naming/coverage/gate、P0 readiness、parallel/inclusive gateway、expression language/editor、step debug/debug API。

## 已运行命令

- `pnpm --dir src/frontend run microflow:verify:matrix`
- `pnpm --dir src/frontend run microflow:verify:naming`
- `pnpm --dir src/frontend run microflow:verify:coverage`
- `pnpm --dir src/frontend run microflow:verify:gate`
- `node scripts/verify-microflow-p0-readiness.ts`
- `node scripts/verify-microflow-parallel-gateway.ts`
- `node scripts/verify-microflow-inclusive-gateway.ts`
- `node scripts/verify-microflow-expression-language.ts`
- `node scripts/verify-microflow-expression-editor.ts`
- `node scripts/verify-microflow-step-debug.ts`
- `node scripts/verify-microflow-debug-api.ts`
- `pnpm --dir src/frontend run i18n:check`

## 未运行命令及原因

- `dotnet build` / `dotnet test`：当前容器 `dotnet: command not found`。
- Playwright live E2E：当前未启动 AppHost，且 dotnet runtime 缺失。

## 剩余风险

详见 `known-limitations.md`。重点风险：Async job queue、time-travel debug、debug-time mutation、OS thread isolation、branchOnly suspend policy、真实 connector 接入。
