# 低代码绑定矩阵（lowcode-binding-matrix）

> 状态：M00 预创建 stub。
> 范围：M09 模式 A（表单 → 工作流 → 回填）+ 模式 B（动态选项填充）的全部黄金样本（≥ 20 用例）；与 `lowcode-workflow-adapter` 完全一致。

## 章节占位

- §1 模式 A 黄金样本（≥ 10 用例）
  - 表单 → workflow.invoke → Markdown 回填
  - 表单 → workflow.invoke-async → 通知回填
  - 上传图片 → workflow.invoke → Image 回填（与模式 C 联动）
- §2 模式 B 黄金样本（≥ 10 用例）
  - 下拉数据源 → workflow.invoke → Array 自动填充
  - 列表数据源 → workflow.invoke-batch → Array 分页填充
- §3 inputMapping / outputMapping JSONata 表达式约定
- §4 loadingTargets / errorTargets 自动绑定规则
- §5 反例（禁止用法）

> 完整内容由 M09 落地。
