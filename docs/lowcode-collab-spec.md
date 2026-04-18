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

- 服务端 `LowCodeCollabSnapshotCache` 内存暂存最近一次 update。
- 周期 10 分钟（M16 后续接入 Hangfire）落 `AppVersionArchive`（`isSystemSnapshot=true`），与用户主动版本区分。

## 9. 性能约束

- 协同延迟：局域网 < 200 ms。
- 5 浏览器并发 100 组件页面不丢稿（M16 单测目标）。

## 10. 反例

- 服务端不允许在 SendUpdate 内反序列化 base64 → 解析 CRDT；这会破坏 Yjs 的 CRDT 不变性。
- 客户端 collab provider 与本地 LocalSliceHistoryProvider 同时启用；必须互斥（IHistoryProvider 接口切换）。
