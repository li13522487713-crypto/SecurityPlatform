# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Atlas Security Platform** - A comprehensive security support platform compliant with Chinese 等保2.0 (GB/T 22239-2019) standards. The system consists of an infrastructure support component and a security support component built with Clean Architecture principles, multi-tenant isolation, and strict security controls.

**Language Requirement:** All assistant responses must be in Chinese (per AGENTS.md).

## Technology Stack

### Backend
- **.NET 10.0** with ASP.NET Core
- **SqlSugar ORM 5.1.4.169** with SQLite database
- **Authentication:** JWT Bearer + Client Certificate
- **Authorization:** Role-based access control (RBAC)
- **Validation:** FluentValidation 11.4.0
- **Mapping:** AutoMapper 12.0.1
- **ID Generation:** Snowflake (IdGen 3.0.1)
- **Logging:** NLog 5.3.4 with file rotation

### Frontend
- **React 18** + **TypeScript** (strict mode)
- **Rsbuild** build/dev toolchain
- **Semi Design (`@douyinfe/semi-ui ^2.82.0`)** — **强制且唯一的前端 UI 框架**，禁止引入任何其他组件库
- workspace packages modular architecture
- **React Router 6** for routing

## Build and Development Commands

### Backend (.NET)
```bash
# Build the solution (must have 0 errors, 0 warnings)
dotnet build

# Run AppHost (application runtime host, starts on http://localhost:5002)
dotnet run --project src/backend/Atlas.AppHost

# Restore dependencies
dotnet restore
```

### Frontend (pnpm monorepo)
```bash
# Navigate to frontend workspace root
cd src/frontend

# Install all workspace dependencies
pnpm install

# Start AppWeb
pnpm run dev:app-web           # AppWeb on :5181 (connects to AppHost)

# Build frontend
pnpm run build
pnpm run build:app-web         # AppWeb only

# Lint / format all projects
pnpm run lint
pnpm run format
```

### Frontend (Legacy)
`Atlas.WebApp` 与 `platform-web` 已删除（2026-04-05），请仅使用 `src/frontend/apps/app-web`。

### Runtime Port Allocation

| Service | Port | Description |
|---|---|---|
| AppHost | 5002 | Application runtime host |
| **AppWeb** | **5181** | **New: Independent application frontend** |

AppWeb 当前仅支持直连 `AppHost` 的运行模式；历史 `Atlas.PlatformHost` 与对应前端链路已不在本仓库中。

### API Testing
- Use `.http` files in `src/backend/Atlas.AppHost/Bosch.http/` for testing endpoints
- Format: REST Client syntax with variable extraction and request chaining
- **Required:** Create or update `.http` files for every new/modified endpoint

## Architecture Overview

### Clean Architecture Layers

```
Atlas.Core (Foundation)
  ↑
Atlas.Domain + Module.Domain (Entities)
  ↑
Atlas.Application + Module.Application (DTOs, Validators, Mappings)
  ↑
Atlas.Infrastructure (Service Implementations, Repositories, SqlSugar)
  ↑
Atlas.AppHost (Controllers, Middleware, Host API Layer)
```

**Dependency Rule:** Dependencies flow inward toward Core. Outer layers depend on inner layers, never vice versa.

### Bounded Contexts

Each major feature is organized as a separate bounded context with:
- **Domain Layer:** `Atlas.Domain.{Context}` - Entities inheriting from `TenantEntity`
- **Application Layer:** `Atlas.Application.{Context}` - DTOs, validators, AutoMapper profiles, service interfaces
- **Infrastructure:** Service implementations in `Atlas.Infrastructure/Services/{Context}*Service.cs`
- **Presentation:** Controllers in `Atlas.AppHost/Controllers/`

**Current Contexts:** Assets, Audit, Alert, plus cross-cutting concerns (Auth, Users, Roles, Permissions, Departments, Menus)

### Multi-Tenancy Architecture

- **Header-based:** Tenant ID passed via `X-Tenant-Id` header (configurable in appsettings.json)
- **TenantEntity:** Base class for all tenant-scoped entities with automatic `TenantId` filtering
- **TenantContextMiddleware:** Extracts tenant ID from headers and provides it to services
- **QueryFilter:** SqlSugar automatically filters queries by `TenantId` for data isolation
- **Validation:** JWT claims and header `X-Tenant-Id` must match for authenticated requests

### Key Patterns

**Query/Command Separation:**
- `I{Context}QueryService` - Read operations (queries, searches, paging)
- `I{Context}CommandService` - Write operations (create, update, delete)

**Repository Pattern:**
- Abstractions in Application layer: `I{Context}Repository`
- Implementations in Infrastructure layer using SqlSugar

**Dependency Injection:**
- All services registered in `ServiceCollectionExtensions.cs` within each layer
- Options pattern for configuration binding (Jwt, Security, Database, Tenancy, etc.)

**Error Handling:**
- `ExceptionHandlingMiddleware` catches exceptions globally
- Returns standardized `ApiResponse<T>` envelope
- FluentValidation throws `ValidationException` for input errors
- Business logic throws `BusinessException` with error codes

## Project Structure

```
src/
├── backend/
│   ├── Atlas.Core/                      # Shared abstractions, base entities, models
│   │   ├── Abstractions/                # EntityBase, TenantEntity, AggregateRoot
│   │   ├── Models/                      # ApiResponse, PagedResult, ErrorCodes
│   │   ├── Tenancy/                     # TenantContext, TenantProvider
│   │   └── Exceptions/                  # BusinessException
│   │
│   ├── Atlas.Domain/                    # Domain layer base
│   ├── Atlas.Domain.{Context}/          # Context-specific entities (Assets, Audit, Alert)
│   │
│   ├── Atlas.Application/               # Application base with AutoMapper setup
│   ├── Atlas.Application.{Context}/     # Context-specific application logic
│   │   ├── Abstractions/                # Query/Command service interfaces
│   │   ├── Models/                      # DTOs (Request/Response/ListItem)
│   │   ├── Validators/                  # FluentValidation validators
│   │   ├── Repositories/                # Repository abstractions
│   │   └── Mappings/                    # AutoMapper profiles
│   │
│   ├── Atlas.Infrastructure/            # Infrastructure implementations
│   │   ├── Services/                    # *QueryService, *CommandService
│   │   ├── Repositories/                # SqlSugar repository implementations
│   │   ├── IdGen/                       # SnowflakeIdGenerator
│   │   ├── Security/                    # Pbkdf2PasswordHasher
│   │   └── Options/                     # Configuration classes
│   │
│   ├── Atlas.AppHost/                   # App runtime data plane host
│   │   ├── Controllers/                 # Runtime APIs
│   │   ├── appsettings.json             # App host configuration
│   │   └── nlog.config                  # Logging setup
│   └── Atlas.Presentation.Shared/       # Shared presentation middleware/filters
│
└── frontend/                              # pnpm monorepo workspace
    ├── package.json                       # Workspace root scripts
    ├── pnpm-workspace.yaml                # Workspace config
    ├── tsconfig.base.json                 # Shared TS base config
    ├── packages/                          # shared capabilities and business modules
    ├── apps/
    │   └── app-web/                       # Atlas.AppWeb (port 5181)
    │       ├── src/
    │       │   ├── pages/                 # App pages (runtime, ai, approval, etc.)
    │       │   ├── layouts/               # AppRuntimeLayout
    │       │   ├── services/              # App API client
    │       │   ├── stores/                # App state management
    │       │   ├── i18n/                  # App i18n
    │       │   └── router/                # App routes
    │       └── rsbuild.config.ts
```

## Coding Standards

### General Rules
- **Zero-warning policy:** Build must complete with 0 errors and 0 warnings (enforced by `Directory.Build.props`)
- **Async/await mandatory:** All I/O operations must be asynchronous
- **Repository pattern required:** No direct database access from controllers
- **Nullable reference types:** Enabled for all C# projects

### .NET Conventions
- **Indentation:** 4 spaces
- **Naming:** PascalCase for types/public members, camelCase for locals/fields
- **Target Framework:** net10.0
- **Implicit Usings:** Enabled
- **File-scoped Namespaces:** Preferred

### React/TypeScript Conventions
- **Indentation:** 2 spaces
- **Component Files:** kebab-case (e.g., `login-page.tsx`)
- **Component Names:** PascalCase in code (e.g., `LoginPage`)
- **TypeScript:** Strict mode enabled, no unused variables
- **UI Framework (MANDATORY):** ALL user-visible UI components MUST use `@douyinfe/semi-ui` (Semi Design). Importing Ant Design, MUI, Chakra UI, Element Plus, or any other third-party component library is **strictly forbidden**. Custom components must be built on top of Semi components. Every `apps/*` or `packages/*` package containing interactive UI must declare `@douyinfe/semi-ui ^2.82.0` in its `package.json` `dependencies`.

## Security and Compliance (等保2.0)

This project must comply with GB/T 22239-2019 (等保2.0) Level 3 requirements. See `等保2.0要求清单.md` for full checklist.

### Implemented Security Controls

**Authentication:**
- JWT Bearer tokens with configurable expiration (default: 60 minutes)
- Client certificate authentication support
- Token validation: issuer, audience, signature, lifetime

**Authorization:**
- Role-based access control (RBAC)
- Controller action-level `[Authorize(Roles = "...")]` attributes
- Default policy requires authentication

**Password Security:**
- PBKDF2 hashing with salt (`Pbkdf2PasswordHasher`)
- Complexity requirements: min 8 chars, uppercase, lowercase, digit, special char
- 90-day expiration policy
- Lockout after 5 failed attempts (15-min lockout, 30-min auto-unlock)

**Data Protection:**
- Multi-tenant row-level isolation via `TenantEntity` + QueryFilter
- SQLite database encryption option (configurable)
- Automated database backups (default: daily, 30-day retention)

**Audit Logging:**
- `IAuditWriter` service captures: actor, action, target, IP, user agent, timestamp
- Separate Audit bounded context
- Logs retained for 180 days (configurable)

**HTTP Security:**
- HTTPS enforcement (production)
- CORS whitelist validation
- HTTP request/response logging

### Configuration Security
- **Secrets:** Never commit secrets to repository
- Use environment variables or secure secret stores in production
- JWT `SigningKey` must be 32+ characters in production
- Default `appsettings.json` contains placeholders only

## API Contract

### Standardized Response Envelope
```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "OK",
  "traceId": "00-...",
  "data": { ... }
}
```

**Error Codes:** `SUCCESS`, `VALIDATION_ERROR`, `UNAUTHORIZED`, `FORBIDDEN`, `ACCOUNT_LOCKED`, `PASSWORD_EXPIRED`, `NOT_FOUND`, `SERVER_ERROR`

### Pagination
```json
{
  "pageIndex": 1,
  "pageSize": 10,
  "total": 100,
  "items": [...]
}
```

### Headers
- `Authorization: Bearer <token>` - Required for authenticated endpoints
- `X-Tenant-Id: <guid>` - Required for all requests (matches JWT claim)

### Frontend Integration
- **AppWeb** API client: `src/frontend/apps/app-web/src/services/api-core.ts`
- **Shared auth utilities**: `src/frontend/packages/shared-react-core/src/utils/auth.ts`
- Token stored in namespaced storage (managed by `@atlas/shared-react-core`, Platform/App 已隔离)
- AppWeb 统一直连 `AppHost`

## Development Workflow

### Adding a New Feature (Clean Architecture)

1. **Define Entity** in `Atlas.Domain.{Context}/Entities/{Entity}.cs`
   - Inherit from `TenantEntity` for tenant isolation
   - Add domain properties and methods

2. **Create DTOs** in `Atlas.Application.{Context}/Models/`
   - `{Entity}CreateRequest`, `{Entity}UpdateRequest`, `{Entity}Response`, `{Entity}ListItem`

3. **Add Validators** in `Atlas.Application.{Context}/Validators/`
   - Use FluentValidation for input validation
   - Example: `{Entity}CreateRequestValidator : AbstractValidator<{Entity}CreateRequest>`

4. **Define Repository Interface** in `Atlas.Application.{Context}/Repositories/`
   - `I{Entity}Repository` with CRUD methods

5. **Create Mapping Profile** in `Atlas.Application.{Context}/Mappings/`
   - AutoMapper profile: `{Entity}MappingProfile : Profile`

6. **Implement Repository** in `Atlas.Infrastructure/Repositories/`
   - SqlSugar-based implementation of `I{Entity}Repository`

7. **Create Services** in `Atlas.Infrastructure/Services/`
   - `{Entity}QueryService` for read operations
   - `{Entity}CommandService` for write operations

8. **Register Services** in `Atlas.Infrastructure/ServiceCollectionExtensions.cs`
   - Add repository and service DI registrations

9. **Add Controller** in `Atlas.AppHost/Controllers/`
   - `{Entity}Controller : ControllerBase`
   - Use `[Authorize]` attributes for access control
   - Inject query/command services via constructor

10. **Create Test File** in `Atlas.AppHost/Bosch.http/`
    - `{Entity}.http` with sample requests for all endpoints

11. **Update Frontend** (if needed)
    - App capability: update `src/frontend/apps/app-web`（services/pages/router/types）
    - Shared contract/util: update `src/frontend/packages/*`

### Modifying Existing Code

1. **Read the file first** - NEVER propose changes to code you haven't read
2. **Understand existing patterns** - Follow established conventions
3. **Minimal changes** - Don't refactor unrelated code or add unnecessary features
4. **Update tests** - Modify corresponding `.http` file if API changes
5. **Run validation** - Ensure `dotnet build` passes with 0 warnings

## Configuration Files

### appsettings.json
```json
{
  "Jwt": {
    "Issuer": "...",
    "Audience": "...",
    "SigningKey": "32+ character secret",
    "ExpiresMinutes": 60
  },
  "Security": {
    "EnforceHttps": true,
    "PasswordPolicy": { "MinLength": 8, ... },
    "LockoutPolicy": { "MaxFailedAttempts": 5, ... },
    "BootstrapAdmin": { "Enabled": true, "TenantId": "...", ... }
  },
  "Database": {
    "ConnectionString": "Data Source=atlas.db",
    "Encryption": { "Enabled": false },
    "Backup": { "Enabled": true, "IntervalHours": 24, "RetentionDays": 30 }
  },
  "Tenancy": {
    "HeaderName": "X-Tenant-Id"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:5180", "http://localhost:5181"]
  }
}
```

### nlog.config
- Console + 4 rolling file logs (all.log, info.log, warn.log, error.log)
- Daily rotation with configurable retention
- Format: `timestamp|level|logger|message`

### rsbuild.config.ts
- Dev server: AppWeb (5181)
- proxy: `/api/*` → `http://localhost:5002`
- Path alias follows workspace package resolution

## Important Notes

### Testing
- **No unit test framework configured yet**
- Current testing: REST Client `.http` files
- When adding tests: document framework (xUnit/NUnit for .NET, Vitest for React) and commands

### File Operations
- **Always prefer editing existing files over creating new ones**
- Don't create documentation files unless explicitly requested
- Update `AGENTS.md` and `docs/contracts.md` if architecture changes

### Database Operations
- Database file: `atlas.app.e2e.db` in AppHost directory (`src/backend/Atlas.AppHost/atlas.app.e2e.db`)
- Backups: `backups/` directory (daily, 30-day retention)
- Schema initialization: `DatabaseInitializerHostedService` runs on startup
- Migrations: Not using EF Core migrations (SqlSugar code-first)

### Avoid Over-Engineering
- Don't add features beyond what was requested
- Don't add unnecessary abstractions, error handling for impossible scenarios, or hypothetical future requirements
- Three similar lines of code is better than a premature abstraction
- If something is unused, delete it completely (no backwards-compatibility hacks)

## Dag 工作流引擎（Coze Parity）更新

- Dag 工作流引擎（后端类型 `DagWorkflow*`；REST 路径 `api/v2/workflows` 中的 `v2` 为 API 版本号）已合并 LogicFlow 表达式能力（`ExprEvaluator`）并在节点执行上下文提供 `EvaluateExpression()`。
- DagExecutor 支持：
  - Selector 条件分支剪枝
  - Loop + Break/Continue 控制信号
  - Batch 子画布并发执行
  - preCompletedNodeKeys 续跑能力（Resume）
- 前端工作流编辑器采用动态节点配置面板（按 `node.type` 装载对应表单组件）。
- 节点范围覆盖 Coze 40+ 节点，按 7 大类分组，配套节点目录与模板 API。
