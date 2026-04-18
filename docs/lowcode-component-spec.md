# 低代码组件规格（lowcode-component-spec）

> 状态：M06 落地。
> 范围：30+ 组件能力 6 维矩阵 + AI 原生特征矩阵 + 元数据驱动原则正/反例。

## 1. ComponentMeta 字段全集（M06 C06-1）

参见 `@atlas/lowcode-schema/types/component.ts` 的 `ComponentMeta`。

- type / displayName / category / group / icon / version
- runtimeRenderer（与后端 `?renderer=` 共享集合 web / mini-wx / mini-douyin / h5）
- bindableProps（声明可绑定 prop 列表；不在表内的 prop 不允许 binding，由 propertyPanels 渲染器 + zod 校验双层守门）
- contentParams（声明组件支持的内容参数 6 类的子集）
- supportedEvents（事件名）
- childPolicy（arity = none / one / many + allowTypes 限定）
- propertyPanels（属性面板分组与字段，元数据驱动渲染）
- supportedValueType（声明 prop → 9 类 valueType 的默认推断）

## 2. 组件能力 6 维矩阵（M06 C06-8）

每个组件至少满足 1 维（CI 守门测试已经强校验）。

| 组件 | 表单值采集 | 事件触发 | 工作流输出回填 | AI 原生 | 上传产物 | 内容参数 |
| --- | :---: | :---: | :---: | :---: | :---: | :---: |
| **layout（8）** Container/Row/Column/Tabs/Drawer/Modal/Grid/Section | – | Tabs/Drawer/Modal | bindableProps | – | – | – |
| **display（13）** Text/Markdown/Image/Video/Avatar/Badge/Progress/Rate/Chart/EmptyState/Loading/Error/Toast | Rate | Rate/Error | bindableProps | – | – | text/image/media/data |
| **input（18）** Button/TextInput/NumberInput/Switch/Select/Radio/Checkbox/Date/Time/Color/Slider/FileUpload/ImageUpload/CodeEditor/FormContainer/FormField/SearchBox/Filter | ✓（除 Button/FormContainer/FormField） | ✓ | ✓ | – | FileUpload / ImageUpload | Select/Radio/Checkbox/Filter（data） |
| **ai（4）** AiChat/AiCard/AiSuggestion/AiAvatarReply | – | AiChat/AiCard/AiSuggestion | ✓ | ✓ | – | ai / data |
| **data（4）** WaterfallList/Table/List/Pagination | – | ✓ | ✓ | – | – | WaterfallList/Table/List（data） |

## 3. AI 原生组件特征（M06 C06-4）

每个 AI 组件必须满足以下能力：

- **绑定 chatflow**：bindableProps 必含 `chatflowId`
- **绑定模型**：bindableProps 必含 `modelId`（可选）或通过 chatflow 间接绑定
- **SSE 流式渲染**：实现层接 M11 ChatflowAdapter `streamChat`（仅由 dispatch 内部调用，组件实现禁止直 fetch）
- **tool_call 气泡**：实现层处理 `tool_call` 事件类型（M11 SSE 4 类事件之一）
- **历史回放**：实现层接 M11 SessionAdapter `listSessions`/`switchSession`
- **中断恢复**：实现层接 M11 `pauseChat` / `resumeChat` / `injectMessage`

| AI 组件 | chatflow | model | SSE | tool_call | 历史 | 中断 |
| --- | :---: | :---: | :---: | :---: | :---: | :---: |
| AiChat | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| AiCard | ✓ | – | ✓ | – | – | – |
| AiSuggestion | – | ✓ | – | – | – | – |
| AiAvatarReply | ✓ | – | ✓ | ✓ | – | ✓ |

## 4. 元数据驱动原则（M06 C06-2）

### 正例

```ts
// 组件实现仅依赖 React + Semi，所有业务行为通过 events / actions 路由到 dispatch。
import { useState } from 'react';
import { Button as SemiButton } from '@douyinfe/semi-ui';

registerComponent(meta, {
  implementationDescriptor: {
    importedGlobals: [],
    importedPackages: ['react', '@douyinfe/semi-ui']
  }
});
```

### 反例（CI 守门会拒绝）

```ts
// 组件实现内直接 fetch → MetadataDrivenViolationError
registerComponent(meta, {
  implementationDescriptor: {
    importedGlobals: ['fetch'],
    importedPackages: []
  }
});

// 组件实现内直接 import workflow_api → 拒绝
registerComponent(meta, {
  implementationDescriptor: {
    importedGlobals: [],
    importedPackages: ['@coze-arch/bot-api/workflow_api']
  }
});
```

## 5. 后端端点

- `GET /api/v1/lowcode/components/registry?renderer=web|mini-wx|mini-douyin|h5` — 静态 manifest + 租户级 overrides
- `POST /api/v1/lowcode/components/overrides` — 租户级隐藏 / 默认 props 覆盖
- `DELETE /api/v1/lowcode/components/overrides/{type}` — 取消覆盖
- 写接口全部经 `IAuditWriter` 审计（lowcode.components.override.upsert/delete）

## 6. 静态 manifest 与前端 build 双源约束

- M06 阶段静态 manifest 由后端 `LowCodeComponentStaticManifest` 与前端 `@atlas/lowcode-components-web/src/meta/categories.ts` 双源持有，必须保持一致。
- M07 lowcode-studio-web 上线后，将由前端 build 输出 `dist/manifest.json` 作为唯一来源；后端在启动时读取并缓存。
