# @atlas/lowcode-property-forms

> Atlas 低代码属性表单（M05）—— ComponentMeta.propertyPanels 元数据驱动 + 5 种值源切换 + 6 类内容参数 + Monaco LSP 适配桥。

## 哲学声明（PLAN.md §M05 C05-8）

**调试 + 版本是绑定系统的伴随能力**，不是后置功能：
- 任意属性面板字段的 binding 可在编辑期看到表达式 lint / hover / completion（M02 LSP 集成）。
- 任意 binding 改动通过 IHistoryProvider 入栈（M04），可撤销 / 重做。
- 任意 binding 提交进入 dispatch trace（M13），可在调试台看到全链路。

任何属性面板新增字段或值源类型，必须保持上述三项联动；禁止把调试 / 版本 / 历史栈作为后置开关。
