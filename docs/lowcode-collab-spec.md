# 低代码协同编辑规格（lowcode-collab-spec）

> 状态：M16 落地。
> 范围：Yjs CRDT + 自定义 SignalR provider。

## 1. 总览

- Hub 路径：`/hubs/lowcode-collab`（AppHost / SignalR + Authorize）
- 按 `appId + tenantId` 划分 connection group：`lowcode-collab:{tenantId}:{appId}`
- 服务端不解析 Yjs CRDT 内部结构，**仅做权限校验 + 广播 + 缓存**；离线快照由周期任务落 `AppVersionArchive`（`isSystemSnapshot=true`）。

## 2. SignalR 方法约定

| 方向 | 方法名 | 参数 | 说明 |
| --- | --- | --- | --- |
| client → server | `JoinApp` | `(appId)` | 加入 group |
| client → server | `LeaveApp` | `(appId)` | 离开 group |
| client → server | `SendUpdate` | `(appId, userId, base64Update)` | 客户端 Yjs update（base64 编码）|
| server → client | `yjsUpdate` | `({ from, update })` | 服务端把其他客户端的 update 广播；sender 自身不会收到 |

## 3. 自定义 Yjs Provider

文件：`@atlas/lowcode-collab-yjs/signalr-provider`。

```ts
const conn = new HubConnectionBuilder().withUrl('/hubs/lowcode-collab').build();
const provider = new YjsSignalRProvider(doc, conn, { appId, userId });
await provider.connect();
```

设计要点：
- 不依赖 `@microsoft/signalr` 类型本身；接受任意 `SignalRConnectionLike` 实例。
- 本地 `doc.on('update')` → `SendUpdate(base64)`；远程 `yjsUpdate` → `Y.applyUpdate(doc, bytes, origin)`；通过 origin 区分自身 update 防止回声。

## 4. CRDT 结构

`LowCodeCollabDoc`：
- `Y.Doc.getMap('root')` 为 AppSchema 顶层。
- 嵌套 object → `Y.Map`；array → `Y.Array`；标量原样存储。
- `fromJson(value)` / `toJson()` 双向同构。

## 5. Awareness（多人光标）

`CollabAwareness`：
- `setLocal({ userId, username, selectedComponentId, cursor: { x, y }, color })`
- `getRemoteStates()` 返回 Map<clientId, AwarenessUserState>。

## 6. 组件级操作锁

`CollabLockManager`：
- `acquire(componentId, ttl=30s)` → 同 componentId 仅一人持有。
- `renew` / `release` / `isLocked` / `getOwner`。
- 锁过期自动失效（在 isLocked / acquire 内现场判断）。

## 7. 撤销/重做（IHistoryProvider 协同实现）

`YjsCollabHistoryProvider`：
- 基于 `Y.UndoManager`；与 `LocalSliceHistoryProvider`（M04）互斥。
- 进入协同模式时由 `lowcode-editor-canvas` 切换 IHistoryProvider 实例。

## 8. 离线快照

- 服务端 `LowCodeCollabSnapshotCache`（位于 `Atlas.Infrastructure.Services.LowCode`）内存暂存最近一次 update。
- M16 收尾（2026-04）已落地 **`LowCodeCollabSnapshotJob` Hangfire 周期任务**：cron `*/10 * * * *`（每 10 分钟）→ 将 cache 内 base64 update 落 `AppVersionArchive`（label `collab-snapshot-{ts}`，`isSystemSnapshot=true`，`note='Yjs 协同周期快照'`）；落表后 `Cache.Clear`，避免重复快照。
- 由 `LowCodeCollabSnapshotSchedulerHostedService` 在 AppHost 启动时注册到 RecurringJobManager。
- 写入全部经 `IAuditWriter`（`lowcode.collab.snapshot`）。

## 9. 性能约束

- 协同延迟：局域网 < 200 ms。
- 5 浏览器并发 100 组件页面不丢稿（M16 单测目标）。

## 10. 反例

- 服务端不允许在 SendUpdate 内反序列化 base64 → 解析 CRDT；这会破坏 Yjs 的 CRDT 不变性。
- 客户端 collab provider 与本地 LocalSliceHistoryProvider 同时启用；必须互斥（IHistoryProvider 接口切换）。

## 11. 冲突解决策略（P5-3 新增）

协同冲突由 Yjs CRDT 自动解决，无需用户参与。具体策略：

- **CRDT 合并**：所有 Y.Map / Y.Array 操作天然可交换（commutative），多客户端并发写入时按操作时间戳合并，最终一致。
- **组件级锁**：`lowcode-collab-yjs/lock` 提供组件 id 级 acquire/renew/release；同一组件同一时间只允许一人编辑属性面板（默认 TTL 30s，5s 心跳 renew）。
- **离线合并**：本地 IndexedDB persistence（`lowcode-collab-yjs/offline`，y-indexeddb）保留离线编辑；重连时 SignalR provider 自动 sync 历史 update，CRDT 合并不丢稿。
- **冲突可视化**：当组件锁被他人持有，UI 显示锁图标 + 持有者头像；强制夺锁需高权限 + 二次确认（接口预留，UI 留增量）。

## 12. awareness 同步帧格式（P1-6 + P5-3 补完）

| 字段 | 类型 | 说明 |
|---|---|---|
| Hub 方法 | string | `SendAwareness(appId, userId, base64Awareness)` |
| 广播事件 | string | `awareness`（与 yjsUpdate 并行通道） |
| 帧 payload | `{ from: userId, awareness: base64String }` | base64 编码的 awareness 协议二进制（y-protocols/awareness encodeAwarenessUpdate） |
| 接收处理 | applyAwarenessUpdate | origin 标记为 provider.origin 防回声 |
| 客户端断开 | removeAwarenessStates([clientID]) | 清理本地状态让其它客户端立即感知 "对方下线" |

## 13. 性能实证（留增量）

- 5 浏览器并发 100 组件页面：Playwright 多 BrowserContext 脚本（P4-6 留增量）
- 协同延迟：局域网 < 200 ms（基线，未实证）
- awareness 帧大小：每客户端 cursor 状态约 200 bytes，10 客户端并发 100 帧/秒 ≈ 200KB/s，可承受

