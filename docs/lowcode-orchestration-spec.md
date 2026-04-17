# 低代码编排哲学与有状态运行规格（lowcode-orchestration-spec）

> 状态：M00 预创建 stub。
> 范围：M19 + M20 两种编排哲学的运行时切换 + 有状态工作流节点状态作用域规范。

## 章节占位

- §1 两种编排哲学
  - 显式模式：现有 `DagExecutor` 完整支持
  - 模型自决模式：`AgenticOrchestrator` 接 LLM tool calling 协议，运行时根据模型决策动态调用 Tool 池
  - 切换机制：在工作流画布顶部"模式"切换器；两种模式均落到现有 DAG 引擎
  - 执行轨迹统一落 trace 系统（M13）
- §2 有状态工作流节点状态作用域
  - 无状态（stateless）
  - session 级
  - conversation 级
  - trigger 级
  - app 级
- §3 `INodeStateStore` 抽象（按作用域分库存储）
- §4 与 M11 SessionAdapter / TriggerAdapter 联动规则
- §5 反例（禁止把状态依赖塞入 page-scope 临时变量）

> 完整内容由 M19 / M20 落地。
