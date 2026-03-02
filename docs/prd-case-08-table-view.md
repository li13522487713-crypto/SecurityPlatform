# PRD Case 08：自定义表格视图闭环

## 1. 背景与目标

中后台列表页需要“个人视图”能力：每个用户可保存列配置、密度、分页和默认视图，提高操作效率并保持租户内用户隔离。

## 2. 用户角色与权限矩阵

| 角色 | 保存视图 | 设为默认 | 删除视图 | 查看他人视图 |
|---|---|---|---|---|
| 业务用户 | ✓ | ✓ | ✓ | - |
| 管理员 | ✓ | ✓ | ✓ | - |

## 3. 交互流程图

```mermaid
flowchart LR
  openTable[打开列表页] --> adjustColumns[调整列配置]
  adjustColumns --> saveView[保存视图]
  saveView --> setDefault[设为默认]
  setDefault --> nextVisit[下次访问自动加载]
```

## 4. 数据模型

| 实体 | 关键字段 | 说明 |
|---|---|---|
| TableView | TenantId, UserId, TableKey, ViewName, ConfigJson | 视图定义 |
| UserTableViewDefault | TenantId, UserId, TableKey, ViewId | 默认视图映射 |

`ConfigJson` 建议包含：`columns`、`density`、`pagination`、`sort`、`filters`。

## 5. API 规范

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/v1/table-views?tableKey={key}` | 查询视图列表 |
| POST | `/api/v1/table-views` | 创建视图 |
| PUT | `/api/v1/table-views/{id}` | 更新视图 |
| DELETE | `/api/v1/table-views/{id}` | 删除视图 |
| PUT | `/api/v1/table-views/{id}/default` | 设置默认视图 |

写接口必须包含 `Idempotency-Key` 与 `X-CSRF-TOKEN`。

## 6. 前端页面要素

- 视图切换器：显示当前表格的可用视图。
- 列配置面板：显示/隐藏、顺序拖拽、宽度、固定列。
- 视图管理弹窗：重命名、复制、删除、设为默认。
- 默认视图自动加载提示。

## 7. 审计事件字典

| 事件 | 对象 | 描述 |
|---|---|---|
| TABLE_VIEW_CREATE | TableView | 创建视图 |
| TABLE_VIEW_UPDATE | TableView | 修改视图 |
| TABLE_VIEW_DELETE | TableView | 删除视图 |
| TABLE_VIEW_SET_DEFAULT | UserTableViewDefault | 设置默认视图 |

## 8. 验收标准

- [ ] 用户可保存当前列表配置为新视图。
- [ ] 默认视图在刷新和重新登录后生效。
- [ ] 同租户不同用户视图互不影响。
- [ ] 删除视图后默认映射自动回退。
- [ ] 视图写操作通过幂等和 CSRF 校验。

## 9. 等保映射

| 控制点 | 对应能力 |
|---|---|
| 8.1.4 访问控制 | 按用户边界隔离个人配置 |
| 8.1.5 审计要求 | 视图配置变更可追踪 |
