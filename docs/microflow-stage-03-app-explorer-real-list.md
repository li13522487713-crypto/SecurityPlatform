# Microflow Stage 03 - App Explorer Real Microflow List

## 1. Scope

本轮只把 Mendix Studio 左侧 App Explorer 的 `Microflows` 分组改为真实只读资源列表。

本轮不做 CRUD、不做 schema 加载、不做 schema 保存、不接 Call Microflow metadata、不接 runtime / trace，也不替换微流编辑器为真实资源编辑器。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `packages/mendix/mendix-studio-core/src/components/app-explorer.tsx` | 修改 | `Microflows` 分组接入 `adapterBundle.resourceAdapter.listMicroflows`，展示 loading / empty / error / retry / success 状态 |
| `packages/mendix/mendix-studio-core/src/index.tsx` | 修改 | 将 Stage 02 创建的 `adapterBundle` 和 `workspaceId` 传给 `AppExplorer`；选中真实微流时避免展示 sample schema |
| `packages/mendix/mendix-studio-core/src/store.ts` | 修改 | 新增 `setModuleMicroflows`，按 module 替换真实微流资产索引 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 P0 状态：Stage 03 已完成只读真实列表，CRUD / 真实画布加载保存仍未完成 |
| `docs/microflow-stage-03-app-explorer-real-list.md` | 新增 | 本阶段范围、数据流、验收和非目标说明 |

## 3. Data Flow

`mendix-studio route`
-> `MendixStudioApp`
-> `adapterBundle.resourceAdapter`
-> `GET /api/microflows?workspaceId&moduleId`
-> `MicroflowResource`
-> `mapMicroflowResourceToStudioDefinitionView`
-> `store.microflowResourcesById / microflowIdsByModuleId`
-> `App Explorer Microflows children`

## 4. ModuleId Source

当前 `moduleId` 来自 `SAMPLE_PROCUREMENT_APP.modules[0].moduleId`，并写入 App Explorer 的 Procurement module node。

当前 Microflows 列表已真实化，但模块树本身仍是 sample/static，模块树真实化不是本轮范围。

## 5. UI States

- `loading`: `Loading microflows...`
- `empty`: `No microflows`
- `error`: `Load failed / Retry`，并提供 `Retry` 操作
- `success`: 展示后端返回的真实微流列表，节点 label 使用 `displayName || name`
- `retry`: 重新调用当前 `workspaceId + moduleId` 下的 `listMicroflows`

## 6. Non Goals

- 未实现 CRUD
- 未实现 `/api/microflows/{id}/schema` 加载
- 未实现 schema 保存
- 未接 Call Microflow metadata
- 未接 runtime
- 未接 trace

## 7. Verification

手工验收步骤：

1. 打开 `/space/:workspaceId/mendix-studio/:appId`。
2. 确认 App Explorer 的 `Microflows` 分组显示 loading。
3. 确认请求语义等价于 `GET /api/microflows?workspaceId={workspaceId}&moduleId={moduleId}`。
4. 后端返回多个微流时，树中显示多个真实微流节点。
5. 后端返回空列表时，显示 `No microflows`。
6. 请求失败时，显示 `Load failed / Retry`，点击 `Retry` 后重新请求。
7. 点击真实微流节点只设置选中状态和 `activeMicroflowId`，不加载真实 schema，不展示 `sampleOrderProcessingMicroflow` 作为该资源画布。

验证命令：

```bash
cd src/frontend
pnpm exec tsc --noEmit --project apps/app-web/tsconfig.json
```
