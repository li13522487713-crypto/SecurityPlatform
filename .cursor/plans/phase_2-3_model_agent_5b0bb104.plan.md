---
name: Phase 2-3 Model Agent
overview: Implement Phase 2 (Model Config management with persistent DB storage) and Phase 3 (Agent CRUD with conversation foundation), following the existing Atlas Clean Architecture patterns (TenantEntity, RepositoryBase, Command/Query split, FluentValidation, PermissionPolicies).
todos:
  - id: 2.1.1-entity
    content: Create ModelConfig entity in Atlas.Domain/AiPlatform/Entities/ModelConfig.cs extending TenantEntity
    status: pending
  - id: 2.1.2-repo
    content: Create ModelConfigRepository extending RepositoryBase<ModelConfig> with GetPagedAsync, GetAllEnabledAsync, ExistsByNameAsync
    status: pending
  - id: 2.1.3-dto
    content: Create ModelConfigModels.cs DTOs (ModelConfigDto, CreateRequest, UpdateRequest, TestRequest, TestResult) in Atlas.Application/AiPlatform/Models/
    status: pending
  - id: 2.1.4-validator
    content: Create ModelConfigValidators.cs (Create + Update validators) in Atlas.Application/AiPlatform/Validators/
    status: pending
  - id: 2.1.5-services
    content: Create IModelConfigCommandService + IModelConfigQueryService interfaces and their implementations (including TestConnectionAsync)
    status: pending
  - id: 2.1.6-controller
    content: Create ModelConfigsController with GET list/detail, POST create, PUT update, DELETE, POST test-connection endpoints
    status: pending
  - id: 2.1.7-permissions-di
    content: Add ModelConfig permissions to PermissionPolicies.cs, register repository + services in AiPlatformServiceRegistration.cs
    status: pending
  - id: 2.1.8-factory-wire
    content: Extend LlmProviderFactory to resolve providers from DB-stored ModelConfig records (primary) with appsettings fallback
    status: pending
  - id: 2.2-frontend
    content: Create api-model-config.ts, ModelConfigsPage.vue (table + create/edit modal + test connection), add route to pathComponentFallbackMap
    status: pending
  - id: 3.1.1-entity
    content: Create Agent entity and AgentKnowledgeLink entity in Atlas.Domain/AiPlatform/Entities/
    status: pending
  - id: 3.1.2-repo
    content: Create AgentRepository and AgentKnowledgeLinkRepository extending RepositoryBase
    status: pending
  - id: 3.1.3-dto
    content: Create AgentModels.cs DTOs (AgentListItem, AgentDetail, AgentCreateRequest, AgentUpdateRequest) in Atlas.Application/AiPlatform/Models/
    status: pending
  - id: 3.1.4-validator
    content: Create AgentValidators.cs (Create + Update validators) in Atlas.Application/AiPlatform/Validators/
    status: pending
  - id: 3.1.5-services
    content: Create IAgentCommandService + IAgentQueryService interfaces and implementations (Create, Update, Delete, Duplicate, Publish, GetPaged, GetById)
    status: pending
  - id: 3.1.6-controller
    content: Create AgentsController with 7 endpoints (list, detail, create, update, delete, duplicate, publish)
    status: pending
  - id: 3.1.7-permissions-di
    content: Add Agent permissions to PermissionPolicies.cs, register repositories + services in AiPlatformServiceRegistration.cs
    status: pending
  - id: 3.3-frontend
    content: Create api-agent.ts, AgentListPage.vue (card grid + create modal), AgentEditorPage.vue (IDE-style 3-panel editor), add routes to pathComponentFallbackMap
    status: pending
isProject: false
---

# Phase 2-3: Model Config + Agent Management Implementation

## Existing Patterns to Follow

All new code follows these established conventions from the codebase:

- **Entity**: extends `TenantEntity`, parameterless ORM ctor + business ctor with `(TenantId, ..., long id)`
- **Repository**: extends `RepositoryBase<T>`, injected as concrete class (not interface)
- **Service**: `I*CommandService` + `I*QueryService` in Application, implementations in Infrastructure
- **Controller**: `api/v1/{resource}`, CQRS, `ApiResponse<T>`, `ITenantProvider`, FluentValidation, `PermissionPolicies`
- **DTOs**: sealed records in `Models/` folder
- **Validators**: `AbstractValidator<T>` in `Validators/` folder
- **DI**: modular `*ServiceRegistration.cs` called from `[ServiceCollectionExtensions.cs](src/backend/Atlas.Infrastructure/ServiceCollectionExtensions.cs)`
- **Frontend**: `requestApi` in `services/api-*.ts`, pages in `pages/`, route via backend `GET /auth/routers` + `[pathComponentFallbackMap](src/frontend/Atlas.WebApp/src/utils/dynamic-router.ts)`

---

## Phase 2: Model Config Management

Currently LLM providers live only in `appsettings.json` via `[AiPlatformOptions](src/backend/Atlas.Infrastructure/Options/AiPlatformOptions.cs)`. Phase 2 adds DB-persisted model configs so admins can manage providers at runtime through the UI.

### 2.1 Backend

#### 2.1.1 Domain Entity

**File**: `src/backend/Atlas.Domain/AiPlatform/Entities/ModelConfig.cs`

```csharp
public sealed class ModelConfig : TenantEntity
{
    public ModelConfig() : base(TenantId.Empty) { /* ORM */ }

    public ModelConfig(TenantId tenantId, string name, string providerType,
        string apiKey, string baseUrl, string defaultModel, long id) : base(tenantId)
    {
        Id = id;
        Name = name;
        ProviderType = providerType;   // "openai" | "deepseek" | "ollama" | ...
        ApiKey = apiKey;
        BaseUrl = baseUrl;
        DefaultModel = defaultModel;
        IsEnabled = true;
        SupportsEmbedding = true;
        CreatedAt = DateTime.UtcNow;
    }

    public string Name { get; private set; }
    public string ProviderType { get; private set; }
    public string ApiKey { get; private set; }
    public string BaseUrl { get; private set; }
    public string DefaultModel { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool SupportsEmbedding { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(string name, string apiKey, string baseUrl,
        string defaultModel, bool isEnabled, bool supportsEmbedding) { ... }
}
```

Place in `Atlas.Domain` directly (no separate project needed for MVP -- same as `DictType` lives in `Atlas.Domain/System/`).

#### 2.1.2 Repository

**File**: `src/backend/Atlas.Infrastructure/Repositories/ModelConfigRepository.cs`

Extends `RepositoryBase<ModelConfig>` with:

- `GetPagedAsync(tenantId, keyword?, pageIndex, pageSize)` -- paged list with name/provider search
- `GetAllEnabledAsync(tenantId)` -- for provider factory resolution
- `FindByNameAsync(tenantId, name)` -- uniqueness check
- `ExistsByNameAsync(tenantId, name)` -- for create validation

#### 2.1.3 DTOs

**File**: `src/backend/Atlas.Application/AiPlatform/Models/ModelConfigModels.cs`

```csharp
public sealed record ModelConfigDto(long Id, string Name, string ProviderType,
    string BaseUrl, string DefaultModel, bool IsEnabled, bool SupportsEmbedding, DateTime CreatedAt);
// Note: ApiKey excluded from DTO for security; use masked version for display

public sealed record ModelConfigCreateRequest(string Name, string ProviderType,
    string ApiKey, string BaseUrl, string DefaultModel, bool SupportsEmbedding);

public sealed record ModelConfigUpdateRequest(string Name, string ApiKey,
    string BaseUrl, string DefaultModel, bool IsEnabled, bool SupportsEmbedding);

public sealed record ModelConfigTestRequest(string ProviderType,
    string ApiKey, string BaseUrl, string Model);

public sealed record ModelConfigTestResult(bool Success, string? ErrorMessage, int? LatencyMs);
```

#### 2.1.4 Validators

**File**: `src/backend/Atlas.Application/AiPlatform/Validators/ModelConfigValidators.cs`

- `ModelConfigCreateRequestValidator`: Name required (max 128), ProviderType in allowed set, BaseUrl valid URL format, DefaultModel not empty
- `ModelConfigUpdateRequestValidator`: same rules minus ProviderType (immutable)

#### 2.1.5 Services

**File**: `src/backend/Atlas.Application/AiPlatform/Abstractions/IModelConfigCommandService.cs`

```csharp
public interface IModelConfigCommandService
{
    Task<long> CreateAsync(TenantId tenantId, ModelConfigCreateRequest request, CancellationToken ct);
    Task UpdateAsync(TenantId tenantId, long id, ModelConfigUpdateRequest request, CancellationToken ct);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken ct);
    Task<ModelConfigTestResult> TestConnectionAsync(ModelConfigTestRequest request, CancellationToken ct);
}
```

**File**: `src/backend/Atlas.Application/AiPlatform/Abstractions/IModelConfigQueryService.cs`

```csharp
public interface IModelConfigQueryService
{
    Task<PagedResult<ModelConfigDto>> GetPagedAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken ct);
    Task<ModelConfigDto?> GetByIdAsync(TenantId tenantId, long id, CancellationToken ct);
    Task<IReadOnlyList<ModelConfigDto>> GetAllEnabledAsync(TenantId tenantId, CancellationToken ct);
}
```

**Implementation**: `src/backend/Atlas.Infrastructure/Services/AiPlatform/ModelConfigCommandService.cs` and `ModelConfigQueryService.cs`

- `TestConnectionAsync`: creates a temporary `OpenAiCompatibleProvider`, sends a minimal chat request (`[{"role":"user","content":"hi"}]`), measures latency, returns success/failure
- Manual entity-to-DTO mapping (no AutoMapper for this simple module, matching Dict pattern)

#### 2.1.6 Controller

**File**: `src/backend/Atlas.WebApi/Controllers/ModelConfigsController.cs`


| Method | Route                           | Permission          | Description                 |
| ------ | ------------------------------- | ------------------- | --------------------------- |
| GET    | `/api/v1/model-configs`         | `ModelConfigView`   | Paged list                  |
| GET    | `/api/v1/model-configs/enabled` | `ModelConfigView`   | All enabled (for dropdowns) |
| GET    | `/api/v1/model-configs/{id}`    | `ModelConfigView`   | Get by ID                   |
| POST   | `/api/v1/model-configs`         | `ModelConfigCreate` | Create                      |
| PUT    | `/api/v1/model-configs/{id}`    | `ModelConfigUpdate` | Update                      |
| DELETE | `/api/v1/model-configs/{id}`    | `ModelConfigDelete` | Delete                      |
| POST   | `/api/v1/model-configs/test`    | `ModelConfigCreate` | Test connection             |


#### 2.1.7 Permissions + DI

Add to `[PermissionPolicies.cs](src/backend/Atlas.WebApi/Authorization/PermissionPolicies.cs)`:

```csharp
public const string ModelConfigView = "Permission:model-config:view";
public const string ModelConfigCreate = "Permission:model-config:create";
public const string ModelConfigUpdate = "Permission:model-config:update";
public const string ModelConfigDelete = "Permission:model-config:delete";
```

Add to `[AiPlatformServiceRegistration.cs](src/backend/Atlas.Infrastructure/DependencyInjection/AiPlatformServiceRegistration.cs)`:

```csharp
services.AddScoped<ModelConfigRepository>();
services.AddScoped<IModelConfigCommandService, ModelConfigCommandService>();
services.AddScoped<IModelConfigQueryService, ModelConfigQueryService>();
```

#### 2.1.8 Wire DB-stored configs into LlmProviderFactory

After ModelConfig CRUD is in place, extend `LlmProviderFactory` to accept an `IModelConfigQueryService` so it can resolve providers from DB configs (falling back to `AiPlatformOptions` from appsettings). This makes DB-managed configs the primary source and appsettings the fallback.

### 2.2 Frontend

#### 2.2.1 API Service

**File**: `src/frontend/Atlas.WebApp/src/services/api-model-config.ts`

Standard `requestApi` calls for all 7 endpoints. TypeScript interfaces for `ModelConfigDto`, `ModelConfigCreateRequest`, etc.

#### 2.2.2 Model Config Page

**File**: `src/frontend/Atlas.WebApp/src/pages/ai/ModelConfigsPage.vue`

- Table with columns: Name, Provider, BaseUrl, Model, Enabled, Actions
- "New Model" button opens a modal
- Modal form: Name, Provider (select: openai/deepseek/ollama/custom), ApiKey (password input), BaseUrl, DefaultModel, SupportsEmbedding toggle
- "Test Connection" button in modal that calls `/model-configs/test` and shows result
- Edit/Delete actions per row

#### 2.2.3 Route Registration

Add to `[pathComponentFallbackMap](src/frontend/Atlas.WebApp/src/utils/dynamic-router.ts)`:

```ts
"/settings/ai/model-configs": "../pages/ai/ModelConfigsPage.vue",
```

Also seed a menu record in the backend DB so it appears in the sidebar under a new "AI Platform" parent menu.

---

## Phase 3: Agent Management

### 3.1 Backend - Core Agent CRUD

#### 3.1.1 Domain Entity

**File**: `src/backend/Atlas.Domain/AiPlatform/Entities/Agent.cs`

```csharp
public sealed class Agent : TenantEntity
{
    public Agent() : base(TenantId.Empty) { }

    public Agent(TenantId tenantId, string name, long id) : base(tenantId)
    {
        Id = id;
        Name = name;
        Status = AgentStatus.Draft;
        CreatedAt = DateTime.UtcNow;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string? SystemPrompt { get; private set; }
    public long? ModelConfigId { get; private set; }     // FK to ModelConfig
    public string? ModelName { get; private set; }       // override model within provider
    public float? Temperature { get; private set; }
    public int? MaxTokens { get; private set; }
    public AgentStatus Status { get; private set; }      // Draft, Published, Disabled
    public long CreatorId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public int PublishVersion { get; private set; }

    public void Update(string name, string? description, string? avatarUrl,
        string? systemPrompt, long? modelConfigId, string? modelName,
        float? temperature, int? maxTokens) { ... }

    public void Publish() { Status = AgentStatus.Published; PublishVersion++; PublishedAt = DateTime.UtcNow; }
    public void Disable() { Status = AgentStatus.Disabled; }
}

public enum AgentStatus { Draft = 0, Published = 1, Disabled = 2 }
```

#### 3.1.2 Agent-KnowledgeBase Link Entity (Many-to-Many)

**File**: `src/backend/Atlas.Domain/AiPlatform/Entities/AgentKnowledgeLink.cs`

```csharp
public sealed class AgentKnowledgeLink : TenantEntity
{
    public long AgentId { get; private set; }
    public long KnowledgeBaseId { get; private set; }
}
```

This enables the "Knowledge Binding" feature (3.3.6) where agents can be linked to multiple knowledge bases for RAG.

#### 3.1.3 Repository

**File**: `src/backend/Atlas.Infrastructure/Repositories/AgentRepository.cs`

Extends `RepositoryBase<Agent>` with:

- `GetPagedAsync(tenantId, keyword?, status?, pageIndex, pageSize)` -- paged list with name search and status filter
- `ExistsByNameAsync(tenantId, name)` -- uniqueness check
- `DuplicateAsync(tenantId, sourceId, newId)` -- copy entity with new ID and "Copy of" name prefix

#### 3.1.4 DTOs

**File**: `src/backend/Atlas.Application/AiPlatform/Models/AgentModels.cs`

```csharp
public sealed record AgentListItem(long Id, string Name, string? Description,
    string? AvatarUrl, string Status, string? ModelName, DateTime CreatedAt, int PublishVersion);

public sealed record AgentDetail(long Id, string Name, string? Description,
    string? AvatarUrl, string? SystemPrompt, long? ModelConfigId, string? ModelName,
    float? Temperature, int? MaxTokens, string Status, long CreatorId,
    DateTime CreatedAt, DateTime? UpdatedAt, DateTime? PublishedAt, int PublishVersion,
    IReadOnlyList<long>? KnowledgeBaseIds);

public sealed record AgentCreateRequest(string Name, string? Description,
    string? SystemPrompt, long? ModelConfigId, string? ModelName,
    float? Temperature, int? MaxTokens);

public sealed record AgentUpdateRequest(string Name, string? Description,
    string? AvatarUrl, string? SystemPrompt, long? ModelConfigId, string? ModelName,
    float? Temperature, int? MaxTokens, IReadOnlyList<long>? KnowledgeBaseIds);
```

#### 3.1.5 Validators

**File**: `src/backend/Atlas.Application/AiPlatform/Validators/AgentValidators.cs`

- Name: required, max 128
- SystemPrompt: max 32000
- Temperature: 0.0-2.0
- MaxTokens: 1-128000

#### 3.1.6 Services

**File**: `src/backend/Atlas.Application/AiPlatform/Abstractions/IAgentCommandService.cs`

```csharp
public interface IAgentCommandService
{
    Task<long> CreateAsync(TenantId tenantId, long creatorId, AgentCreateRequest request, CancellationToken ct);
    Task UpdateAsync(TenantId tenantId, long id, AgentUpdateRequest request, CancellationToken ct);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken ct);
    Task<long> DuplicateAsync(TenantId tenantId, long id, CancellationToken ct);
    Task PublishAsync(TenantId tenantId, long id, CancellationToken ct);
}
```

**File**: `src/backend/Atlas.Application/AiPlatform/Abstractions/IAgentQueryService.cs`

```csharp
public interface IAgentQueryService
{
    Task<PagedResult<AgentListItem>> GetPagedAsync(TenantId tenantId, string? keyword, string? status, int pageIndex, int pageSize, CancellationToken ct);
    Task<AgentDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken ct);
}
```

**Implementations**: `src/backend/Atlas.Infrastructure/Services/AiPlatform/AgentCommandService.cs` and `AgentQueryService.cs`

- Create: generates ID via `IIdGeneratorAccessor`, auto-assigns default ModelConfig if none provided
- Update: updates entity + syncs `AgentKnowledgeLink` records
- Duplicate: copies entity and links, prefixes name with "Copy of"
- Publish: increments version, sets `PublishedAt`

#### 3.1.7 Controller

**File**: `src/backend/Atlas.WebApi/Controllers/AgentsController.cs`


| Method | Route                           | Permission    | Description |
| ------ | ------------------------------- | ------------- | ----------- |
| GET    | `/api/v1/agents`                | `AgentView`   | Paged list  |
| GET    | `/api/v1/agents/{id}`           | `AgentView`   | Get detail  |
| POST   | `/api/v1/agents`                | `AgentCreate` | Create      |
| PUT    | `/api/v1/agents/{id}`           | `AgentUpdate` | Update      |
| DELETE | `/api/v1/agents/{id}`           | `AgentDelete` | Delete      |
| POST   | `/api/v1/agents/{id}/duplicate` | `AgentCreate` | Duplicate   |
| POST   | `/api/v1/agents/{id}/publish`   | `AgentUpdate` | Publish     |


#### 3.1.8 Permissions + DI

Add to `[PermissionPolicies.cs](src/backend/Atlas.WebApi/Authorization/PermissionPolicies.cs)`:

```csharp
public const string AgentView = "Permission:agent:view";
public const string AgentCreate = "Permission:agent:create";
public const string AgentUpdate = "Permission:agent:update";
public const string AgentDelete = "Permission:agent:delete";
```

Add to `AiPlatformServiceRegistration.cs`:

```csharp
services.AddScoped<AgentRepository>();
services.AddScoped<AgentKnowledgeLinkRepository>();
services.AddScoped<IAgentCommandService, AgentCommandService>();
services.AddScoped<IAgentQueryService, AgentQueryService>();
```

### 3.2 Agent Publish (simplified MVP)

For MVP, "publish" means flipping status to Published and incrementing version. Full publish history (3.2.2) and connectors (3.2.3) are deferred.

### 3.3 Frontend

#### 3.3.1 API Service

**File**: `src/frontend/Atlas.WebApp/src/services/api-agent.ts`

TypeScript interfaces + `requestApi` calls for all 7 Agent endpoints.

#### 3.3.2 Agent List Page

**File**: `src/frontend/Atlas.WebApp/src/pages/ai/AgentListPage.vue`

- Card/Grid layout showing agent cards (avatar, name, description, status badge)
- "New Agent" button opens a create modal (Name + Description + optional ModelConfig select)
- Each card has: Edit, Duplicate, Delete actions
- Search bar + status filter tabs (All / Draft / Published)

#### 3.3.3 Agent Editor Page

**File**: `src/frontend/Atlas.WebApp/src/pages/ai/AgentEditorPage.vue`

IDE-style layout with 3 panels:

- **Left panel**: Agent settings form (Name, Description, Avatar upload, System Prompt textarea, Model Config select, Temperature slider, MaxTokens input, Knowledge Base multi-select)
- **Center panel**: System Prompt editor (large textarea with character count)
- **Right panel**: Chat preview (sends test messages using the agent's config, calls existing `/api/v1/ai/chat/stream` endpoint)

#### 3.3.4 Route Registration

Add to `[pathComponentFallbackMap](src/frontend/Atlas.WebApp/src/utils/dynamic-router.ts)`:

```ts
"/ai/agents": "../pages/ai/AgentListPage.vue",
"/ai/agents/:id/edit": "../pages/ai/AgentEditorPage.vue",
```

---

## DB Schema (SqlSugar auto-creates tables)

SqlSugar with `IsAutoCloseConnection = true` handles table creation during initialization. The entities define the schema:

**ModelConfig table**:

- Id (long PK), TenantIdValue (Guid), Name, ProviderType, ApiKey, BaseUrl, DefaultModel, IsEnabled, SupportsEmbedding, CreatedAt, UpdatedAt

**Agent table**:

- Id (long PK), TenantIdValue (Guid), Name, Description, AvatarUrl, SystemPrompt, ModelConfigId (nullable FK), ModelName, Temperature, MaxTokens, Status, CreatorId, CreatedAt, UpdatedAt, PublishedAt, PublishVersion

**AgentKnowledgeLink table**:

- Id (long PK), TenantIdValue (Guid), AgentId, KnowledgeBaseId

---

## File Tree Summary

```
src/backend/
  Atlas.Domain/AiPlatform/Entities/
    ModelConfig.cs                    (2.1.1)
    Agent.cs                          (3.1.1)
    AgentKnowledgeLink.cs             (3.1.2)
  Atlas.Application/AiPlatform/
    Abstractions/
      IModelConfigCommandService.cs   (2.1.5)
      IModelConfigQueryService.cs     (2.1.5)
      IAgentCommandService.cs         (3.1.6)
      IAgentQueryService.cs           (3.1.6)
    Models/
      ModelConfigModels.cs            (2.1.3)
      AgentModels.cs                  (3.1.4)
    Validators/
      ModelConfigValidators.cs        (2.1.4)
      AgentValidators.cs              (3.1.5)
  Atlas.Infrastructure/
    Repositories/
      ModelConfigRepository.cs        (2.1.2)
      AgentRepository.cs              (3.1.3)
      AgentKnowledgeLinkRepository.cs (3.1.3)
    Services/AiPlatform/
      ModelConfigCommandService.cs    (2.1.5)
      ModelConfigQueryService.cs      (2.1.5)
      AgentCommandService.cs          (3.1.6)
      AgentQueryService.cs            (3.1.6)
  Atlas.WebApi/Controllers/
    ModelConfigsController.cs         (2.1.6)
    AgentsController.cs               (3.1.7)

src/frontend/Atlas.WebApp/src/
  services/
    api-model-config.ts               (2.2.1)
    api-agent.ts                      (3.3.1)
  pages/ai/
    ModelConfigsPage.vue              (2.2.2)
    AgentListPage.vue                 (3.3.2)
    AgentEditorPage.vue               (3.3.3)
```

