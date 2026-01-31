# 动态表与低代码模块目录结构

本结构用于实现“动态建表 + 动态 CRUD + AMIS 低代码页面”，遵循 Clean Architecture 与现有仓库约束。

## 后端（.NET）

- `src/backend/Atlas.Domain.DynamicTables`
  - `Entities/`：DynamicTable、DynamicField、DynamicIndex、DynamicRecord
  - `ValueObjects/`：FieldType、FieldLength、FieldConstraint
  - `Specifications/`：命名规范、字段规则、主键约束
- `src/backend/Atlas.Application.DynamicTables`
  - `Dtos/`：TableCreateRequest、FieldDefinition、RecordUpsertDto
  - `Commands/`：CreateTable、AlterTable、CreateRecord、UpdateRecord、DeleteRecord
  - `Queries/`：GetTable、ListTables、QueryRecords
  - `Validators/`：FluentValidation 校验器
  - `Mappers/`：AutoMapper 配置
- `src/backend/Atlas.Infrastructure.DynamicTables`
  - `Repositories/`：DynamicTableRepository、DynamicRecordRepository
  - `Sql/`：DDL/DML 生成器、数据库方言适配
  - `Metadata/`：表结构与字段元数据存取
- `src/backend/Atlas.WebApi`
  - `Controllers/DynamicTables/`：表结构与记录 CRUD
  - `Controllers/Amis/`：动态 AMIS Schema 输出
  - `AmisSchemas/dynamic/`：可选的 JSON 模板缓存

## 前端（Vue 3 + TypeScript）

- `src/frontend/Atlas.WebApp/src/modules/dynamic-tables`
  - `pages/`
    - `TableListPage.vue`：动态表列表
    - `TableDesignerPage.vue`：表结构设计器
    - `TableCrudPage.vue`：动态 CRUD
  - `services/`
    - `dynamicTablesApi.ts`：表结构与记录 API
  - `types/`
    - `dynamicTables.ts`：强类型 DTO
  - `schemas/`：可选的 AMIS JSON 本地模板

## 文档与模板

- `docs/contracts.md`：接口契约与 DTO 规范
- `docs/amis-templates/`：AMIS JSON 模板（落地示例）
