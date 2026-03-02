# 文件上传下载

> 文档版本：v1.0 | 等保2.0 覆盖：数据保密性、安全审计、访问控制

---

## 一、功能描述

提供基于租户隔离的文件存储服务，支持文件上传、下载和管理能力，广泛用于用户头像、通知附件、导入模板等场景。

### 核心功能

| 功能 | 说明 |
|------|------|
| 文件上传 | multipart/form-data，支持大小限制和类型白名单 |
| 文件下载 | 按 fileId 授权下载，返回原始文件流 |
| 文件信息查询 | 获取文件元数据（名称、大小、类型、上传时间） |
| 文件删除 | 按 fileId 删除，自动清理物理文件 |
| 租户隔离 | 每个租户独立存储目录，互不可见 |

### 等保2.0 合规要求

- 文件上传限制类型（拒绝可执行文件：`.exe`, `.sh`, `.bat` 等）
- 文件访问须鉴权，不可匿名下载
- 文件操作（上传、删除）记录操作审计
- 存储路径不在 Web 根目录，防止直接访问

---

## 二、产品架构清单（实现追踪）

### Phase 3 — 后端实现

| # | 层 | 文件 | 工作内容 | 状态 |
|---|---|------|---------|------|
| A1 | Domain | `Atlas.Domain/System/Entities/FileRecord.cs` | 文件记录实体 | ☐ |
| A2 | Application | `System/Models/FileModels.cs` | DTO 和请求模型 | ☐ |
| A3 | Application | `System/Abstractions/IFileStorageService.cs` | 存储服务接口 | ☐ |
| A4 | Application | `Options/FileStorageOptions.cs` | 文件存储配置 | ☐ |
| A5 | Infrastructure | `Services/LocalFileStorageService.cs` | 本地磁盘存储实现 | ☐ |
| A6 | Infrastructure | `Repositories/FileRecordRepository.cs` | 文件记录仓储 | ☐ |
| A7 | Infrastructure | `DatabaseInitializerHostedService.cs` | InitTables 添加 FileRecord | ☐ |
| A8 | Infrastructure | `CoreServiceRegistration.cs` | 注册服务 | ☐ |
| A9 | WebApi | `Controllers/FilesController.cs` | 上传/下载/删除端点 | ☐ |
| A10 | WebApi | `appsettings.json` | 添加 FileStorage 配置节 | ☐ |
| A11 | WebApi | `Program.cs` | 注册 FileStorageOptions | ☐ |

---

## 三、数据模型

### FileRecord 实体

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long | 主键（文件 ID） |
| TenantIdValue | string | 租户 ID |
| OriginalName | string(500) | 原始文件名 |
| StoredName | string(500) | 存储文件名（UUID + 扩展名） |
| ContentType | string(200) | MIME 类型 |
| SizeBytes | long | 文件大小（字节） |
| UploadedById | long | 上传者 UserId |
| UploadedByName | string(100) | 上传者姓名 |
| UploadedAt | DateTimeOffset | 上传时间 |
| IsDeleted | bool | 软删除标志 |

### FileStorageOptions（appsettings.json）

```json
{
  "FileStorage": {
    "BasePath": "uploads",
    "MaxFileSizeBytes": 10485760,
    "AllowedExtensions": [".jpg", ".jpeg", ".png", ".gif", ".pdf", ".xlsx", ".docx", ".txt", ".zip"],
    "BlockedExtensions": [".exe", ".sh", ".bat", ".cmd", ".ps1", ".vbs"]
  }
}
```

---

## 四、API 端点

| 方法 | 路径 | 权限 | 说明 |
|------|------|------|------|
| POST | `/api/files` | `file:upload` | 上传文件（multipart/form-data） |
| GET | `/api/files/{id}` | `file:download` | 下载文件（返回文件流） |
| GET | `/api/files/{id}/info` | 已登录 | 获取文件元数据 |
| DELETE | `/api/files/{id}` | `file:delete` | 删除文件（软删除） |

### POST `/api/files` 响应示例

```json
{
  "success": true,
  "data": {
    "id": "1234567890",
    "originalName": "report.pdf",
    "contentType": "application/pdf",
    "sizeBytes": 123456,
    "uploadedAt": "2026-03-01T10:00:00Z"
  }
}
```

---

## 五、验收标准

- [ ] 上传 `.jpg`、`.pdf`、`.xlsx` 文件返回 200 及文件 ID
- [ ] 上传 `.exe` 文件返回 400（类型不允许）
- [ ] 超过 `MaxFileSizeBytes` 的文件返回 400
- [ ] 未登录访问下载接口返回 401
- [ ] 文件下载响应包含 `Content-Disposition: attachment` 头
- [ ] 删除后再次下载返回 404
- [ ] 不同租户无法访问彼此的文件
- [ ] 上传和删除操作均有审计记录
