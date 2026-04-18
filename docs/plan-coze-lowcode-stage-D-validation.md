# 阶段 D 验证报告（M15-M17 多端 / 协同 / 发布）

## 范围
- M15 lowcode-runtime-mini + lowcode-components-mini + lowcode-mini-host(5187) + 渲染器能力差异化
- M16 Yjs 协同：lowcode-collab-yjs + 自定义 SignalR provider + LowCodeCollabHub
- M17 web-sdk + 三类发布产物（hosted/embedded-sdk/preview）+ sdk-playground(5186) + 端点双套

## 验证
- `dotnet build Atlas.SecurityPlatform.slnx` → **0 警告 0 错误**（20s）。
- `pnpm run i18n:check` → **0 缺失**。
- 前端单测：
  - lowcode-components-mini → 5
  - lowcode-runtime-mini → 4
  - lowcode-collab-yjs → 4
  - lowcode-web-sdk → 4
  - 阶段 D 累计：**17**；总计 **133 + 33 + 17 = 183**（A 75 + B 58 + C 33 + D 17）。

## 新增端点
- `GET /api/runtime/renderers/{renderer}/capability`（M15）
- `POST /hubs/lowcode-collab` SignalR Hub（M16）
- `POST /api/v1/lowcode/apps/{id}/publish/{kind}` + `/artifacts` + `/publish/rollback`（M17）
- `GET /api/runtime/publish/{appId}/artifacts`（M17 运行时只读）

## 关键决策
- **Yjs 协同**：自定义 SignalR provider，无 Node 边车；服务端只中转 base64 update，不解析 CRDT。
- **mini 端**：通过 MINI_CAPABILITY_MATRIX 控制 47 件组件在 3 端的支持度；Taro 真实 build 由运维流水线 `taro build --type weapp/tt` 触发。
- **三类产物**：hosted / embedded-sdk / preview 完整 publish 流程；指纹 SHA256(kind|versionId|schemaJson)。
- **端点双套严守**：M14 + M17 都做到设计态 v1 与运行时 runtime 各自 controller 物理隔离。

## 进入阶段 E
- M18 智能体复刻 + 插件完整域
- M19 工作流父级工程能力
- M20 节点 49 全集 + 双哲学引擎
