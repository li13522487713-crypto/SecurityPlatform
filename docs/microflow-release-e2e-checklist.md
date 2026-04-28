# Microflow Release E2E Checklist

本文是 Microflow 发布验收清单。所有路径必须通过 `/space/:workspaceId/mendix-studio/:appId` 完成，不允许新增孤立 demo 页面绕过目标路由。

## Startup

Backend:

```bash
dotnet run --project src/backend/Atlas.AppHost
```

Frontend:

```bash
cd src/frontend
pnpm install
pnpm run dev:app-web
```

Default credentials:

- Tenant ID: `00000000-0000-0000-0000-000000000001`
- Username: `admin`
- Password: `P@ssw0rd!`

Target route:

```text
http://localhost:5181/space/{workspaceId}/mendix-studio/{appId}
```

Deep link:

```text
http://localhost:5181/space/{workspaceId}/mendix-studio/{appId}?microflowId={microflowId}
```

## Automated Commands

Backend:

```bash
dotnet build
dotnet test tests/Atlas.AppHost.Tests --filter "FullyQualifiedName~Microflow"
dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName~MicroflowIntegration"
```

Frontend:

```bash
cd src/frontend
pnpm test:unit
pnpm verify:microflow-contracts
pnpm verify:microflow-no-production-mock
pnpm verify:microflow-http-error-handling
pnpm i18n:check
pnpm build:app-web
pnpm test:e2e:app
```

## Manual Checklist

### 1. Target Route

- Open `/space/:workspaceId/mendix-studio/:appId`.
- Confirm Studio loads workspace/app context.
- Confirm Network contains `/api/v1/microflows` and `/api/v1/microflow-metadata`.
- Confirm no request is served by localStorage adapter.

Expected:

- App Explorer appears.
- Microflows section is loaded from API.
- No console uncaught promise.

### 2. Create Microflow

- Open Microflows section.
- Create `MF_ValidatePurchaseRequest`.
- Add parameter `amount:Number`.
- Add Decision node with expression `amount > 100`.
- Add true End returning `true`.
- Add false End returning `false`.
- Save.

Expected:

- Create success refreshes tree.
- New tab opens.
- Save shows success time.
- Refresh page restores node/edge/property state.

### 3. Duplicate Name Failure

- Try creating another `MF_ValidatePurchaseRequest`.

Expected:

- Modal stays open.
- Inline error appears.
- Toast shows readable 409/422 message.
- traceId is visible in UI or logs.

### 4. Deep Link

- Copy current `microflowId`.
- Refresh `/space/:workspaceId/mendix-studio/:appId?microflowId={id}`.

Expected:

- The microflow tab opens automatically.
- Missing id shows not-found state and allows returning to list.

### 5. Multi Tab Isolation

- Create/open `MF_A`, `MF_B`, `MF_C`.
- Edit only `MF_A`.
- Switch between tabs.

Expected:

- Dirty state is per tab.
- `MF_B` and `MF_C` schema are not changed.
- Closing dirty tab prompts guard.

### 6. Call Microflow

- Create `MF_SubmitPurchaseRequest`.
- Add parameter `amount:Number`.
- Add Call Microflow node.
- Select `MF_ValidatePurchaseRequest`.
- Map `amount` to `amount`.
- Bind return value to a variable or end return.
- Save.

Expected:

- Selector shows real metadata.
- No `Sales.*` mock values.
- Parameter mapping persists after refresh.

### 7. Test Run

- Run `MF_SubmitPurchaseRequest` with `amount=120`.

Expected:

- Output is `true`.
- Trace shows Submit -> Validate.
- Call stack includes both microflows.
- Failed node is clickable when errors occur.

- Run `MF_SubmitPurchaseRequest` with `amount=50`.

Expected:

- Output is `false`.
- Decision false branch is visible in trace.

### 8. Delete Reference Protection

- Try deleting `MF_ValidatePurchaseRequest` while Submit references it.

Expected:

- Backend returns 409.
- UI shows reference protection message.
- References panel opens with inbound reference.
- Delete is blocked.

### 9. Rename Reference Stability

- Rename `MF_ValidatePurchaseRequest`.
- Run Submit again.

Expected:

- Target reference remains valid by id.
- Run still returns true/false correctly.

### 10. Publish

- Publish `MF_ValidatePurchaseRequest`.
- Publish `MF_SubmitPurchaseRequest`.

Expected:

- Publish runs validation first.
- Validation issues are clickable.
- Published snapshot is created.

### 11. Error Differentiation

Trigger and verify:

- 401 unauthenticated.
- 403 permission denied.
- 409 save conflict or reference blocked.
- 422 schema validation error.
- 500 server/storage error.

Expected:

- UI does not show all errors as service unavailable.
- Category-specific message appears.
- traceId is visible.

### 12. Security Limits

- Recursive Call Microflow over `maxCallDepth`.
- Loop over `maxLoopIterations`.
- Runtime timeout.
- RestCall with `allowRealHttp=false`.
- RestCall to private network.

Expected:

- `CALL_DEPTH_EXCEEDED`.
- `LOOP_LIMIT_EXCEEDED`.
- `RUNTIME_TIMEOUT`.
- `EXTERNAL_CALL_BLOCKED`.
- SSRF policy blocks private network.

## Playwright Coverage

Required specs:

| Spec | Coverage |
|---|---|
| `mendix-studio-microflow-create-save.spec.ts` | target route, create, duplicate failure, save refresh |
| `mendix-studio-microflow-call-runtime.spec.ts` | Validate + Submit + Call Microflow + test-run true/false + trace |
| `mendix-studio-microflow-references-delete.spec.ts` | delete reference protection |
| `mendix-studio-microflow-no-uncaught.spec.ts` | console/pageerror no uncaught |

## Release Gates

- No `IMicroflowMockRuntimeRunner` in backend runtime path.
- No `CreateMockResponse` in RestCall runtime.
- No fixed test-run success result.
- No production fallback to `mock-metadata.ts`.
- No production fallback to `createLocalMicroflowApiClient`.
- No shared single `microflowSchema` state for workbench tabs.
- All validation/build/test commands pass or documented with external blockers.
