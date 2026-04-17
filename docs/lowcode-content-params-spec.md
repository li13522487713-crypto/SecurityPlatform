# 低代码内容参数 6 类独立机制（lowcode-content-params-spec）

> 状态：M00 预创建 stub。
> 范围：M05 属性表单 + M06 ComponentMeta.contentParams 字段 + 后端校验。
>
> 内容参数（ContentParam）独立于 BindingSchema，是组件接收"内容"输入的统一抽象，分 6 类。

## 章节占位

- §1 6 类内容参数总览
  - `text`（文案）：模板字符串 + i18n key + 静态文本
  - `image`（图片）：URL / fileHandle / imageId / 占位图
  - `data`（数据）：Array / Object 数据源（接 workflow output 或变量）
  - `link`（链接）：内部路由 / 外部 URL（受 webview 白名单约束）
  - `media`（媒体）：视频/音频 URL + 封面
  - `ai`（AI 内容）：chatflow 流式输出 / AI 卡片配置
- §2 与 BindingSchema 的差异说明
- §3 后端校验规则（设计态 `POST /api/v1/lowcode/apps/{id}/validate` 一并校验）
- §4 ComponentMeta.contentParams 字段格式
- §5 反例（禁止用 BindingSchema 替代 ContentParam）

> 完整内容由 M05 / M06 落地。
