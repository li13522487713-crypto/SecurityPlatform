# 微流树 CRUD UI

## 范围

- 路由：`/space/:workspaceId/mendix-studio/:appId`。
- 后端：`/api/v1/microflows` 与 `/api/v1/microflow-folders`。
- 前端：`@atlas/mendix-studio-core` 的 `components/app-explorer/` 与 `microflow/tree-crud/`。

## 树结构

- 顶层按应用模块展示。
- 每个模块下保留 Domain Model、Pages、Microflows、Workflows、Security 等分组。
- Microflows 分组按模块加载微流与文件夹，文件夹支持多级嵌套，微流按 `folderId` 挂载，未分组微流展示在模块 Microflows 根下。

## 操作

- Microflows 根/文件夹：新建微流、新建文件夹、刷新、属性占位。
- 文件夹：支持重命名；删除非空文件夹由后端 `MICROFLOW_FOLDER_NOT_EMPTY` 拦截。
- 微流：打开、重命名、复制、删除、查看引用。
- 删除微流前先拉取引用，存在 active caller 时阻止；后端 409 作为兜底。

## API 对照

| 操作 | API |
|---|---|
| 列出文件夹 | `GET /api/v1/microflow-folders?workspaceId=&moduleId=` |
| 文件夹树 | `GET /api/v1/microflow-folders/tree?workspaceId=&moduleId=` |
| 新建文件夹 | `POST /api/v1/microflow-folders` |
| 重命名文件夹 | `POST /api/v1/microflow-folders/{id}/rename` |
| 移动文件夹 | `POST /api/v1/microflow-folders/{id}/move` |
| 删除文件夹 | `DELETE /api/v1/microflow-folders/{id}` |
| 移动微流 | `POST /api/v1/microflows/{id}/move` |

## 已知限制

- 批量移动、属性编辑面板和文件夹删除确认弹窗仍作为后续增强。
- 本轮保留旧 `components/app-explorer.tsx`、`components/app-explorer-tree.tsx`、`components/microflow-tree-section.tsx` re-export 以兼容现有导入。
