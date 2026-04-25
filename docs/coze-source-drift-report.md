# Coze Source Drift Report

Generated at: 2026-04-24T20:31:32.085Z

Coze root: `D:\Code\coze-studio-main`

Local workflow root: `D:\Code\Web_SaaS_Backend\SecurityPlatform\src\frontend\packages\workflow`

## Summary

- Same files: 1039
- Different files: 18
- Missing locally: 0
- Atlas-only files: 9
- Unallowed drift count: 27

## Compared Roots

- `base/src`
- `nodes/src`
- `variable/src`
- `playground/src/node-registries`
- `playground/src/nodes-v2`

## Drift Details


### `base/src/store/workflow/index.ts`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 49

```diff
- export const useWorkflowStore = create<
+ const ENABLE_ZUSTAND_DEVTOOLS =
```

### `base/src/utils/index.ts`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 51

```diff
- 
+ export { stableStringifyWorkflowSchema } from './stable-json';
```

### `base/src/utils/schema-invariants.spec.ts`

- Status: `atlas-only`
- Allowlist: no (not allowed)

### `base/src/utils/schema-invariants.ts`

- Status: `atlas-only`
- Allowlist: no (not allowed)

### `base/src/utils/stable-json.spec.ts`

- Status: `atlas-only`
- Allowlist: no (not allowed)

### `base/src/utils/stable-json.ts`

- Status: `atlas-only`
- Allowlist: no (not allowed)

### `base/src/utils/workflow-schema-roundtrip.spec.ts`

- Status: `atlas-only`
- Allowlist: no (not allowed)

### `nodes/src/typings/node.ts`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 78

```diff
-  * Plugin data structures returned by interface /api/workflow_api/apiDetail
+  * Plugin data structures returned by the app-web workflow gateway apiDetail endpoint
```

### `nodes/src/utils/__tests__/get-llm-models.test.ts`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 98

```diff
- 
+   let fetchMock: Mock;
```

### `nodes/src/utils/get-llm-models.ts`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 30

```diff
- } from '@coze-arch/bot-api/developer_api';
+   ModelParamType,
```

### `nodes/src/workflow-document-with-format.ts`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 96

```diff
-   toNodeJSON(node: WorkflowNodeEntity): WorkflowNodeJSON {
+   /**
```

### `nodes/src/workflow-json-format.ts`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 50

```diff
- /**
+ let hasLoggedLegacyNumericNodeType = false;
```

### `playground/src/node-registries/common/fields/inputs-parameters-field/inputs-field.tsx`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 112

```diff
-                   style: { flex: 2 },
+                   style: { flex: 2, minWidth: 80 },
```

### `playground/src/node-registries/common/fields/value-expression-input.tsx`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 36

```diff
-   | 'literalDisabled'
+   | 'hideSettingIcon'
```

### `playground/src/node-registries/dataset/dataset-search/components/atlas-v5-settings.tsx`

- Status: `atlas-only`
- Allowlist: no (not allowed)

### `playground/src/node-registries/dataset/dataset-search/data-transformer.ts`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 21

```diff
- export function transformOnInit(value) {
+ /**
```

### `playground/src/node-registries/dataset/dataset-search/form.tsx`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 29

```diff
- 
+ import { AtlasV5SettingsField } from './components/atlas-v5-settings';
```

### `playground/src/node-registries/dataset/dataset-write/components/atlas-v5-write-settings.tsx`

- Status: `atlas-only`
- Allowlist: no (not allowed)

### `playground/src/node-registries/dataset/dataset-write/components/dataset-write-setting.tsx`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 24

```diff
- 
+ import { AtlasV5WriteSettingsField } from './atlas-v5-write-settings';
```

### `playground/src/node-registries/dataset/dataset-write/data-transformer.ts`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 186

```diff
-   return actualData as DatasetNodeActualData;
+   // v5 §35 / 计划 G7：把 atlasV5 设置序列化到 datasetParam，便于后端 KnowledgeIndexerNodeExecutor 透传
```

### `playground/src/node-registries/start/node-test.ts`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 68

```diff
-         const { dtoMeta } = variable;
+         const dtoMeta = variable?.dtoMeta;
```

### `playground/src/node-registries/trigger-upsert/utils/trigger-form.ts`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 144

```diff
-       eventName: '/api/workflow_api/list_trigger_events fetch error',
+       eventName: 'app-web workflow gateway list_trigger_events fetch error',
```

### `playground/src/nodes-v2/components/batch/batch.tsx`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 145

```diff
-                       },
+                         minWidth: 80,
```

### `playground/src/nodes-v2/llm/llm-form-meta.tsx`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 161

```diff
-                       },
+                         minWidth: 80,
```

### `variable/src/index.ts`

- Status: `different`
- Allowlist: no (not allowed)
- First different line: 30

```diff
- export { createWorkflowVariablePlugins } from './create-workflow-variable-plugin';
+ export {
```

### `variable/src/utils/variable-reference-index.spec.ts`

- Status: `atlas-only`
- Allowlist: no (not allowed)

### `variable/src/utils/variable-reference-index.ts`

- Status: `atlas-only`
- Allowlist: no (not allowed)

## CI Guidance

Use this report as a drift guard. A non-zero unallowed drift count means a local workflow file differs from Coze upstream without a documented reason in `tools/coze-source-diff/allowlist.json`.