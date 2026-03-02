# 字典管理 + 参数管理实施计划

> Phase 1 功能，为下拉选项、状态码等固定数据提供统一维护入口，同时集中管理系统动态参数。
> 等保2.0关联要求：参数配置需有变更审计，防止未授权修改影响系统安全策略。

---

## 一、功能说明

### 1.1 字典管理

维护系统中经常使用的固定数据集合（如性别、状态、类型、等级等），分为：

- **字典类型（DictType）**：对数据分类，如 `sys_user_sex`、`sys_normal_disable`。
- **字典数据（DictData）**：具体的键值对，如 `{label: "男", value: "0"}`。

前端下拉框、状态标签等通过字典类型编码查询字典数据。

### 1.2 参数管理

维护系统动态配置参数（如文件上传路径、功能开关、页面配置等），支持：

- 内置参数（系统预置，不可删除，只读 Key）。
- 自定义参数（用户可增删改）。
- 按 Key 快速查询（供业务模块使用）。

---

## 二、数据模型

### DictType（字典类型）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long | 雪花ID |
| TenantIdValue | Guid | 租户隔离 |
| Code | string | 字典类型编码，唯一，如 `sys_user_sex` |
| Name | string | 类型名称，如"用户性别" |
| Status | bool | 是否启用（true=启用） |
| Remark | string? | 备注 |

### DictData（字典数据）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long | 雪花ID |
| TenantIdValue | Guid | 租户隔离 |
| DictTypeCode | string | 关联的字典类型编码 |
| Label | string | 显示标签，如"男" |
| Value | string | 实际值，如"0" |
| SortOrder | int | 排序，越小越靠前 |
| Status | bool | 是否启用 |
| CssClass | string? | 前端样式类（可选） |
| ListClass | string? | 表格标签样式（可选） |

### SystemConfig（系统参数）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long | 雪花ID |
| TenantIdValue | Guid | 租户隔离 |
| ConfigKey | string | 参数键，唯一 |
| ConfigValue | string | 参数值 |
| ConfigName | string | 参数名称 |
| IsBuiltIn | bool | 是否内置（内置不可删除） |
| Remark | string? | 备注 |

---

## 三、API 契约

### 3.1 字典类型接口

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| GET | `/api/v1/dict-types` | 分页查询字典类型 | `dict:type:view` |
| GET | `/api/v1/dict-types/all` | 查询所有启用字典类型 | `dict:type:view` |
| GET | `/api/v1/dict-types/{id}` | 字典类型详情 | `dict:type:view` |
| POST | `/api/v1/dict-types` | 创建字典类型 | `dict:type:create` |
| PUT | `/api/v1/dict-types/{id}` | 更新字典类型 | `dict:type:update` |
| DELETE | `/api/v1/dict-types/{id}` | 删除字典类型 | `dict:type:delete` |

### 3.2 字典数据接口

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| GET | `/api/v1/dict-types/{code}/data` | 按类型编码查询字典数据（含分页） | `dict:data:view` |
| GET | `/api/v1/dict-data/by-code/{code}` | 按字典类型编码查所有启用数据（供前端下拉使用） | 已登录即可 |
| POST | `/api/v1/dict-types/{code}/data` | 新增字典数据 | `dict:data:create` |
| PUT | `/api/v1/dict-data/{id}` | 更新字典数据 | `dict:data:update` |
| DELETE | `/api/v1/dict-data/{id}` | 删除字典数据 | `dict:data:delete` |

### 3.3 参数管理接口

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| GET | `/api/v1/system-configs` | 分页查询参数 | `config:view` |
| GET | `/api/v1/system-configs/by-key/{key}` | 按 Key 查询参数值 | 已登录即可 |
| POST | `/api/v1/system-configs` | 创建参数 | `config:create` |
| PUT | `/api/v1/system-configs/{id}` | 更新参数 | `config:update` |
| DELETE | `/api/v1/system-configs/{id}` | 删除参数（内置不可删） | `config:delete` |

---

## 四、后端实现步骤

### 4.1 Domain 层

新增实体文件：
- `Atlas.Domain/System/Entities/DictType.cs`
- `Atlas.Domain/System/Entities/DictData.cs`
- `Atlas.Domain/System/Entities/SystemConfig.cs`

### 4.2 Application 层

新增文件：
- `Atlas.Application/System/Models/DictModels.cs`（DTOs）
- `Atlas.Application/System/Models/SystemConfigModels.cs`
- `Atlas.Application/System/Validators/DictValidators.cs`
- `Atlas.Application/System/Validators/SystemConfigValidators.cs`
- `Atlas.Application/System/Abstractions/IDictQueryService.cs`
- `Atlas.Application/System/Abstractions/IDictCommandService.cs`
- `Atlas.Application/System/Abstractions/ISystemConfigQueryService.cs`
- `Atlas.Application/System/Abstractions/ISystemConfigCommandService.cs`

### 4.3 Infrastructure 层

新增文件：
- `Atlas.Infrastructure/Repositories/DictTypeRepository.cs`
- `Atlas.Infrastructure/Repositories/DictDataRepository.cs`
- `Atlas.Infrastructure/Repositories/SystemConfigRepository.cs`
- `Atlas.Infrastructure/Services/DictQueryService.cs`
- `Atlas.Infrastructure/Services/DictCommandService.cs`
- `Atlas.Infrastructure/Services/SystemConfigQueryService.cs`
- `Atlas.Infrastructure/Services/SystemConfigCommandService.cs`

更新：
- `Atlas.Infrastructure/DependencyInjection/CoreServiceRegistration.cs`（注册新服务）
- `Atlas.Infrastructure/Services/DatabaseInitializerHostedService.cs`（InitTables + 内置参数种子）

### 4.4 WebApi 层

新增文件：
- `Atlas.WebApi/Controllers/DictTypesController.cs`
- `Atlas.WebApi/Controllers/DictDataController.cs`
- `Atlas.WebApi/Controllers/SystemConfigsController.cs`
- `Atlas.WebApi/Bosch.http/DictTypes.http`
- `Atlas.WebApi/Bosch.http/SystemConfigs.http`

更新：
- `Atlas.WebApi/Authorization/PermissionPolicies.cs`（新增字典/参数权限常量）

---

## 五、前端实现步骤

### 5.1 Service 层

- `src/services/dict.ts`：getDictTypes、getDictTypeById、getDictDataByCode、createDictType、updateDictType、deleteDictType、createDictData、updateDictData、deleteDictData
- `src/services/system-config.ts`：getSystemConfigs、getSystemConfigByKey、createSystemConfig、updateSystemConfig、deleteSystemConfig

### 5.2 页面

- `src/pages/system/DictTypesPage.vue`：
  - 左侧字典类型列表（分页、搜索、新增/编辑/删除）
  - 右侧字典数据详情（选中类型后展示，支持新增/编辑/删除/排序）
- `src/pages/system/SystemConfigsPage.vue`：
  - 参数列表（分页、搜索、新增/编辑/删除）
  - 内置参数显示锁定标志，不显示删除按钮

### 5.3 路由与菜单

- 路由：`/system/dict`、`/system/configs`
- 在路由 index.ts 中注册

---

## 六、等保2.0 合规要点

- 字典类型编码、参数 Key 不允许包含 SQL 注入特殊字符（FluentValidation 正则校验）。
- 参数修改操作必须写入审计日志（AuditRecorder）。
- 内置参数不允许删除，防止系统基础配置被破坏。
- 所有写接口需 `Idempotency-Key` + `X-CSRF-TOKEN`（已有全局中间件）。

---

## 七、验收标准

- [ ] DictType CRUD 接口 200/400 响应正确
- [ ] DictData 按 code 查询接口返回启用数据
- [ ] SystemConfig 内置参数删除返回 403/400 业务错误
- [ ] 参数修改后审计日志中有对应记录
- [ ] 前端字典管理页分页、搜索正常
- [ ] 字典数据弹窗新增/编辑/删除/排序正常
- [ ] 参数管理页内置参数无删除按钮
