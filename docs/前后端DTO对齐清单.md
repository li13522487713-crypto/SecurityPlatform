# 前后端 DTO 对齐清单

## 目的

- 建立统一的接口契约，减少前后端不一致导致的集成风险。
- 明确当前已对齐项与待修复项。

## 通用模型

| 模型 | 后端定义 | 前端定义 | 状态 | 备注 |
| --- | --- | --- | --- | --- |
| ApiResponse | `src/backend/Atlas.Core/Models/ApiResponse.cs` | `src/frontend/Atlas.WebApp/src/types/api.ts` | 已对齐 | `data` 前端可选，后端可为空 |
| PagedRequest | `src/backend/Atlas.Core/Models/PagedRequest.cs` | `src/frontend/Atlas.WebApp/src/types/api.ts` | 部分对齐 | 后端字段为必填，前端为可选 |
| PagedResult | `src/backend/Atlas.Core/Models/PagedResult.cs` | `src/frontend/Atlas.WebApp/src/types/api.ts` | 已对齐 | - |

## 资产/审计/告警

| 模块 | 后端定义 | 前端定义 | 状态 | 备注 |
| --- | --- | --- | --- | --- |
| Assets | `src/backend/Atlas.Application.Assets/Models` | `src/frontend/Atlas.WebApp/src/services/api.ts` | 已对齐 | `AssetListItem` 字段一致 |
| Audit | `src/backend/Atlas.Application.Audit/Models` | `src/frontend/Atlas.WebApp/src/services/api.ts` | 已对齐 | `AuditListItem` 字段一致 |
| Alert | `src/backend/Atlas.Application.Alert/Models` | `src/frontend/Atlas.WebApp/src/services/api.ts` | 已对齐 | `AlertListItem` 字段一致 |

## 身份与权限

| 模块 | 后端定义 | 前端定义 | 状态 | 备注 |
| --- | --- | --- | --- | --- |
| Users/Roles/Permissions/Menus/Departments | `src/backend/Atlas.Application/Identity/Models` | `src/frontend/Atlas.WebApp/src/types` | 待补充 | 前端缺少 DTO 与枚举定义 |

## 审批流 DTO

| DTO | 后端定义 | 前端定义 | 状态 | 备注 |
| --- | --- | --- | --- | --- |
| ApprovalFlowDefinition* | `src/backend/Atlas.Application.Approval/Models` | `src/frontend/Atlas.WebApp/src/types/api.ts` | 部分对齐 | `Id`/`DefinitionId`/`InitiatorUserId` 为 long，前端为 string |
| ApprovalStartRequest | `src/backend/Atlas.Application.Approval/Models/ApprovalStartRequest.cs` | `src/frontend/Atlas.WebApp/src/types/api.ts` | 不一致 | `definitionId` 后端为 long，前端为 string |
| ApprovalTaskDecideRequest | `src/backend/Atlas.Application.Approval/Models/ApprovalTaskDecideRequest.cs` | `src/frontend/Atlas.WebApp/src/types/api.ts` | 不一致 | 实际接口使用路由参数决定操作 |
| ApprovalOperationRequest | `src/backend/Atlas.Application.Approval/Models/ApprovalOperationRequest.cs` | 前端缺失 | 待补充 | 缺少运行时操作请求模型 |
| ApprovalCopyRecordResponse | `src/backend/Atlas.Application.Approval/Models/ApprovalCopyRecordResponse.cs` | 前端缺失 | 待补充 | 缺少抄送记录响应模型 |

## 审批流枚举

| 枚举 | 后端定义 | 前端定义 | 状态 | 备注 |
| --- | --- | --- | --- | --- |
| ApprovalFlowStatus | `src/backend/Atlas.Domain.Approval/Enums/ApprovalFlowStatus.cs` | `src/frontend/Atlas.WebApp/src/types/api.ts` | 已对齐 | - |
| ApprovalInstanceStatus | `src/backend/Atlas.Domain.Approval/Enums/ApprovalInstanceStatus.cs` | `src/frontend/Atlas.WebApp/src/types/api.ts` | 已对齐 | - |
| ApprovalTaskStatus | `src/backend/Atlas.Domain.Approval/Enums/ApprovalTaskStatus.cs` | `src/frontend/Atlas.WebApp/src/types/api.ts` | 部分对齐 | 前端缺少 `Waiting` |
| AssigneeType | `src/backend/Atlas.Domain.Approval/Enums/AssigneeType.cs` | `src/frontend/Atlas.WebApp/src/types/api.ts` | 不一致 | 前端仅覆盖 3 种 |
| ApprovalMode | `src/backend/Atlas.Domain.Approval/Enums/ApprovalMode.cs` | 前端缺失 | 待补充 | 会签/或签/顺序会签 |
| FlowNodeType | `src/backend/Atlas.Domain.Approval/Enums/FlowNodeType.cs` | 前端缺失 | 待补充 | 节点类型 |
| ApprovalOperationType | `src/backend/Atlas.Domain.Approval/Enums/ApprovalOperationType.cs` | 前端缺失 | 待补充 | 运行时操作类型 |

## 接口路径对齐（审批流）

| 前端调用 | 后端实际 | 状态 | 备注 |
| --- | --- | --- | --- |
| `POST /approval/instances` | `POST /api/approval/runtime/start` | 不一致 | 路由不同 |
| `GET /approval/instances/my` | `GET /api/approval/runtime/my-instances` | 不一致 | 路由不同 |
| `GET /approval/instances/{id}` | `GET /api/approval/runtime/instances/{id}` | 不一致 | 路由不同 |
| `GET /approval/instances/{id}/history` | `GET /api/approval/runtime/instances/{id}/history` | 不一致 | 路由不同 |
| `POST /approval/instances/{id}/cancel` | `POST /api/approval/runtime/instances/{id}/cancel` | 不一致 | 路由不同 |
| `GET /approval/tasks/my` | `GET /api/approval/tasks/my-tasks` | 不一致 | 路由不同 |
| `GET /approval/tasks/instance/{id}` | `GET /api/approval/tasks/by-instance/{id}` | 不一致 | 路由不同 |
| `POST /approval/tasks/decide` | `POST /api/approval/tasks/{taskId}/approve|reject` | 不一致 | 接口设计不同 |

## 接口路径对齐（基础模块）

| 前端调用（API_BASE 为 `/api`） | 后端实际 | 状态 | 备注 |
| --- | --- | --- | --- |
| `/api/assets` | `/assets` | 需确认 | 如无网关前缀需统一 |
| `/api/alert` | `/alert` | 需确认 | 如无网关前缀需统一 |
| `/api/audit` | `/audit` | 需确认 | 如无网关前缀需统一 |
| `/api/auth/token` | `/auth/token` | 需确认 | 如无网关前缀需统一 |

## 建议修复顺序

1. 统一审批流接口路径（前端或后端其一）。
2. 补齐审批流枚举与运行时操作 DTO。
3. 统一 ID 类型（推荐前端使用 `number` 或 `string` 但保持一致）。
4. 建立身份与权限模块的前端 DTO。
