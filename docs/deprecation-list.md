# Deprecated API and Route List

## Scope

- Window: 6 months from 2026-03-08
- Rule: deprecated endpoints remain available for security fixes and critical bug fixes only
- Baseline: new implementation uses `api/v1/platform/*`, `api/v1/app-manifests/*`, `api/v1/runtime/*`, `api/v1/packages/*`, `api/v1/licenses/*`, `api/v1/tools/*`

## API Mapping

| Old API | New API | Status |
|---|---|---|
| `GET /api/v1/lowcode-apps` | `GET /api/v1/app-manifests` | Deprecated |
| `POST /api/v1/lowcode-apps` | `POST /api/v1/app-manifests` | Deprecated |
| `GET /api/v1/lowcode-apps/{id}` | `GET /api/v1/app-manifests/{id}` | Deprecated |
| `PUT /api/v1/lowcode-apps/{id}` | `PUT /api/v1/app-manifests/{id}` | Deprecated |
| `POST /api/v1/lowcode-apps/{id}/publish` | `POST /api/v1/app-manifests/{id}/releases` | Deprecated |
| `GET /api/v1/lowcode-apps/{id}/export` | `POST /api/v1/packages/export` | Deprecated |
| `POST /api/v1/lowcode-apps/import` | `POST /api/v1/packages/import` | Deprecated |
| `GET /api/v1/license/status` | `GET /api/v1/licenses/validate` | Deprecated |
| `POST /api/v1/license/activate` | `POST /api/v1/licenses/import` | Deprecated |

## Frontend Route Mapping

| Old Route | New Route | Status |
|---|---|---|
| `/console/settings/*` | `/settings/*` | Deprecated |
| `/console/datasources` | `/settings/system/datasources` | Deprecated |
| `/console/settings/system/configs` | `/settings/system/configs` | Deprecated |
| `/lowcode/apps` | `/console/apps` | Deprecated |
| `/apps/:appId/run/:pageKey` | `/r/:appKey/:pageKey` | Deprecated |

## Notes

- Existing integrations should migrate to the new routes before the sunset date.
- New feature work is disallowed on deprecated APIs and routes.
