# 表单数据持久化层实现指南

## 概述

本文档描述低代码平台表单数据的持久化架构设计和实现计划。

## 当前状态

**已完成**：
- ✅ FormDefinition 实体（表单定义）
- ✅ FormDefinitionCommandService（表单定义CRUD）
- ✅ FormDefinitionsController（表单定义API）
- ✅ FormDesignerPage.vue（前端表单设计器）

**待实现（TODO）**：
- ⏳ FormData 数据持久化层
- ⏳ 动态表单数据CRUD API
- ⏳ 与DynamicTable服务集成

## 架构设计

### 1. 数据模型

#### FormData（表单数据）
```csharp
// 选项A：使用JSON字段存储动态数据（简单但查询性能差）
public class FormDataRecord : TenantEntity
{
    public long FormDefinitionId { get; set; }
    public string DataJson { get; set; }  // JSON格式存储表单数据
    public long CreatedBy { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
}

// 选项B：使用DynamicTable（复杂但查询性能好）
// 根据FormDefinition的SchemaJson动态创建数据表
// 例如：FormDefinition.DataTableKey = "employee_info"
// 则创建表：employee_info (id, tenant_id, name, email, phone, ...)
```

**推荐方案**：选项B - 使用DynamicTable

#### 与DynamicTable集成
```csharp
// FormDefinition 已有字段：
public string? DataTableKey { get; set; }  // 绑定的动态表键名

// 当FormDefinition发布时：
// 1. 解析SchemaJson，提取字段定义
// 2. 调用DynamicTableService.CreateTableAsync()
// 3. 创建对应的动态表
```

### 2. 服务层设计

#### IFormDataCommandService
```csharp
public interface IFormDataCommandService
{
    /// <summary>
    /// 提交表单数据
    /// </summary>
    Task<long> CreateAsync(long formDefinitionId, TenantId tenantId,
        Dictionary<string, object> data, CancellationToken cancellationToken);

    /// <summary>
    /// 更新表单数据
    /// </summary>
    Task UpdateAsync(long formDefinitionId, long recordId, TenantId tenantId,
        Dictionary<string, object> data, CancellationToken cancellationToken);

    /// <summary>
    /// 删除表单数据
    /// </summary>
    Task DeleteAsync(long formDefinitionId, long recordId, TenantId tenantId,
        CancellationToken cancellationToken);
}
```

#### IFormDataQueryService
```csharp
public interface IFormDataQueryService
{
    /// <summary>
    /// 分页查询表单数据
    /// </summary>
    Task<PagedResult<Dictionary<string, object>>> GetPagedAsync(
        long formDefinitionId, TenantId tenantId,
        PagedRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 获取单条表单数据
    /// </summary>
    Task<Dictionary<string, object>?> GetByIdAsync(
        long formDefinitionId, long recordId, TenantId tenantId,
        CancellationToken cancellationToken);
}
```

### 3. 实现步骤

#### Phase 1: 基础架构（优先级：P0）
```
文件位置：
- src/backend/Atlas.Application/LowCode/Abstractions/IFormDataCommandService.cs
- src/backend/Atlas.Application/LowCode/Abstractions/IFormDataQueryService.cs
- src/backend/Atlas.Infrastructure/Services/LowCode/FormDataCommandService.cs
- src/backend/Atlas.Infrastructure/Services/LowCode/FormDataQueryService.cs
- src/backend/Atlas.WebApi/Controllers/FormDataController.cs

实现内容：
1. 定义服务接口
2. 实现基于DynamicTable的数据存储
3. 实现Schema验证逻辑
4. 创建REST API端点
```

#### Phase 2: 数据验证（优先级：P1）
```
实现内容：
1. 根据FormDefinition.SchemaJson进行字段验证
2. 必填字段检查
3. 数据类型验证（字符串、数字、日期等）
4. 自定义验证规则支持
```

#### Phase 3: 高级功能（优先级：P2）
```
实现内容：
1. 表单数据导入/导出（Excel）
2. 表单数据批量操作
3. 表单数据审计日志
4. 表单数据统计和聚合查询
```

## API端点设计

### FormDataController

```csharp
[ApiController]
[Route("api/v1/forms/{formId}/data")]
[Authorize]
public class FormDataController : ControllerBase
{
    /// <summary>
    /// POST /api/v1/forms/{formId}/data
    /// 提交表单数据
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<long>>> Create(
        [FromRoute] long formId,
        [FromBody] Dictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        // 实现逻辑
    }

    /// <summary>
    /// GET /api/v1/forms/{formId}/data
    /// 分页查询表单数据
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<Dictionary<string, object>>>>> GetPaged(
        [FromRoute] long formId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken)
    {
        // 实现逻辑
    }

    /// <summary>
    /// GET /api/v1/forms/{formId}/data/{id}
    /// 获取单条表单数据
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object>>>> GetById(
        [FromRoute] long formId,
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        // 实现逻辑
    }

    /// <summary>
    /// PUT /api/v1/forms/{formId}/data/{id}
    /// 更新表单数据
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        [FromRoute] long formId,
        [FromRoute] long id,
        [FromBody] Dictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        // 实现逻辑
    }

    /// <summary>
    /// DELETE /api/v1/forms/{formId}/data/{id}
    /// 删除表单数据
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        [FromRoute] long formId,
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        // 实现逻辑
    }
}
```

## 与DynamicTable服务集成

### 创建动态表流程

```csharp
// 当FormDefinition发布时触发
public async Task PublishFormDefinitionAsync(long formId, TenantId tenantId)
{
    // 1. 获取表单定义
    var form = await _formRepository.FindByIdAsync(tenantId, formId);

    // 2. 如果有DataTableKey，检查是否需要创建动态表
    if (!string.IsNullOrEmpty(form.DataTableKey))
    {
        var tableExists = await _dynamicTableService.TableExistsAsync(
            tenantId, form.DataTableKey);

        if (!tableExists)
        {
            // 3. 解析SchemaJson，提取字段定义
            var fields = ExtractFieldsFromSchema(form.SchemaJson);

            // 4. 创建动态表
            var tableDefinition = new DynamicTableDefinition
            {
                TableKey = form.DataTableKey,
                TableName = form.Name,
                Fields = fields
            };

            await _dynamicTableService.CreateTableAsync(tenantId, tableDefinition);
        }
    }

    // 5. 更新表单状态为已发布
    form.Publish();
    await _formRepository.UpdateAsync(form);
}

private List<FieldDefinition> ExtractFieldsFromSchema(JsonElement schemaJson)
{
    var fields = new List<FieldDefinition>();

    // 遍历SchemaJson中的form.body字段
    // 提取每个input的name、type、required等属性
    // 转换为FieldDefinition

    // 示例：
    // {
    //   "type": "input-text",
    //   "name": "employeeName",
    //   "label": "员工姓名",
    //   "required": true
    // }
    // →
    // FieldDefinition {
    //   Name = "employeeName",
    //   Type = "string",
    //   IsRequired = true
    // }

    return fields;
}
```

## 数据验证逻辑

```csharp
public async Task ValidateFormDataAsync(
    long formDefinitionId,
    TenantId tenantId,
    Dictionary<string, object> data)
{
    // 1. 获取表单定义
    var form = await _formRepository.FindByIdAsync(tenantId, formDefinitionId);
    if (form == null)
    {
        throw new BusinessException("表单定义不存在", ErrorCodes.NotFound);
    }

    // 2. 解析SchemaJson
    var schema = JsonSerializer.Deserialize<FormSchema>(form.SchemaJson);
    var formFields = ExtractFields(schema);

    // 3. 验证必填字段
    foreach (var field in formFields.Where(f => f.Required))
    {
        if (!data.ContainsKey(field.Name) || data[field.Name] == null)
        {
            throw new BusinessException(
                $"字段 '{field.Label}' 是必填项",
                ErrorCodes.ValidationError);
        }
    }

    // 4. 验证数据类型
    foreach (var kvp in data)
    {
        var field = formFields.FirstOrDefault(f => f.Name == kvp.Key);
        if (field != null)
        {
            ValidateFieldType(field, kvp.Value);
        }
    }
}

private void ValidateFieldType(FormField field, object value)
{
    switch (field.Type)
    {
        case "input-text":
        case "textarea":
            if (value is not string)
                throw new BusinessException(
                    $"字段 '{field.Name}' 必须是字符串类型",
                    ErrorCodes.ValidationError);
            break;

        case "input-number":
            if (value is not int and not long and not decimal and not double)
                throw new BusinessException(
                    $"字段 '{field.Name}' 必须是数字类型",
                    ErrorCodes.ValidationError);
            break;

        case "input-email":
            if (value is not string str || !IsValidEmail(str))
                throw new BusinessException(
                    $"字段 '{field.Name}' 必须是有效的邮箱地址",
                    ErrorCodes.ValidationError);
            break;

        // ... 其他类型验证
    }
}
```

## 测试文件

### 创建测试文件
```
src/backend/Atlas.WebApi/Bosch.http/FormData.http
```

### 示例测试场景
```http
### 1. 提交表单数据
POST {{baseUrl}}/forms/1/data
{
  "employeeName": "张三",
  "email": "zhangsan@example.com",
  "phone": "13800138000",
  "department": "tech"
}

### 2. 查询表单数据列表
GET {{baseUrl}}/forms/1/data?pageIndex=1&pageSize=10

### 3. 获取单条表单数据
GET {{baseUrl}}/forms/1/data/1

### 4. 更新表单数据
PUT {{baseUrl}}/forms/1/data/1
{
  "employeeName": "张三",
  "email": "zhangsan_new@example.com",
  "phone": "13900139000",
  "department": "hr"
}

### 5. 删除表单数据
DELETE {{baseUrl}}/forms/1/data/1
```

## 实施优先级

| 优先级 | 功能 | 时间估算 | 依赖 |
|-------|------|---------|------|
| P0 | FormDataController API端点 | 2天 | 无 |
| P0 | FormDataCommandService基础实现 | 3天 | DynamicTable服务 |
| P0 | FormDataQueryService基础实现 | 2天 | DynamicTable服务 |
| P1 | Schema字段提取和验证 | 2天 | P0完成 |
| P1 | 表单发布时自动创建动态表 | 1天 | P0完成 |
| P2 | 数据导入/导出功能 | 3天 | P0+P1完成 |
| P2 | 批量操作和统计 | 2天 | P0+P1完成 |

**总计**: 约15天（3周）

## 相关文件引用

- 现有动态表服务：`src/backend/Atlas.Infrastructure/Services/DynamicTableService.cs`
- 表单定义实体：`src/backend/Atlas.Domain/LowCode/Entities/FormDefinition.cs`
- 表单设计器：`src/frontend/Atlas.WebApp/src/pages/lowcode/FormDesignerPage.vue`

## TODO清单

- [ ] 创建 FormDataController
- [ ] 实现 IFormDataCommandService
- [ ] 实现 IFormDataQueryService
- [ ] 集成 DynamicTable 服务
- [ ] 实现 Schema 字段解析
- [ ] 实现数据验证逻辑
- [ ] 创建测试文件 FormData.http
- [ ] 前端表单数据提交/查询功能
- [ ] 编写单元测试
- [ ] 更新文档

## 参考资料

- AMIS Form Schema: https://aisuda.bce.baidu.com/amis/zh-CN/components/form/index
- JSON Schema Validation: https://json-schema.org/
- Dynamic Table Design Patterns: https://martinfowler.com/bliki/DynamicTable.html
