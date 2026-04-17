# 低代码组件规格（lowcode-component-spec）

> 状态：M00 预创建 stub。
> 范围：M06 全部 30+ 组件能力 6 维矩阵 + AI 原生特征矩阵 + 元数据驱动原则正/反例。

## 章节占位

- §1 ComponentMeta 字段全集（type / displayName / category / supportedValueType / bindableProps / contentParams / supportedEvents / childPolicy / propertyPanels / icon / group / version / runtimeRenderer）
- §2 组件能力 6 维矩阵：组件 × (表单值采集 / 事件触发 / 工作流输出回填 / AI 原生绑定 / 上传产物 / 内容参数)
  - 布局类（layout）：Container / Row / Column / Tabs / Drawer / Modal / Grid / Section
  - 展示类（display）：Text / Markdown / Image / Video / Avatar / Badge / Progress / Rate / Chart / EmptyState / Loading / Error / Toast
  - 输入类（input）：Button / TextInput / NumberInput / Switch / Select / RadioGroup / CheckboxGroup / DatePicker / TimePicker / ColorPicker / Slider / FileUpload / ImageUpload / CodeEditor / FormContainer / FormField / SearchBox / Filter
  - AI 原生（ai）：AiChat / AiCard / AiSuggestion / AiAvatarReply
  - 数据类（data）：WaterfallList / Table / List / Pagination
- §3 AI 原生组件特征矩阵：绑定 chatflow / 绑定模型 / SSE 流式渲染 / tool_call 气泡 / 历史回放 / 中断恢复
- §4 元数据驱动原则正例与反例

> 完整内容由 M06 落地。
