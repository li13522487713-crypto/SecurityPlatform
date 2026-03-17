# SEC-14 Project 可选子域规则（含 SEC-55/56）

## 1. 任务信息

- Linear：`SEC-14`（`[P1] 将 Project 降级为可选子域`）
- 覆盖子任务：`SEC-55`、`SEC-56`
- 所属里程碑：`P1 模型与边界收口`

## 2. 主链条与可选子域关系

```text
默认主链条：Tenant -> TenantApp(AppSpace)
可选子域：ProjectAsset（仅在启用项目模式时出现）
```

## 3. SEC-55：projectId 使用场景盘点结论

| 场景 | 是否必须 Project | 说明 |
|---|---|---|
| 应用内多团队协作分域 | 是 | 需要资源分区与权限分区 |
| 单团队应用快速构建 | 否 | 可直接在 TenantApp 下运行 |
| 运行态数据提交 | 可选 | 由应用配置决定是否启用 |
| 平台治理配置 | 否 | 不应依赖 projectId |

## 4. SEC-56：启用条件与生命周期

## 4.1 启用条件

- 应用开启 `EnableProjectScope=true`；
- 组织存在明确项目隔离需求；
- 权限模型已配置项目级资源。

## 4.2 生命周期

| 阶段 | 动作 |
|---|---|
| 创建 | 在 TenantApp 下创建 ProjectAsset |
| 激活 | 绑定成员、部门、职位与资源 |
| 运行 | 通过 `X-Project-Id` 参与数据范围控制 |
| 归档/停用 | 保留审计，不再承接新运行数据 |
| 退出 | 解绑资源后删除或只读归档 |

## 4.3 与 AppSpace 的关系

```mermaid
flowchart LR
  A[TenantApp(AppSpace)] --> P1[ProjectAsset-A]
  A --> P2[ProjectAsset-B]
  A --> P3[No Project Mode]
```

## 5. `tenantId / appId / projectId` 使用规则

| 字段 | 是否必填 | 规则 |
|---|---|---|
| `tenantId` | 是 | 全链路必填，且与 JWT 一致 |
| `appId` | 是（应用域请求） | 由路由或上下文确定 |
| `projectId` | 条件必填 | 仅在应用启用项目模式时必填 |

## 6. 风险与约束

| 风险 | 约束 |
|---|---|
| projectId 被误当主上下文 | 禁止在平台控制台默认要求 projectId |
| 资源误跨项目读写 | 强制数据层按 project scope 过滤 |
| 旧接口无 project 语义 | 在 contracts 明确兼容行为 |

## 7. 任务映射核验

| 任务号 | 对应章节 |
|---|---|
| SEC-14 | 第2~6章 |
| SEC-55 | 第3章 |
| SEC-56 | 第4章 |

## 8. 完成定义核验

- [x] Project 已降级为可选子域并有清晰启用条件  
- [x] 生命周期与 AppSpace 关系明确  
- [x] `tenantId/appId/projectId` 使用规则可直接用于实施
