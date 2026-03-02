# OpenAPI 类型自动生成使用指南

## 概述

本项目使用 **NSwag** 从后端 API 自动生成 TypeScript 类型定义和客户端代码,确保前后端类型一致性。

## 配置文件

### 1. 后端 OpenAPI 配置 (Program.cs)

```csharp
// 配置 NSwag OpenAPI 文档生成
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "Atlas Security Platform API";
    config.Version = "v1";
    config.Description = "Atlas 安全平台 API 文档(符合等保2.0标准)";
    config.UseControllerSummaryAsTagDescription = true;
});

// 启用 Swagger UI (开发环境)
if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();       // 提供 /swagger/v1/swagger.json
    app.UseSwaggerUi();     // 提供 /swagger 交互式文档
}
```

### 2. NSwag 配置文件 (nswag.json)

位置: `src/backend/Atlas.WebApi/nswag.json`

关键配置:
- **runtime**: Net90 (匹配 NSwag CLI 版本)
- **url**: http://localhost:5000/swagger/v1/swagger.json
- **output**: ../../frontend/Atlas.WebApp/src/types/api-generated.ts
- **template**: Fetch (使用浏览器原生 Fetch API)
- **typeScriptVersion**: 5.0
- **generateClientClasses**: true (生成 API 客户端类)
- **generateDtoTypes**: true (生成 DTO 类型接口)

## 使用方法

### 1. 安装 NSwag CLI (首次运行)

```bash
dotnet tool install -g NSwag.ConsoleCore --version 14.2.0
```

### 2. 启动后端服务器

```bash
cd src/backend/Atlas.WebApi
dotnet run
```

服务器启动后,OpenAPI 文档可访问:
- **Swagger UI**: http://localhost:5000/swagger
- **OpenAPI JSON**: http://localhost:5000/swagger/v1/swagger.json

### 3. 生成 TypeScript 类型

**方法 A - 使用 npm 脚本 (推荐)**:

```bash
cd src/frontend/Atlas.WebApp
npm run generate-types
```

**方法 B - 直接运行 NSwag**:

```bash
cd src/backend/Atlas.WebApi
nswag run nswag.json
```

### 4. 生成文件位置

生成的文件: `src/frontend/Atlas.WebApp/src/types/api-generated.ts`
- 包含所有 API 客户端类 (如 `AiAssistantClient`, `AuthClient`, `UsersClient` 等)
- 包含所有 DTO 接口 (如 `ApiResponse`, `PagedResult`, `AssetResponse` 等)
- 约 10000+ 行,自动同步后端 API 定义

## 在前端代码中使用

### 示例 1: 使用生成的客户端类

```typescript
import { AiAssistantClient, AiFormGenerateRequest } from '@/types/api-generated';

// 创建客户端实例
const aiClient = new AiAssistantClient('http://localhost:5000');

// 调用 API
const request: AiFormGenerateRequest = {
  description: '创建用户注册表单',
  requirements: ['包含用户名', '邮箱', '密码']
};

const response = await aiClient.generateForm(request);
if (response.success) {
  console.log(response.data?.formSchema);
}
```

### 示例 2: 仅使用类型定义

```typescript
import type {
  ApiResponse,
  PagedResult,
  AssetResponse,
  AssetCreateRequest
} from '@/types/api-generated';

// 在现有 API 服务中使用类型
async function getAssets(pageIndex: number): Promise<ApiResponse<PagedResult<AssetResponse>>> {
  const response = await fetch(`/api/v1/assets?pageIndex=${pageIndex}`);
  return response.json();
}

async function createAsset(data: AssetCreateRequest): Promise<ApiResponse<AssetResponse>> {
  const response = await fetch('/api/v1/assets', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  return response.json();
}
```

### 示例 3: 与现有 API 服务集成

```typescript
// src/services/api.ts
import type { ApiResponse, AssetResponse } from '@/types/api-generated';
import { apiClient } from './api-core';

export const assetApi = {
  async getAssets(pageIndex: number, pageSize: number): Promise<ApiResponse<PagedResult<AssetResponse>>> {
    return apiClient.get('/api/v1/assets', {
      params: { pageIndex, pageSize }
    });
  }
};
```

## 开发工作流

### 1. 修改后端 API 后的同步流程

```bash
# 1. 修改 Controller 或 DTO
# 2. 确保后端编译成功
dotnet build

# 3. 启动后端服务器
dotnet run

# 4. 在新终端生成 TypeScript 类型
cd src/frontend/Atlas.WebApp
npm run generate-types

# 5. 前端代码自动获得最新类型提示
```

### 2. CI/CD 集成

在 CI 流程中添加类型生成验证:

```yaml
# .github/workflows/ci.yml
- name: 启动后端服务器
  run: |
    cd src/backend/Atlas.WebApi
    dotnet run &
    sleep 10

- name: 生成 TypeScript 类型
  run: |
    cd src/frontend/Atlas.WebApp
    npm run generate-types

- name: 验证类型文件已更新
  run: |
    git diff --exit-code src/frontend/Atlas.WebApp/src/types/api-generated.ts
```

## 优势

### 1. 类型安全
- **IDE 智能提示**: 完整的类型提示和自动补全
- **编译时检查**: TypeScript 编译器捕获类型错误
- **重构安全**: 后端 API 修改后,前端立即感知

### 2. 减少手动维护
- **自动同步**: 后端修改后一键生成新类型
- **零手工编写**: 无需手动维护接口定义
- **一致性保证**: 前后端类型定义来源唯一

### 3. 开发效率
- **快速开发**: 直接使用生成的客户端类
- **减少错误**: 避免手动拼写 URL 和字段名错误
- **文档即代码**: OpenAPI 文档即为前端类型定义

## 常见问题

### Q1: 生成的文件太大怎么办?

A: api-generated.ts 包含所有 API 的类型,但 TypeScript 编译器会执行 tree-shaking,只打包实际使用的部分。

### Q2: 生成失败怎么办?

A: 检查:
1. 后端服务器是否启动 (http://localhost:5000/swagger/v1/swagger.json 可访问)
2. NSwag CLI 版本是否匹配 nswag.json 中的 runtime
3. 查看错误日志定位具体问题

### Q3: 如何自定义生成配置?

A: 编辑 `src/backend/Atlas.WebApi/nswag.json`,参考 [NSwag 文档](https://github.com/RicoSuter/NSwag/wiki/NSwag-Configuration-Document)

### Q4: 能否只生成类型不生成客户端?

A: 可以,修改 nswag.json:
```json
{
  "generateClientClasses": false,
  "generateDtoTypes": true
}
```

## 相关文件

- **后端配置**: `src/backend/Atlas.WebApi/Program.cs`
- **NSwag 配置**: `src/backend/Atlas.WebApi/nswag.json`
- **生成文件**: `src/frontend/Atlas.WebApp/src/types/api-generated.ts`
- **npm 脚本**: `src/frontend/Atlas.WebApp/package.json`

## 更新历史

- **2026-02-12**: 初始配置,集成 NSwag 14.2.0,生成 10894 行类型定义
