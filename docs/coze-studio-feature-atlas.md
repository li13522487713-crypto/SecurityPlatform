# Coze Studio 功能图谱（Feature Atlas）

> 文档生成时间：2026-03-13  
> 分析项目：`E:\codeding\coze-studio`  
> 数据来源：legacymap 架构地图分析 + featureatlas 功能清单梳理

---

## 一、功能清单总览（按业务域分组）

### 1.1 用户可见功能（前台）

#### 域 1：用户账户与认证（Auth / Passport）

| # | 功能名称 | 功能简介 | API 入口 | 核心后端文件 | 相关表 | 双实现/废弃风险 |
|---|---------|---------|----------|-------------|--------|----------------|
| 1 | 邮箱注册 | 账号注册 | `POST /api/passport/web/email/register/v2/` | `passport_service.go` | `user`, `space`, `space_user` | **v2 版本** |
| 2 | 邮箱登录 | 账号登录 | `POST /api/passport/web/email/login/` | `passport_service.go` | `user` | — |
| 3 | 退出登录 | 清除 session | `GET /api/passport/web/logout/` | `passport_service.go` | — | — |
| 4 | 重置密码 | 邮箱密码找回 | `GET /api/passport/web/email/password/reset/` | `passport_service.go` | `user` | — |
| 5 | 获取账户信息 | 当前登录用户信息 | `POST /api/passport/account/info/v2/` | `passport_service.go` | `user` | **v2 版本** |
| 6 | 更新用户资料 | 修改昵称/描述 | `POST /api/user/update_profile` | `developer_api_service.go` | `user` | — |
| 7 | 头像上传 | 上传用户头像到 OSS | `POST /api/web/user/update/upload_avatar/` | `developer_api_service.go` | `files` | — |

**Application 模块：** `application/user` · **Domain：** `domain/user` · **前端：** `foundation/account-*` → `/sign`

---

#### 域 2：空间与工作区（Space / Workspace）

| # | 功能名称 | API 入口 | 相关表 |
|---|---------|----------|--------|
| 8 | 获取空间列表 | `POST /api/playground_api/space/list` | `space`, `space_user` |

**前端：** `foundation/space-*` → `/space/:space_id/develop`

---

#### 域 3：Agent Bot 开发

| # | 功能名称 | 功能简介 | API 入口 | 相关表 | 双实现/废弃风险 |
|---|---------|---------|----------|--------|----------------|
| 9 | 创建 Bot 草稿 | 新建 Agent | `POST /api/draftbot/create` | `app_draft` | — |
| 10 | 更新/获取 Bot 草稿 | 保存 IDE 内配置 | `POST /api/playground_api/draftbot/update_draft_bot_info` | `app_draft` | — |
| 11 | 复制 Bot | 复制 Agent | `POST /api/draftbot/duplicate` | `app_draft` | — |
| 12 | 发布预检 | 发布前合法性检查 | `POST /api/draftbot/commit_check` | `app_draft` | — |
| 13 | 发布 Bot | 草稿发布到线上 | `POST /api/draftbot/publish` | `app_release_record` | — |
| 14 | 历史版本列表 | 历史发布版本 | `POST /api/draftbot/list_draft_history` | `app_release_record` | — |
| 15 | 发布渠道列表 | 可发布 connector 列表 | `POST /api/draftbot/publish/connector/list` | `app_connector_release_ref` | — |
| 16 | 获取线上 Bot 信息（Open API） | 第三方查询 | `GET /v1/bot/get_online_info` `GET /v1/bots/:bot_id` | `app_release_record` | **双入口并存** |

**Application：** `application/singleagent` · **Domain：** `domain/app` · **前端：** `agent-ide/*` → `/space/:id/bot/:bot_id`

---

#### 域 4：项目 IDE（Project / App）

| # | 功能名称 | API 入口 | 相关表 | 双实现/废弃风险 |
|---|---------|----------|--------|----------------|
| 17 | 创建/更新/删除项目 | `POST /api/intelligence_api/draft_project/create|update|delete` | `app_draft` | — |
| 18 | 复制项目（异步） | `POST /api/intelligence_api/draft_project/copy` | `app_draft`, `data_copy_task` | — |
| 19 | 搜索草稿资源列表 | `POST /api/intelligence_api/search/get_draft_intelligence_list` | ES 索引 | — |
| 20 | 发布项目 | `POST /api/intelligence_api/publish/publish_project` | `app_release_record` | — |
| 21 | 发布记录与详情 | `POST /api/intelligence_api/publish/publish_record_list|detail` | `app_release_record` | — |
| 22 | 最近编辑的资源 | `POST /api/intelligence_api/search/get_recently_edit_intelligence` | — | ⚠️ **空 stub，未实现** |

**Application：** `application/app` · **Domain：** `domain/app` · **前端：** `project-ide/*` → `/space/:id/project-ide/:id`

---

#### 域 5：工作流（Workflow）

| # | 功能名称 | 功能简介 | API 入口 | 相关表 | 双实现/废弃风险 |
|---|---------|---------|----------|--------|----------------|
| 23 | 工作流 CRUD | 创建/保存/删除/复制 | `POST /api/workflow_api/create|save|delete|copy` | `workflow_meta`, `workflow_draft` | — |
| 24 | 发布工作流 | 草稿发布为版本 | `POST /api/workflow_api/publish` | `workflow_version` | — |
| 25 | 测试运行工作流 | 完整流程调试 | `POST /api/workflow_api/test_run` | `workflow_execution` | — |
| 26 | 单节点调试（v2） | 调试单个节点 | `POST /api/workflow_api/nodeDebug` | `node_execution` | v2 实现 |
| 27 | 恢复/取消执行 | 断点恢复/终止 | `POST /api/workflow_api/test_resume|cancel` | `workflow_execution` | — |
| 28 | 获取执行进度 | 实时查看执行状态 | `GET /api/workflow_api/get_process` | `workflow_execution` | — |
| 29 | 执行 Trace | 获取节点链路 trace | `POST /api/workflow_api/get_trace|list_spans` | `workflow_execution` | — |
| 30 | ChatFlow 角色管理 | ChatFlow 角色配置 | `POST/GET /api/workflow_api/chat_flow_role/*` | `chat_flow_role_config` | — |
| 31 | 会话模板管理（项目） | App 内会话定义 | `POST/GET /api/workflow_api/project_conversation/*` | `app_conversation_template_*` | — |
| 32 | 运行工作流（Open API） | 外部同步/流式调用 | `POST /v1/workflow/run|stream_run|stream_resume` | `workflow_execution` | — |
| 33 | ChatFlow 运行（Open API） | 对话式工作流调用 | `POST /v1/workflows/chat` | `workflow_execution` | — |

**Application：** `application/workflow` · **Domain：** `domain/workflow`（最复杂，含 Eino 运行时）· **前端：** `workflow/*` → `/work_flow`

---

#### 域 6：插件开发（Plugin）

| # | 功能名称 | API 入口 | 相关表 | 双实现/废弃风险 |
|---|---------|----------|--------|----------------|
| 34 | 创建/更新/删除插件 | `POST /api/plugin_api/register|update|del_plugin` | `plugin`, `plugin_draft` | — |
| 35 | 创建/更新/删除工具（API） | `POST /api/plugin_api/create_api|update_api|delete_api` | `tool`, `tool_draft` | — |
| 36 | 在线调试工具 | `POST /api/plugin_api/debug_api` | — | — |
| 37 | 发布插件 | `POST /api/plugin_api/publish_plugin` | `plugin_version`, `agent_tool_version` | — |
| 38 | OAuth 授权管理 | `POST /api/plugin_api/get_oauth_schema|status|revoke_auth_token` | `plugin_oauth_auth` | ⚠️ 有 `/api/plugin/get_oauth_schema` 老路由 |
| 39 | 编辑锁（并发保护） | `POST /api/plugin_api/check_and_lock_plugin_edit` | `plugin_draft` | — |
| 40 | OAuth 回调处理 | `GET /api/oauth/authorization_code` | `plugin_oauth_auth` | — |
| 41 | 资源复制（异步） | `POST /api/plugin_api/resource_copy_dispatch|detail|retry|cancel` | `data_copy_task` | — |

**Application：** `application/plugin` · **Domain：** `domain/plugin` · **前端：** `agent-ide/plugin-*` → `/space/:id/plugin/:id`

---

#### 域 7：知识库（Knowledge / RAG）

| # | 功能名称 | API 入口 | 相关表 | 双实现/废弃风险 |
|---|---------|----------|--------|----------------|
| 42 | 知识库 CRUD | `POST /api/knowledge/create|detail|update|delete` | `knowledge` | ⚠️ **三套入口并存**：`/api/knowledge/`、`/open_api/knowledge/`、`/v1/datasets` |
| 43 | 文档上传/解析/分片 | `POST /api/knowledge/document/create|progress|resegment` | `knowledge_document`, `knowledge_document_slice` | ⚠️ 同上，三套入口 |
| 44 | 片段 CRUD | `POST /api/knowledge/slice/create|update|delete|list` | `knowledge_document_slice` | — |
| 45 | 图片知识库 | `POST /api/knowledge/photo/list|extract_caption` | `knowledge_document` | — |
| 46 | 文档人工审核 | `POST /api/knowledge/review/create|mget|save` | `knowledge_document_review` | — |
| 47 | 结构化表 Schema 管理 | `POST /api/knowledge/table_schema/get|validate` | `knowledge` | — |

**Application：** `application/knowledge` · **Domain：** `domain/knowledge` · **基础设施：** ES + MinIO + Milvus + Reranker

---

#### 域 8：数据库与变量（Memory）

| # | 功能名称 | API 入口 | 双实现/废弃风险 |
|---|---------|----------|----------------|
| 48 | 数据库表 CRUD | `POST /api/memory/database/add|list|update|delete` | — |
| 49 | 数据库绑定/解绑 Bot | `POST /api/memory/database/bind_to_bot|unbind_to_bot` | — |
| 50 | 表记录读写 | `POST /api/memory/database/list_records|update_records` | — |
| 51 | 文件批量导入（异步） | `POST /api/memory/table_file/submit|get_progress` | 异步任务 |
| 52 | 变量读写 | `POST /api/memory/variable/get|upsert|delete` | — |
| 53 | 项目变量元信息 | `GET/POST /api/memory/project/variable/meta_list|meta_update` | — |

---

#### 域 9：会话与聊天（Conversation）

| # | 功能名称 | API 入口 | 双实现/废弃风险 |
|---|---------|----------|----------------|
| 54 | Agent 执行（SSE 流式） | `POST /api/conversation/chat` | — |
| 55 | 消息历史/删除/清空 | `POST /api/conversation/get_message_list|delete_message|clear_message` | — |
| 56 | 会话分段（Section） | `POST /api/conversation/create_section` | — |
| 57 | 中断生成 | `POST /api/conversation/break_message` | — |
| 58 | 对话 v3（Open API） | `POST /v3/chat` | **最新规范**，与内部 `/api/conversation/chat` 并存 |
| 59 | 取消/查询 v3 Chat | `POST /v3/chat/cancel` `GET /v3/chat/retrieve` | — |
| 60 | 会话管理（Open API v1） | `POST|GET|PUT|DELETE /v1/conversations/*` | **v1/v3 双版本并存** |

---

#### 域 10：探索广场（Explore / Marketplace）

| # | 功能名称 | API 入口 | 相关表 |
|---|---------|----------|--------|
| 61 | 产品列表/搜索/分类 | `GET /api/marketplace/product/list|search|category/list` | `plugin_version`, `template` |
| 62 | 产品详情/复制/收藏 | `GET|POST /api/marketplace/product/detail|duplicate|favorite` | — |
| 63 | 收藏列表（v2） | `GET /api/marketplace/product/favorite/list.v2` | — |

> ⚠️ `favorite/list.v2` 路径含 `.v2` 后缀不符合 RESTful 版本规范。

---

#### 域 11：Prompt / 快捷命令

| # | 功能名称 | API 入口 |
|---|---------|----------|
| 64 | Prompt 资源 CRUD | `POST /api/playground_api/upsert_prompt_resource|delete_prompt_resource` |
| 65 | 官方 Prompt 列表 | `POST /api/playground_api/get_official_prompt_list` |
| 66 | 快捷命令管理 | `POST /api/playground_api/create_update_shortcut_command` |

---

### 1.2 后台管理功能（Admin）

#### 域 12：管理后台（Admin Config）

| # | 功能名称 | API 入口 | 说明 |
|---|---------|----------|------|
| 67 | 获取/保存基础配置 | `GET/POST /api/admin/config/basic/get|save` | 服务域名、API Key 等平台参数 |
| 68 | 获取/更新知识库配置 | `GET/POST /api/admin/config/knowledge/get|save` | 全局 RAG 参数 |
| 69 | 创建/删除/列举模型 | `POST/GET /api/admin/config/model/create|delete|list` | LLM 模型注册管理 |

> ⚠️ 后端 API 已全部实现，但**前端路由文件中未发现对应 `/admin` 页面**，可能通过独立入口访问。

---

### 1.3 系统支撑功能

#### 域 13：Open API 认证（PAT）

| # | 功能名称 | API 入口 |
|---|---------|----------|
| 70 | 创建/删除/列举/更新 PAT | `POST/GET /api/permission_api/pat/*` |

---

#### 域 14：文件上传（Upload）

| # | 功能名称 | API 入口 | 双实现/废弃风险 |
|---|---------|----------|----------------|
| 71 | 获取上传 token/预签名 | `GET/POST /api/common/upload/apply_upload_action` | — |
| 72 | 通用文件上传 | `POST /api/common/upload/*` | — |
| 73 | Bot 文件上传 | `POST /api/bot/upload_file` | ⚠️ 与 `/v1/files/upload` 并存 |
| 74 | Open API 文件上传 | `POST /v1/files/upload` | Open API 标准入口 |

---

#### 域 15：连接器与权限（Connector / Permission）

| 功能域 | 说明 |
|--------|------|
| Connector 管理 | Bot/App 对外发布渠道（`application/connector` → `domain/connector`） |
| 权限控制（RBAC） | 基于角色/空间的访问控制（`application/permission` → `domain/permission`） |

---

### 1.4 定时/异步功能

| # | 功能名称 | 触发方式 | 消费位置 | 处理内容 |
|---|---------|----------|----------|----------|
| 75 | 文档向量化管道 | 文档上传后 MQ 消息 | `application/knowledge/init.go` | 解析 → 分片 → Embedding → 写入 Milvus/VikingDB |
| 76 | 搜索索引更新 | 资源创建/更新/删除事件 | `application/search/` | 写入 Elasticsearch |
| 77 | 跨空间资源复制 | 用户触发复制操作（异步） | `crossdomain/datacopy` | 复制插件/工作流/知识库，状态写入 `data_copy_task` |
| 78 | 数据表文件批量导入 | 用户提交导入任务 | `application/memory` | 文件解析 → 批量写入数据库表 |

> **无独立定时任务框架**，全部通过消息队列 Producer-Consumer 模式实现，默认 NSQ，支持 Kafka/RMQ/Pulsar/NATS 切换（`COZE_MQ_TYPE` 环境变量）。

---

## 二、功能-模块映射表

| 功能域 | 后端 Application 模块 | 后端 Domain | 前端 Package / 路由 |
|--------|----------------------|-------------|---------------------|
| 用户认证 | `application/user` | `domain/user` | `foundation/account-*` → `/sign` |
| 工作区/空间 | `application/user` | `domain/user` | `foundation/space-*` → `/space/:id/develop` |
| Agent Bot 开发 | `application/singleagent` | `domain/app` | `agent-ide/*` → `/space/:id/bot/:bot_id` |
| 项目 IDE | `application/app` | `domain/app` | `project-ide/*` → `/space/:id/project-ide/:id` |
| 工作流 | `application/workflow` | `domain/workflow` | `workflow/*` → `/work_flow` |
| 插件开发 | `application/plugin` | `domain/plugin` | `agent-ide/plugin-*` → `/space/:id/plugin/:id` |
| 知识库 RAG | `application/knowledge` | `domain/knowledge` | `studio/workspace-base` → `/space/:id/knowledge/:id` |
| 内存/数据库 | `application/memory` | —（OceanBase/RDB） | `studio/workspace-base` → `/space/:id/database/:id` |
| 会话/聊天 | `application/conversation` | `crossdomain/conversation` | `agent-ide/chat-*`（Playground 内嵌） |
| 探索广场 | `application/plugin` + `application/template` | `domain/plugin`, `domain/template` | `community/explore` → `/explore/*`, `/search/*` |
| Prompt 管理 | `application/prompt` | `domain/prompt` | Agent IDE 内嵌 |
| 快捷命令 | `application/shortcutcmd` | `domain/shortcutcmd` | Agent IDE 内嵌 |
| 文件上传 | `application/upload` | `domain/upload` | 公共组件（嵌入各页） |
| PAT 认证 | `application/openauth` | — | `studio/open-platform/open-auth` |
| Admin 配置 | `application/modelmgr` | — | ⚠️ 前端路由待确认 |
| 搜索 | `application/search` | `domain/search`（ES） | `community/explore` → `/search/:word` |
| 模板 | `application/template` | `domain/template` | `community/explore`（探索广场内） |
| 连接器 | `application/connector` | `domain/connector` | `studio/open-platform/*`（发布配置内） |
| 权限/RBAC | `application/permission` | `domain/permission` | 中间件层，无独立页面 |
| 数据复制（异步） | `crossdomain/datacopy` | `domain/datacopy` | 进度嵌入复制操作 UI |

---

## 三、关键发现与风险点

### 3.1 高风险（🔴）

| 风险项 | 描述 |
|--------|------|
| **知识库三套入口并存** | `/api/knowledge/`（内部）、`/open_api/knowledge/`（早期 OpenAPI）、`/v1/datasets`（新 OpenAPI 规范）功能高度重叠，维护成本高，建议统一到 `/v1/datasets` 并弃用旧路由 |
| **Chat API 版本混用** | 内部 `/api/conversation/chat`（Playground）与 Open API `/v1/conversation`、`/v3/chat` 三套并存，`/v3/chat` 为最新规范但内部 Playground 仍用旧路由，未统一 |

### 3.2 中风险（🟡）

| 风险项 | 描述 |
|--------|------|
| **空 stub 实现** | `GetUserRecentlyEditIntelligence`（最近编辑资源）返回空对象，功能未实现 |
| **Admin 前端页面缺失** | 后端 `/api/admin/config/` 系列接口齐全，但前端路由未声明对应管理页面 |
| **OAuth 插件旧路由残留** | `/api/plugin/get_oauth_schema`（旧）与 `/api/plugin_api/get_oauth_schema`（新）并存 |

### 3.3 低风险（🟢）

| 风险项 | 描述 |
|--------|------|
| **收藏列表 URL 版本化不规范** | `/api/marketplace/product/favorite/list.v2` 路径含 `.v2` 后缀，不符合 RESTful 版本化规范（应为 `/v2/...`） |

### 3.4 架构亮点

| 亮点 | 说明 |
|------|------|
| **crossdomain 防腐层** | 项目大量使用 `crossdomain/` 跨域服务（agent, agentrun, app, connector, conversation, database, datacopy, knowledge, message, permission, plugin, search, upload, user, variables, workflow），符合 DDD 防腐层设计，防止 domain 间直接依赖 |
| **多中间件可插拔** | MQ（NSQ/Kafka/RMQ/Pulsar/NATS）、OSS（MinIO/TOS/S3）、向量库（Milvus/VikingDB/OceanBase）均支持配置切换，扩展性好 |

---

## 四、完整功能清单分析（按模块细分）

### 4.1 项目主要功能模块（22 个）

| 模块 | 说明 | 后端包 | 前端包 |
|------|------|--------|--------|
| **用户认证** | 注册、登录、登出、密码重置、Session 管理 | `application/user` | `foundation/account` |
| **工作空间** | Space 管理、开发列表、资源库 | `application/user`, `application/search` | `foundation/space`, `studio/workspace` |
| **Agent (Bot) 管理** | Agent 创建/编辑/复制/删除/发布 | `application/singleagent` | `agent-ide` |
| **对话系统** | Agent 对话 (SSE)、消息管理 | `application/conversation` | `common/chat-area` |
| **工作流引擎** | 可视化工作流编辑/运行/调试/发布 | `application/workflow`, `domain/workflow` | `workflow` |
| **知识库** | 知识库 CRUD、文档处理、RAG 检索 | `application/knowledge` | `data/knowledge` |
| **数据库 (Memory)** | 数据库 CRUD、记录管理、导入 | `application/memory` | `data/memory` |
| **变量系统** | KV 变量、项目变量、Playground 内存 | `application/memory` | `agent-ide`, `data/memory` |
| **插件系统** | 插件注册/开发/调试/发布、OAuth | `application/plugin` | `studio/plugin-forms` |
| **应用/项目 (App)** | App 创建/编辑/复制/删除/发布 | `application/app` | `project-ide` |
| **Prompt 管理** | Prompt 模板 CRUD、官方 Prompt 库 | `application/prompt` | `common/prompt-kit` |
| **模型管理** | LLM 模型配置、多 Provider 支持 | `application/modelmgr`, `bizpkg/config` | `agent-ide/model-manager` |
| **市场/探索** | 插件/模板商店、搜索、收藏 | `application/plugin`, `application/search` | `community/explore` |
| **上传系统** | 文件/图片上传、分片上传、ImageX | `application/upload` | 多模块 |
| **权限/PAT** | Personal Access Token CRUD、API 鉴权 | `application/openauth` | `foundation/account` |
| **管理后台** | 基础配置、知识库配置、模型管理 | `bizpkg/config` | `/admin` 静态页面 |
| **搜索系统** | 全局搜索、ES 索引同步 | `application/search`, `infra/es` | `community/search` |
| **DevOps/调试** | 测试集、Mock、Trace 可视化 | — | `devops` |
| **代码沙箱** | Python 代码节点执行 | `infra/coderunner` | — |
| **文档处理** | PDF/DOCX 解析、OCR、向量化 | `infra/document` | — |

---

### 4.2 各模块功能点明细（含涉及文件、关键方法、完成度）

#### 用户认证 (Passport)

| 功能名称 | 功能说明 | 涉及文件 | 关键方法 | 完成度 |
|----------|----------|----------|----------|--------|
| 邮箱注册 | 邮箱 + 密码注册新用户 | `application/user/`, `idl/passport/passport.thrift` | `PassportWebEmailRegisterV2` | ✅ 完整 |
| 邮箱登录 | 邮箱 + 密码登录，生成 Session | 同上 | `PassportWebEmailLoginPost` | ✅ 完整 |
| 登出 | 清除 Session | 同上 | `PassportWebLogoutGet` | ✅ 完整 |
| 密码重置 | 重置用户密码 | 同上 | `PassportWebEmailPasswordResetGet` | ✅ 完整 |
| Session 校验 | Cookie session_key 校验 | `api/middleware/session.go` | `ValidateSession` | ✅ 完整 |
| 账号信息 | 获取当前用户信息 | 同上 | `PassportAccountInfoV2` | ✅ 完整 |
| 资料更新 | 修改用户名等资料 | 同上 | `UserUpdateProfile` | ✅ 完整 |
| 头像上传 | 上传用户头像 | 同上 | `UserUpdateAvatar` | ✅ 完整 |
| 资料检查 | 更新前检查是否可修改 | 同上 | `UpdateUserProfileCheck` | ✅ 完整 |

**前端页面**：`/sign` 登录页、Account Settings 面板

---

#### 工作空间 (Space)

| 功能名称 | 功能说明 | 涉及文件 | 关键方法 | 完成度 |
|----------|----------|----------|----------|--------|
| Space 列表 | 获取用户空间列表 | `application/user/` | `GetSpaceListV2` | ✅ 完整 |
| 开发列表 | 展示 Agent/App/项目列表，支持过滤搜索 | `application/search/` | `GetDraftIntelligenceList` | ✅ 完整 |
| 资源库 | 插件/工作流/知识库/Prompt/数据库资源 | `application/search/` | `LibraryResourceList`, `ProjectResourceList` | ✅ 完整 |
| 最近编辑 | 最近编辑的智能体/项目 | `application/search/` | `GetUserRecentlyEditIntelligence` | ⚠️ 空 stub |
| 全局搜索 | 搜索资源、Agent、项目 | `infra/es/` | ES 索引搜索 | ✅ 完整 |

**前端路由**：`/space/:space_id/develop` (开发), `/space/:space_id/library` (资源库), `/search/:word` (搜索)

---

#### Agent (Bot) 管理

| 功能名称 | 功能说明 | 涉及文件 | 关键方法 | 完成度 |
|----------|----------|----------|----------|--------|
| 创建 Agent | 创建草稿 Agent，自动设置默认模型 | `application/singleagent/` | `CreateSingleAgentDraft` | ✅ 完整 |
| 编辑 Agent | 更新名称/模型/插件/知识库/变量 | 同上 | `UpdateSingleAgentDraft` | ✅ 完整 |
| 删除 Agent | 删除草稿 Agent + 发布事件 | 同上 | `DeleteAgentDraft` | ✅ 完整 |
| 复制 Agent | 复制 Agent (含变量/插件/快捷命令) | 同上 | `DuplicateDraftBot` | ✅ 完整 |
| Agent 信息 | 获取完整 Agent 信息 | 同上 | `GetAgentBotInfo` | ✅ 完整 |
| 发布 Agent | 并行发布到多个 Connector | 同上 | `PublishAgent` | ✅ 完整 |
| 发布历史 | 查看发布历史记录 | 同上 | `ListAgentPublishHistory` | ✅ 完整 |
| 发布渠道 | 获取已发布渠道列表 | 同上 | `GetPublishConnectorList` | ✅ 完整 |
| Agent 在线信息 | Open API 获取在线 Agent 信息 | 同上 | `GetAgentOnlineInfo`, `OpenGetBotInfo` | ✅ 完整 |
| Agent 展示信息 | 获取/更新 Agent 展示信息 | 同上 | `Get/UpdateAgentDraftDisplayInfo` | ✅ 完整 |
| 弹窗计数 | Agent 弹窗信息管理 | 同上 | `Get/UpdateAgentPopupInfo` | ✅ 完整 |
| 行为上报 | 上报最近打开记录 | 同上 | `ReportUserBehavior` | ✅ 完整 |
| 绑定数据库 | 绑定/解绑数据库到 Agent | 同上 | `BindDatabase`, `UnBindDatabase` | ✅ 完整 |
| 模式选择 | Single LLM / Workflow 模式切换 | 前端 `agent-ide/entry` | — | ✅ 完整 |

---

#### 对话系统

| 功能名称 | 功能说明 | 涉及文件 | 关键方法 | 完成度 |
|----------|----------|----------|----------|--------|
| Agent 对话 (内部) | Playground SSE 流式对话 | `application/conversation/` | `Run` | ✅ 完整 |
| Agent 对话 (Open API) | v3 Chat SSE/同步对话 | 同上 | `OpenapiAgentRun`, `OpenapiAgentRunSync` | ✅ 完整 |
| 取消对话 | 取消进行中的对话 | 同上 | `CancelRun` | ✅ 完整 |
| 获取运行记录 | 查看对话运行状态 | 同上 | `RetrieveRunRecord` | ✅ 完整 |
| 创建会话 | 创建新会话 | 同上 | `CreateConversation` | ✅ 完整 |
| 会话列表 | 列出会话 | 同上 | `ListConversation` | ✅ 完整 |
| 更新/删除会话 | 会话 CRUD | 同上 | `Update/DeleteConversation` | ✅ 完整 |
| 消息列表 | 查看消息列表 | 同上 | `GetMessageList`, `GetApiMessageList` | ✅ 完整 |
| 删除/中断消息 | 删除消息、中断生成 | 同上 | `DeleteMessage`, `BreakMessage` | ✅ 完整 |
| 清除上下文/历史 | 创建新分段、清空历史 | 同上 | `CreateSection`, `ClearHistory` | ✅ 完整 |

---

#### 工作流引擎（23 项功能）

| 功能名称 | 涉及文件 | 关键方法 | 完成度 |
|----------|----------|----------|--------|
| 创建工作流 | `application/workflow/` | `CreateWorkflow` | ✅ 完整 |
| 保存工作流 | 同上 | `SaveWorkflow` | ✅ 完整 |
| 工作流详情/画布 | 同上 | `GetWorkflowDetail`, `GetCanvasInfo` | ✅ 完整 |
| 删除工作流 | 同上 | `DeleteWorkflow`, `BatchDeleteWorkflow` | ✅ 完整 |
| 复制工作流/模板 | 同上 | `CopyWorkflow`, `CopyWkTemplateApi` | ✅ 完整 |
| 发布工作流 | 同上 | `PublishWorkflow` | ✅ 完整 |
| 测试运行/恢复/取消 | 同上 | `TestRun`, `TestResume`, `Cancel` | ✅ 完整 |
| 节点调试 | 同上 | `NodeDebug` | ✅ 完整 |
| 运行进度/节点执行历史 | 同上 | `GetProcess`, `GetNodeExecuteHistory` | ✅ 完整 |
| 校验工作流/引用查询 | 同上 | `ValidateTree`, `GetWorkflowReferences` | ✅ 完整 |
| 节点类型/模板 | 同上 | `QueryWorkflowNodeTypes`, `GetNodeTemplateList` | ✅ 完整 |
| LLM FC 设置 | 同上 | `GetLLMNodeFCSettingDetail/Merged` | ✅ 完整 |
| ChatFlow 角色 | 同上 | `Create/Delete/GetChatFlowRole` | ✅ 完整 |
| 项目对话定义 | 同上 | `Create/Update/Delete/ListApplicationConversationDef` | ✅ 完整 |
| Trace 追踪 | 同上 | `GetTraceSDK`, `ListRootSpans` | ✅ 完整 |
| Open API 运行 | 同上 | `OpenAPIRun`, `OpenAPIStreamRun` | ✅ 完整 |
| ChatFlow 运行 | 同上 | `OpenAPIChatFlowRun` | ✅ 完整 |
| 示例工作流 | 同上 | `GetExampleWorkFlowList` | ✅ 完整 |

---

#### 工作流节点类型（47 种）

| 分类 | 节点 | 说明 |
|------|------|------|
| **输入输出** | Entry, Exit, InputReceiver, OutputEmitter | 工作流入口/出口/输入/输出 |
| **AI/LLM** | LLM | 大语言模型调用 |
| **逻辑控制** | Selector, Loop, Batch, Break, Continue, IntentDetector | 条件分支、循环、批处理、意图识别 |
| **变量** | VariableAssigner, VariableAssignerWithinLoop, VariableAggregator | 变量赋值/合并 |
| **数据** | KnowledgeRetriever, KnowledgeIndexer, KnowledgeDeleter, Plugin | RAG + 插件 |
| **数据库** | DatabaseQuery, DatabaseInsert, DatabaseUpdate, DatabaseDelete, DatabaseCustomSQL | 数据库操作 |
| **工具** | HTTPRequester, CodeRunner, TextProcessor, SubWorkflow | HTTP 请求、代码执行、子工作流 |
| **JSON** | JsonSerialization, JsonDeserialization | 序列化/反序列化 |
| **对话管理** | CreateConversation, ConversationUpdate, ConversationDelete, ConversationList, ConversationHistory, ClearConversationHistory | 对话操作 |
| **消息** | MessageList, CreateMessage, EditMessage, DeleteMessage | 消息操作 |
| **其他** | QuestionAnswer, Comment, Lambda | 用户提问、注释、占位 |

---

#### 知识库

| 功能名称 | 涉及文件 | 关键方法 | 完成度 |
|----------|----------|----------|--------|
| 创建/列表/详情/更新/删除知识库 | `application/knowledge/` | `CreateKnowledge`, `ListKnowledge`, `DatasetDetail` 等 | ✅ 完整 |
| 创建文档/文档列表/进度/重新分段 | 同上 | `CreateDocument`, `ListDocument`, `GetDocumentProgress`, `Resegment` | ✅ 完整 |
| 片段 CRUD | 同上 | `Create/List/Update/DeleteSlice` | ✅ 完整 |
| 表 Schema / 文档审核 | 同上 | `GetTableSchema`, `ValidateTableSchema`, `CreateDocumentReview` | ✅ 完整 |
| 图片知识库 | 同上 | `ListPhoto`, `ExtractPhotoCaption` | ✅ 完整 |
| 复制/移动 | 同上 | `CopyKnowledge`, `MoveKnowledgeToLibrary` | ✅ 完整 |
| Open API | 同上 | `*OpenAPI` 方法 | ✅ 完整 |

**文档处理能力**（`infra/document/`）：PDF/TXT/DOC/DOCX/Markdown/CSV/XLSX/JSON 解析；JPG/PNG 图片；Volcengine OCR、PaddleOCR；VikingDB Rerank、RRF；NL2SQL；向量索引 VikingDB/OceanBase

---

#### 数据库 (Memory)

| 功能名称 | 涉及文件 | 关键方法 | 完成度 |
|----------|----------|----------|--------|
| 数据库 CRUD / 记录管理 | `application/memory/` | `Add/Update/DeleteDatabase`, `ListDatabaseRecords`, `UpdateDatabaseRecords` | ✅ 完整 |
| 绑定 Bot | 同上 | `BindDatabase`, `UnBindDatabase` | ✅ 完整 |
| 表 Schema / 数据导入/进度 | 同上 | `GetDatabaseTableSchema`, `SubmitDatabaseInsertTask` | ✅ 完整 |
| 模板导出 / 复制/移动 | 同上 | `GetDatabaseTemplate`, `CopyDatabase`, `MoveDatabaseToLibrary` | ✅ 完整 |
| 模式配置 / 连接器名称 | 同上 | `GetModeConfig`, `GetConnectorName` | ⚠️ 硬编码返回 |

---

#### 变量系统

| 功能名称 | 涉及文件 | 关键方法 | 完成度 |
|----------|----------|----------|--------|
| 系统变量配置 | `application/memory/` | `GetSysVariableConf` | ✅ 完整 |
| 项目变量 | 同上 | `GetProjectVariablesMeta`, `UpdateProjectVariable` | ✅ 完整 |
| 变量实例 | 同上 | `SetVariableInstance`, `GetPlayGroundMemory`, `DeleteVariableInstance` | ✅ 完整 |

---

#### 插件系统

| 功能名称 | 涉及文件 | 关键方法 | 完成度 |
|----------|----------|----------|--------|
| 注册/更新/删除插件 | `application/plugin/` | `RegisterPlugin`, `UpdatePlugin`, `DelPlugin` | ✅ 完整 |
| API CRUD / API 调试 | 同上 | `CreateAPI`, `UpdateAPI`, `DeleteAPI`, `DebugAPI` | ✅ 完整 |
| 发布插件 | 同上 | `PublishPlugin` | ✅ 完整 |
| OpenAPI 转换 / OAuth 流程 | 同上 | `Convert2OpenAPI`, `GetOAuthSchema`, `OauthAuthorizationCode` | ✅ 完整 |
| 编辑锁 | 同上 | `CheckAndLockPluginEdit`, `UnlockPluginEdit` | ⚠️ Stub (总是返回 true) |
| Bot 默认参数 / 资源复制 | 同上 | `GetBotDefaultParams`, `ResourceCopyDispatch/Detail/Retry/Cancel` | ✅ 完整 |

**22 个内置插件**：文库搜索、博查搜索、Wolfram Alpha、创客贴设计、搜狐热闻、图片压缩、什么值得买、板栗看板、天眼查、飞书 (认证/消息/多维表格/电子表格/任务/云文档/知识库/日历)、高德地图

---

#### 应用/项目 (App)

| 功能名称 | 涉及文件 | 关键方法 | 完成度 |
|----------|----------|----------|--------|
| 创建/更新/删除项目 | `application/app/` | `DraftProjectCreate`, `DraftProjectUpdate`, `DraftProjectDelete` | ✅ 完整 |
| 复制项目 | 同上 | `DraftProjectCopy` | ✅ 完整 |
| 发布项目 / 版本检查 | 同上 | `PublishAPP`, `CheckProjectVersionNumber` | ✅ 完整 |
| 发布记录/渠道 | 同上 | `GetPublishRecordList/Detail`, `ProjectPublishConnectorList` | ✅ 完整 |
| 在线应用数据 (Open API) | 同上 | `GetOnlineAppData` | ✅ 完整 |

---

#### 其他模块（Prompt / 模型 / 市场 / 上传 / PAT / Admin / DevOps / 代码沙箱）

- **Prompt**：Upsert/删除/获取/官方列表 → `application/prompt/`
- **模型**：列表/创建/删除/Provider 列表 → `application/modelmgr/`，支持 OpenAI/Claude/Gemini/Qwen/DeepSeek/Ollama/Ark/BytePlus
- **市场**：产品列表/详情/搜索/分类/收藏/复制 → `application/plugin/`, `application/search/`
- **上传**：通用/分片/图片/Open API 上传、图标获取 → `application/upload/`
- **PAT**：创建/列表/详情/更新/删除/权限校验/模拟用户 → `application/openauth/`
- **Admin**：基础配置/知识库配置/模型管理 → `bizpkg/config/`
- **DevOps**：测试集/Mock 集/调试面板/JSON 预览 → `frontend/packages/devops/`
- **代码沙箱**：Python 直接/沙箱执行 → `infra/coderunner/`；JavaScript → ❌ 未实现

---

### 4.3 事件总线 & 后台消费者

| 功能名称 | 涉及文件 | Topic | 完成度 |
|----------|----------|-------|--------|
| 资源搜索同步 | `infra/eventbus/handler_resource` | `opencoze_search_resource` | ✅ 完整 |
| 项目搜索同步 | `infra/eventbus/handler_project` | `opencoze_search_app` | ✅ 完整 |
| 知识库索引 | `infra/eventbus/knowledgeEventHandler` | `opencoze_knowledge` | ✅ 完整 |

**支持 MQ**：NSQ / Kafka / RocketMQ / Pulsar / NATS

---

### 4.4 暂时无法确认的功能点

| 功能点 | 说明 | 依据 |
|--------|------|------|
| **定时任务 (Scheduled Tasks)** | IDL 有 `CronExpr`、`ScheduledTaskTabStatus`，但后端无 cron 实现 | `api/model/app/bot_common/bot_common.go` |
| **编辑锁** | `CheckAndLockPluginEdit` 始终返回 `Seized: true` | 可能预留分布式锁 |
| **连接器名称** | `GetConnectorName` 硬编码返回 Coze/Chat SDK/API | 可能预留更多 Connector |
| **JavaScript 代码节点** | Code Runner 返回 "not supported yet" | 预留功能 |
| **Webhook / 回调** | 未发现 Webhook 推送机制 | 仅有 Connector 发布概念 |
| **多租户隔离** | 单 Space 结构 | 数据按 SpaceID 隔离 |
| **审计日志** | 未发现操作审计模块 | 仅有 AccessLog |
| **限流/熔断** | 未发现 Rate Limiting / Circuit Breaker | CORS 全开 |

---

### 4.5 最核心的 10 个功能

| 排名 | 功能 | 核心度说明 |
|------|------|------------|
| **1** | **工作流引擎** | 47 种节点，可视化编辑/测试/调试/发布，DAG 验证，SSE 流式执行，ChatFlow 模式 |
| **2** | **Agent (Bot) 对话** | SSE 流式 Agent 对话，Playground + Open API (v3 Chat) |
| **3** | **知识库 RAG 系统** | 文本/表格/图片知识库，PDF/DOCX/OCR，向量检索，NL2SQL，Rerank |
| **4** | **Agent 管理** | 全生命周期管理，绑定模型/插件/知识库/数据库/变量 |
| **5** | **插件系统** | 22 个内置插件，OAuth，OpenAPI 转换 |
| **6** | **多模型支持** | 8+ LLM Provider，22 个模型模板 |
| **7** | **数据库/变量系统** | 结构化存储 + KV 变量 + 项目变量 |
| **8** | **Open API 体系** | v1/v3，PAT 认证，Chat/Conversation/Workflow/Knowledge/File |
| **9** | **App/项目管理** | 复合 App 管理，VS Code 风格项目 IDE |
| **10** | **事件驱动搜索** | MQ → ES 索引 + 知识库向量索引 |

---

### 4.6 错误码分布

| 模块 | 错误码数量 | 文件 |
|------|-----------|------|
| Workflow | ~49 | `types/errno/workflow.go` |
| Knowledge | ~38 | `types/errno/knowledge.go` |
| Memory | ~27 | `types/errno/memory.go` |
| Plugin | ~22 | `types/errno/plugin.go` |
| Agent | ~14 | `types/errno/agent.go` |
| Conversation | ~14 | `types/errno/conversation.go` |
| Upload | ~11 | `types/errno/upload.go` |
| 其他 | ~31 | 多个模块 |
| **合计** | **~206** | |

---

## 五、相关文档索引

| 文档 | 说明 |
|------|------|
| `coze-studio-project-cognitive-map.md` | 项目认知地图、目录结构、启动流程 |
| `coze-studio-tech-stack-analysis.md` | 技术栈分析 |
| `coze-studio-feature-inventory.md` | 功能点清单（按模块） |
| `coze-studio-api-inventory.md` | API 接口清单 |
