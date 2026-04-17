# 低代码协同编辑规格（lowcode-collab-spec）

> 状态：M00 预创建 stub。
> 范围：M16 Yjs + 自定义 SignalR provider 协同编辑协议、CRDT 结构、冲突解决策略、性能指标。

## 章节占位

- §1 协同总览：`/hubs/lowcode-collab` SignalR Hub + 按 appId 划分 room + 权限校验
- §2 自定义 SignalR Yjs provider 帧格式（仅 Hub 透传 update 二进制，不解析 CRDT 内部结构）
- §3 AppSchema CRDT 结构：Y.Map / Y.Array 适配 ComponentSchema 嵌套
- §4 awareness：多人光标 / 选区 / 选中组件高亮
- §5 组件级操作锁（同一组件同一时刻仅一人编辑属性）+ 锁超时自动释放
- §6 离线编辑（IndexedDB persistence）+ 重连合并 + 冲突可视化
- §7 协同历史回放（按用户 / 按时间）
- §8 与本地撤销/重做栈（M04）的互斥切换：`IHistoryProvider` 抽象
- §9 离线快照：每 10 分钟落 `AppVersionArchive`（系统快照，与用户主动版本区分）
- §10 性能指标：协同延迟 < 200ms（局域网） / 5 浏览器并发不丢稿

> 完整内容由 M16 落地。
