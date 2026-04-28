# 低代码内容参数 6 类独立机制（lowcode-content-params-spec）

> 范围：M05 属性表单 + M06 ComponentMeta.contentParams 字段 + 后端校验。
>
> 内容参数（ContentParam）独立于 BindingSchema，是组件接收"内容"输入的统一抽象，分 6 类。

## §1 6 类内容参数总览（P5-1 修正：与 `@atlas/lowcode-schema/types/content-param.ts` 严格对齐）

```ts
type ContentParamSchema =
  | TextContentParam
  | ImageContentParam
  | DataContentParam
  | LinkContentParam
  | MediaContentParam
  | AiContentParam;
```

| kind | 字段 | 用途 |
| --- | --- | --- |
| `text` | `mode = 'static' \| 'template' \| 'i18n'`、`source`（模板字符串 / i18n key / 静态文本）、`context?` | 文案：模板字符串、i18n key、静态文本 |
| `image` | `mode = 'url' \| 'fileHandle' \| 'imageId' \| 'placeholder'`、`source`、`placeholder?` | 图片：URL / 资产句柄 / 图床 ID / 占位图 |
| `data` | `source`（BindingSchema：static / variable / expression / workflow_output / chatflow_output）、`expectArray?` | 数据：列表/对象数据源；通过 BindingSchema 接 workflow output / 变量等 |
| `link` | `linkType = 'internal' \| 'external'`、`href`、`target?` | 链接：内部路由 / 外部 URL（**外部链接强制经 webview 白名单校验**） |
| `media` | `mediaType = 'video' \| 'audio'`、`url`、`cover?` | 媒体：视频 / 音频 URL + 封面 |
| `ai` | `mode = 'chatflow_stream' \| 'ai_card'`、`chatflowId?`、`cardConfig?` | AI 内容：chatflow 流式输出 / AI 卡片配置 |

每个 ContentParam 必带（来自 `ContentParamBase`）：

- `kind`：6 类之一
- `code`：参数编码（应用内唯一），用于在表达式 / props 引用：`contentParam.<code>`
- `description?`：可选描述

> **历史名词对照**：早期文档曾使用 `id` / `name` / `dataSourceKind` 等命名，已统一为 `code` / `mode` / `source` 字段（与 TypeScript 类型严格对齐）。

## §2 与 BindingSchema 的差异说明

| 维度 | ContentParamSchema | BindingSchema |
| --- | --- | --- |
| 用途 | 描述组件**内容输入**（图片/文案/数据等具体业务内容） | 描述**属性绑定**（如 prop 取自表达式 / 变量 / workflow 输出） |
| 数据语义 | 富语义（含 mode / 资源类型 / 校验规则） | 仅描述"取值来源 + 类型"，无业务结构 |
| 多端渲染 | 渲染器按 contentParam.kind 分发（image 走 Image 组件，ai 走 AiCard） | 与组件 prop 一一对应 |
| 校验 | 含 mime / 域名白名单 / 资源存在性等业务级校验 | 仅类型校验 + 作用域隔离 |
| 改写场景 | UI Builder 内"插入图片 / 选择视频 / 引用数据源"用户视图 | 属性面板 monaco 表达式编辑器 |

**反例（禁止）**：用 `BindingSchema { sourceType: 'static', value: 'http://...' }` 表达图片 — 必须用 `ImageContentParam { mode: 'url', url: 'http://...' }` 才能走图片域校验与多端渲染分发。

## §3 后端校验规则

设计态保存 / 发布快照时校验：

- `text`：`template` 必须可被 jsonata + Jinja 解析（不抛 TemplateSyntaxError）
- `image`：mode + 字段一致性（mode=url 必须有 url；mode=fileHandle 必须有 fileHandle 且文件存在 + mime 是 image/*）
- `data`：dataSourceKind 与 ref 一致；workflow_output 必须指向已发布工作流；database_query 必须指向已存在 AiDatabase
- `link`：linkType=external 时 href 域必须命中 LowCodeWebviewDomain 已 verified 列表（通配符按 `*.example.com` 后缀匹配）
- `media`：url 协议必须 https；mediaType 与 cover 字段一致
- `ai`：mode=chatflow_stream 必须 chatflowId 非空且工作流存在

校验失败抛 `BusinessException(ErrorCodes.ValidationError, ...)`，详细字段路径放 `data.errors[]`。

## §4 ComponentMeta.contentParams 字段格式

```ts
interface ComponentMeta {
  // ...
  contentParams?: ReadonlyArray<ContentParamSchema['kind']>;
}
```

声明该组件支持哪些 ContentParam.kind。运行时按 schema.contentParams[].kind 与 meta.contentParams 求交集；不在白名单的 kind 在设计态属性面板隐藏，运行时跳过渲染并产 warning。

## §5 与 BindingSchema 共享的工程约束

- 内容参数中的所有 URL / fileHandle / dataSourceRef 等"外部资源"必须经 ResourceReferenceIndex（M14）登记
- 修改时触发版本归档新建 schema_snapshot_json + resource_snapshot_json 两份副本
- 调试时按 contentParam 标识聚合事件 trace，便于按"图 ABC 来源"排查
