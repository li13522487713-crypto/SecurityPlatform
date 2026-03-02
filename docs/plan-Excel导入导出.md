# Excel 导入导出

> 文档版本：v1.0 | 等保2.0 覆盖：数据完整性、输入验证、访问控制、安全审计

---

## 一、功能描述

为用户管理模块提供 Excel 批量导入和数据导出能力，提升运维效率。后续可扩展至其他模块。

### 核心功能

| 功能 | 说明 |
|------|------|
| 用户列表导出 | 将当前查询结果（含筛选条件）导出为 `.xlsx` 文件 |
| 用户批量导入 | 上传 `.xlsx` 模板，解析后批量创建用户账号 |
| 导入模板下载 | 提供填写规范的导入模板文件 |
| 导入结果反馈 | 返回成功行数、失败行数及每行错误详情 |

### 等保2.0 合规要求

- 导入文件须经扩展名和内容类型双重校验（仅允许 `.xlsx`）
- 导出操作须有权限控制，记录操作审计
- 导入数据经 FluentValidation 逐行校验，拒绝非法格式
- 导入结果不在响应中暴露敏感数据（密码仅临时生成，日志不存储）

---

## 二、产品架构清单（实现追踪）

### Phase 3 — 后端实现

| # | 层 | 文件 | 工作内容 | 状态 |
|---|---|------|---------|------|
| A1 | Application | `Identity/Models/UserImportModels.cs` | 导入行 DTO、结果 DTO | ☐ |
| A2 | Application | `Identity/Abstractions/IExcelExportService.cs` | Excel 导出接口 | ☐ |
| A3 | Infrastructure | `Services/ClosedXmlExcelExportService.cs` | ClosedXML 实现 | ☐ |
| A4 | Infrastructure | `CoreServiceRegistration.cs` | 注册导出服务 | ☐ |
| A5 | WebApi | `Controllers/UsersController.cs` | 新增 `/export`、`/import`、`/import-template` 端点 | ☐ |
| A6 | WebApi | `Atlas.WebApi.csproj` | 添加 `ClosedXML` NuGet 包 | ☐ |

### Phase 3 — 前端实现

| # | 层 | 文件 | 工作内容 | 状态 |
|---|---|------|---------|------|
| B1 | Composable | `composables/useExcelExport.ts` | 封装导出/导入 API 调用 + 下载触发 | ☐ |
| B2 | Page | `pages/system/UsersPage.vue` | 添加"导出"按钮和"导入"Modal | ☐ |

---

## 三、API 端点

| 方法 | 路径 | 权限 | 说明 |
|------|------|------|------|
| GET | `/api/users/export` | `users:view` | 导出当前筛选用户为 `.xlsx` |
| GET | `/api/users/import-template` | `users:create` | 下载导入模板 |
| POST | `/api/users/import` | `users:create` | 批量导入用户 |

### 导入模板字段（列头）

| 列 | 必填 | 规则 |
|----|------|------|
| 用户名 | 是 | 3-50 位字母/数字/下划线 |
| 显示名称 | 是 | 最长 100 字符 |
| 邮箱 | 否 | 合法邮箱格式 |
| 手机号 | 否 | 11 位数字 |

### 导入响应示例

```json
{
  "success": true,
  "data": {
    "totalRows": 50,
    "successCount": 48,
    "failureCount": 2,
    "errors": [
      { "row": 5, "field": "username", "message": "用户名已存在" },
      { "row": 12, "field": "email", "message": "邮箱格式不正确" }
    ]
  }
}
```

---

## 四、验收标准

### 后端

- [ ] GET `/api/users/export` 返回 `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- [ ] 导出文件包含用户表所有可见字段（不含密码哈希）
- [ ] POST `/api/users/import` 上传合规模板，返回正确的成功/失败计数
- [ ] 上传非 `.xlsx` 文件返回 400
- [ ] 导入结果记录审计日志（每次导入一条）

### 前端

- [ ] 点击"导出"按钮触发文件下载
- [ ] 点击"导入"按钮弹出 Modal，支持 Drag & Drop 文件选择
- [ ] 上传后显示导入结果（成功/失败数量及错误详情列表）
- [ ] "下载模板"链接可下载空白导入模板
