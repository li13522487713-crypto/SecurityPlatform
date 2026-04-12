# Atlas Security Platform Contracts

## Workflow V2 API（Coze 40+ 节点复刻）

### 工作流详情读取语义

- `GET /api/v2/workflows/{id}`
- 查询参数：
  - `source=draft|published`
  - `versionId=<WorkflowVersion.Id>`
- 语义：
  - 默认读取当前草稿。
  - `source=published` 读取最新发布版本。
  - `versionId` 优先级高于 `source`，用于只读查看指定版本。
  - 请求发布态但工作流尚未发布时，返回 `404 NotFound`。

### 节点元数据目录

- `GET /api/v2/workflows/node-types`
- 返回内容包含：
  - `key`、`name`、`category`、`description`
  - `ports[]`（方向、数据类型、必填、连接上限）
  - `configSchemaJson`
  - `uiMeta`（icon、color、supportsBatch）

### 节点模板列表

- `GET /api/v2/workflows/node-templates`
- 返回每个节点的真实默认配置模板（用于前端动态表单初始化）
- `Llm` 默认配置至少包含：
  - `provider`
  - `model`
  - `prompt`
  - `systemPrompt`
  - `temperature`
  - `maxTokens`
  - `stream`
  - `outputKey`

### 模型目录来源

- 工作流模型节点前端候选源固定复用 `GET /model-configs/enabled`。
- 返回项映射为：
  - `provider`
  - `providerType`
  - `model`
  - `label`
  - `systemPrompt`
  - `temperature`
  - `maxTokens`
  - `enableStreaming`

### 执行恢复（流式）

- `POST /api/v2/workflows/executions/{executionId}/stream-resume`
- SSE 事件补充：
  - `execution_resume_start`
  - `node_start`
  - `node_output`
  - `node_complete`
  - `node_failed`
  - `execution_complete`
  - `execution_failed`
  - `execution_interrupted`

### 执行入口（run/stream）source 语义

- `POST /api/v2/workflows/{id}/run`
- `POST /api/v2/workflows/{id}/stream`
- 请求体新增可选字段：`source`
  - `published`：按最新发布版本运行（默认）
  - `draft`：按当前草稿运行
- `source=published` 且 workflow 尚未发布时，返回 `VALIDATION_ERROR`。

### 单节点调试语义

- `POST /api/v2/workflows/{id}/debug-node`
- 请求体新增可选字段：
  - `source=draft|published`
  - `versionId=<WorkflowVersion.Id>`
- 语义：
  - 默认调试当前草稿。
  - `source=published` 调试最新发布版本。
  - `versionId` 用于指定历史发布版本的只读调试。

### 画布模型（CanvasSchema）

- `nodes[]`：支持 `childCanvas`（Batch/子图）
- `connections[]`：支持 `condition`
- `NodeSchema` 扩展：
  - `inputTypes` / `outputTypes`
  - `inputSources` / `outputSources`

### 端点连线约束（workflow-editor-react）

- 连线模型固定为端点级：
  - `fromNode` + `fromPort`
  - `toNode` + `toPort`
- `fromPort` / `toPort` 必须来自 `node-types[].ports[].key`。
- 连线方向必须满足 `Output -> Input`。
- 默认禁止节点自环连接（可在后续策略中显式放开）。
- 同一对端点（`fromNode:fromPort -> toNode:toPort`）不允许重复边。
- 连接上限遵循端口元数据：
  - 出端口：`ports[].maxConnections`
  - 入端口：`ports[].maxConnections`
- 类型兼容遵循严格规则：
  - 同类型直接允许；
  - 或命中显式白名单（`any/json/object/array/unknown` 的可兼容集合）；
  - 未命中白名单即拒绝连接。

### 画布保存前一致性校验

- 节点级：
  - `configSchemaJson` 字段校验（`required/type/enum/range/pattern/items`）
  - `inputMappings` 键必须为节点输入端口键
- 连线级：
  - 端口存在性（缺失端口视为非法）
  - 方向合法性（Output -> Input）
  - 重复边拦截
  - 连接数量上限
  - 类型兼容
- 画布级：
  - 指向不存在节点的悬空连接拦截

### 未保存画布校验

- `POST /api/v2/workflows/{id}/validate`
- 请求体支持两种形式：
  - `canvasJson`
  - `canvas`（结构化 `CanvasSchema`）
- 若请求体未提供画布，则回退校验已保存草稿。
- 返回：
  - `isValid`
  - `errors[]`
  - `errors[].code/message/nodeKey/sourcePort/targetPort`

### 执行 Trace

- `GET /api/v2/workflows/executions/{executionId}/trace`
- 返回：
  - `steps[]`：节点级执行时间线
  - `edgeStatuses[]`：边级运行状态回放
- `edgeStatuses[]` 字段：
  - `sourceNodeKey`
  - `sourcePort`
  - `targetNodeKey`
  - `targetPort`
  - `status`
  - `reason`

### 历史草稿兼容策略

- 对历史草稿中缺失 `fromPort` / `toPort` 或端口键失效的连接，编辑器加载时执行迁移归一：
  - 输出端口回退到节点默认输出端口；
  - 输入端口回退到节点默认输入端口；
  - 迁移后若形成重复边，保留一条并给出迁移提示。
- 无法归一的连接在保存/发布前由一致性校验阻断，并输出定位信息。

### 节点分类（7 类）

- Flow Control：Start/End/If/Loop/Batch/Break/Continue
- AI：LLM/Intent Detector/Question Answer
- Data：Code/Text/JSON/Variable/Set Variable
- External：Plugin/HTTP/SubWorkflow
- Knowledge：Dataset Search/Dataset Write/LTM
- Database：Query/Insert/Update/Delete/Custom SQL
- Conversation：Conversation CRUD + History + Message CRUD + Input/Output

## AI 资源库与知识库 API

### 适用主机

- `PlatformHost` 与 `AppHost` 均提供同构接口。
- `app-web` 在 `platform` 与 `direct` 两种运行模式下统一消费以下契约。

### 资源库列表

- `GET /api/v1/ai-workspaces/library`
- 查询参数：
  - `keyword`
  - `resourceType`
  - `pageIndex`
  - `pageSize`
- 返回：`ApiResponse<AiLibraryPagedResult>`
- `AiLibraryPagedResult` 字段：
  - `items[]`
  - `totalCount`
  - `pageIndex`
  - `pageSize`
- `items[]` 每项至少包含：
  - `id`
  - `name`
  - `description`
  - `resourceType`
  - `resourceSubType`
  - `status`
  - `documentCount`
  - `chunkCount`
  - `updatedAt`

### 知识库 CRUD

- `GET /api/v1/knowledge-bases`
- `GET /api/v1/knowledge-bases/{id}`
- `POST /api/v1/knowledge-bases`
- `PUT /api/v1/knowledge-bases/{id}`
- `DELETE /api/v1/knowledge-bases/{id}`
- `KnowledgeBaseCreateRequest` / `KnowledgeBaseUpdateRequest`：
  - `name`
  - `description`
  - `type`，枚举值固定为 `Text`、`Table`、`Image`
- `KnowledgeBaseDto`：
  - `id`
  - `name`
  - `description`
  - `type`
  - `documentCount`
  - `chunkCount`
  - `createdAt`

### 文档管理

- `GET /api/v1/knowledge-bases/{id}/documents`
- `POST /api/v1/knowledge-bases/{id}/documents`
- `DELETE /api/v1/knowledge-bases/{id}/documents/{docId}`
- `GET /api/v1/knowledge-bases/{id}/documents/{docId}/progress`
- `POST /api/v1/knowledge-bases/{id}/documents/{docId}/resegment`
- `POST /api/v1/knowledge-bases/{id}/documents` 支持两种导入方式：
  - `multipart/form-data` 上传 `file`
  - 传入已存在文件的 `fileId`
- `KnowledgeDocumentDto`：
  - `id`
  - `knowledgeBaseId`
  - `fileId`
  - `fileName`
  - `contentType`
  - `fileSizeBytes`
  - `status`
  - `errorMessage`
  - `chunkCount`
  - `createdAt`
  - `processedAt`
- `DocumentProgressDto`：
  - `id`
  - `status`
  - `chunkCount`
  - `errorMessage`
  - `processedAt`
- `DocumentResegmentRequest`：
  - `chunkSize`
  - `overlap`
  - `strategy`

### 分片管理

- `GET /api/v1/knowledge-bases/{id}/documents/{docId}/chunks`
- `POST /api/v1/knowledge-bases/{id}/chunks`
- `PUT /api/v1/knowledge-bases/{id}/chunks/{chunkId}`
- `DELETE /api/v1/knowledge-bases/{id}/chunks/{chunkId}`
- `DocumentChunkDto`：
  - `id`
  - `knowledgeBaseId`
  - `documentId`
  - `chunkIndex`
  - `content`
  - `startOffset`
  - `endOffset`
  - `hasEmbedding`
  - `createdAt`

### 检索测试

- `POST /api/v1/knowledge-bases/{id}/retrieval-test`
- 请求体：`KnowledgeRetrievalTestRequest`
  - `query`
  - `topK`
- 返回：`ApiResponse<RagSearchResult[]>`
- `RagSearchResult`：
  - `knowledgeBaseId`
  - `documentId`
  - `chunkId`
  - `content`
  - `score`
  - `documentName`
  - `documentCreatedAt`
