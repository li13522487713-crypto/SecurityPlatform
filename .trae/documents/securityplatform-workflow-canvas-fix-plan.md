# SecurityPlatform 工作流画布体验修复计划

## 1. 计划概要

基于 Coze Studio vs SecurityPlatform 的深度对比分析，已完成 P0 阶段排查。核心发现：

- **核心代码 95%+ 完全一致** — 画布/连接线/节点交互代码无差异
- **依赖包版本 100% 一致** — playground、base、render 三个包的 package.json 完全相同
- **代码差异在少量定制逻辑** — endDrag/setDropNode 微调、新增 updateDropTarget/clearDropNode
- **差异主要在后端服务和数据层** — API 响应速度、节点模板数据完整性、节点注册钩子完整度

本计划按修复优先级排序，系统性消除体验差距。

---

## 2. 排查结论（P0 阶段已完成）

### 2.1 核心代码对比结论

| 文件 | 差异行数 | 状态 |
|------|----------|------|
| workflow-line-service.ts | **0** | 完全一致 ✅ |
| workflow-edit-service.ts | **0** | 完全一致 ✅ |
| workflow-drag-service.ts | **3** | 微小差异 (endDrag/setDropNode) |
| lines-layer.tsx | **0** | 完全一致 ✅ |
| bezier-line/index.tsx | **0** | 完全一致 ✅ |
| workflow-render-contribution.ts | **0** | 完全一致 ✅ |
| workflow-port-render/index.tsx | **0** | 完全一致 ✅ |
| playground/package.json | **0** | 完全一致 ✅ |
| base/package.json | **0** | 完全一致 ✅ |
| render/package.json | **0** | 完全一致 ✅ |

### 2.2 已知代码差异详情

#### (1) workflow-drag-service.ts — endDrag() 方法

**SecurityPlatform 额外逻辑**:
```typescript
// Coze Studio
if (!isDragging && !dragNode?.type) { return; }

// SecurityPlatform
if (!isDragging && !dragNode?.type && !dropNode) { return; }
// + this.setDropNode(undefined); // 显式清理
```

**影响评估**: 无害，更保守的清理逻辑。

#### (2) workflow-drag-service.ts — setDropNode() 方法

**SecurityPlatform 额外逻辑**:
```typescript
// Coze Studio: 直接赋值
this.state.dropNode = newDropNode;

// SecurityPlatform: 增加防重复
if (this.state.dropNode === newDropNode) { return; }
this.state.dropNode = newDropNode;
```

**影响评估**: 优化，避免相同 dropNode 重复触发 selectService。

#### (3) workflow-drag-service.ts — 新增方法

```typescript
public updateDropTarget(params): void {
  const { dropNode } = this.computeCanDrop(params);
  this.setDropNode(dropNode);
}

public clearDropNode(): void {
  this.setDropNode(undefined);
}
```

**影响评估**: 新增功能，不影响现有逻辑。

### 2.3 节点注册钩子完整性检查结果

详细报告见: [node-registry-completeness-report.md](file:///D:/Code/Web_SaaS_Backend/SecurityPlatform/docs/node-registry-completeness-report.md)

**核心发现**:

| 检查项 | Coze Studio | SecurityPlatform | 差异 |
|--------|-------------|-------------------|------|
| node-registries 目录 (34 个文件) | 3 个实现了生命周期钩子 | 3 个实现了生命周期钩子 | **无差异** ✅ |
| nodes-v2 目录 (14 个文件) | 不存在此目录 | 14 个均**缺失**生命周期钩子 | **SecurityPlatform 独有缺失** ⚠️ |

**SecurityPlatform 独有的 nodes-v2 节点缺失钩子**:
- LLM 节点 (chat, cot, skills, system-prompt)
- Image-Generate / Image-Reference
- Database 节点 (Query, Create, Update, Delete)
- Variable-Assign / Variable-Merge
- Chat-Template / Chat-Start

**缺失钩子影响**: V2 架构节点初始化、验证、销毁可能不完整，导致节点行为不稳定。

---

## 3. 根因排序（按影响程度）

| 排名 | 根因 | 影响范围 | 排查/修复难度 |
|------|------|----------|---------------|
| 1 | **后端 API 响应速度** | 全局（面板打开/验证/保存） | 中 |
| 2 | **节点模板数据完整性** | 节点面板显示 | 低 |
| 3 | **nodes-v2 节点注册钩子缺失** | V2 节点稳定性 | 中 |
| 4 | **自定义样式差异** | 视觉/热区 | 低 |
| 5 | **验证服务不完整** | 错误提示 | 低 |

---

## 4. 修复计划详情

### Phase 1: 后端排查（需要运行环境）

#### P0-1: 后端 API 响应速度排查

**目标**: 定位并优化慢速 API，确保关键接口 < 200ms

**步骤**:

1. 启动后端服务，使用浏览器 DevTools Network 记录以下 API 响应时间:
   - 节点模板列表 API
   - 验证 API (validate_tree / validate_schema)
   - 保存 API

2. 如果响应时间 > 200ms，排查:
   - 数据库查询是否缺少索引
   - Redis 缓存是否生效
   - 是否有同步阻塞

**涉及文件**:
- `backend/api/handler/coze/workflow_service.go`
- `backend/domain/workflow/internal/canvas/validate/canvas_validate.go`

**验收标准**:
- [ ] 所有关键 API 响应时间 < 200ms (p95)

---

#### P0-2: 节点模板数据完整性检查

**目标**: 确保节点面板显示完整的节点列表和分类

**步骤**:

1. 在浏览器控制台检查 `WorkflowPlaygroundContext` 的数据:
   - `nodeTemplateMap.size` — 节点模板数量
   - `nodeCategoryList.length` — 分类数量
   - `pluginApiMap` — 插件 API 数量

2. 与 Coze Studio 对比数据量

3. 如果差异 > 10%，排查后端 NodeTemplateList 接口

**涉及文件**:
- `frontend/packages/workflow/playground/src/workflow-playground-context.ts` (第 119-164 行)

**验收标准**:
- [ ] 节点模板数量与 Coze 一致
- [ ] 节点面板显示正常

---

### Phase 2: 代码层修复

#### P2-1: 补全 nodes-v2 节点注册钩子

**目标**: 为所有 nodes-v2 节点补全 `onInit`、`checkError`、`onDispose` 钩子

**优先级排序**:
1. **LLM 节点** (chat, cot, skills, system-prompt) — 影响最大
2. **Image-Generate** — 缺失 size/defaultPorts
3. **Database 节点** (Query, Create, Update, Delete)
4. **Variable-Assign / Variable-Merge**
5. **Chat-Template / Chat-Start**

**修复模式** (参考 Plugin/Sub-Workflow/Database-Create 已有的实现):

```typescript
const nodeRegistry: WorkflowNodeRegistry = {
  meta: { /* ... */ },

  // 1. onInit — 节点创建时初始化
  onInit: async (nodeJSON: WorkflowNodeJSON, context: any) => {
    // 初始化表单数据、设置默认值等
  },

  // 2. checkError — 节点验证
  checkError: (nodeJSON: WorkflowNodeJSON, context: any): string | undefined => {
    // 返回错误信息，无错误返回 undefined
  },

  // 3. onDispose — 节点销毁时清理
  onDispose: (nodeJSON: WorkflowNodeJSON, context: any) => {
    // 清理资源、释放订阅等
  },

  // 4. getNodeInputParameters — 获取输入参数
  getNodeInputParameters: (node: FlowNodeEntity): InputValueVO[] => {
    // 返回节点的输入参数列表
  },

  // 5. getNodeOutputs — 获取输出
  getNodeOutputs: (node: FlowNodeEntity): OutputValueVO[] => {
    // 返回节点的输出列表
  },
};
```

**涉及文件** (按优先级):
- `frontend/packages/workflow/playground/src/nodes-v2/llm/*/node-registry.ts`
- `frontend/packages/workflow/playground/src/nodes-v2/image-generate/node-registry.ts`
- `frontend/packages/workflow/playground/src/nodes-v2/database-*/node-registry.ts`
- `frontend/packages/workflow/playground/src/nodes-v2/variable-*/node-registry.ts`
- `frontend/packages/workflow/playground/src/nodes-v2/chat-*/node-registry.ts`

**验收标准**:
- [ ] 所有 nodes-v2 节点实现 onInit
- [ ] 所有 nodes-v2 节点实现 checkError
- [ ] 所有 nodes-v2 节点实现 onDispose
- [ ] 节点初始化、验证、销毁行为正常

---

#### P2-2: 样式文件对比对齐

**目标**: 消除关键交互样式的差异

**对比文件**:

| 样式文件 | 影响 | 优先级 |
|----------|------|--------|
| `render/src/components/lines/index.module.less` | 连接线样式/热区 | P0 |
| `render/src/components/workflow-port-render/index.module.less` | 端口大小/热区 | P0 |
| `playground/src/components/node-panel/styles.module.less` | 节点面板视觉 | P1 |

**重点关注**:
- 端口圆点大小和点击热区（影响连线吸附精度感知）
- 连接线 stroke-width（影响视觉粗细）
- 拖拽 ghost 元素样式

**验收标准**:
- [ ] 端口热区一致
- [ ] 连接线样式一致

---

#### P2-3: 验证服务完整性

**目标**: 确保验证错误提示清晰准确

**检查清单**:
- [ ] `validateNode()` — 节点验证逻辑完整
- [ ] `validateWorkflow()` — 工作流验证逻辑完整
- [ ] `isLineError()` — 连接线错误检查正常
- [ ] 后端验证返回格式一致
- [ ] 错误提示文案清晰

**涉及文件**:
- `frontend/packages/workflow/playground/src/services/workflow-validation-service.ts`
- `frontend/packages/workflow/base/src/services/validation-service.ts`

---

### Phase 3: 体验优化

#### P3-1: 历史撤销系统完整性

**检查操作注册清单**:
```typescript
// history/src/operation-metas/index.ts
export const operationMetas = [
  addLineOperationMeta,
  deleteLineOperationMeta,
  addNodeOperationMeta,
  deleteNodeOperationMeta,
  moveNodeOperationMeta,
];
```

**验收标准**:
- [ ] 添加/删除节点可撤销
- [ ] 添加/删除连线可撤销
- [ ] 移动节点可撤销

---

#### P3-2: 快捷键系统完善

**检查快捷键**:
- `Ctrl+Z` / `Ctrl+Y` — 撤销/重做
- `Ctrl+C` / `Ctrl+V` — 复制/粘贴
- `Delete` — 删除
- `Ctrl+A` — 全选

**涉及文件**:
- `frontend/packages/workflow/playground/src/shortcuts/contributions/`

---

### Phase 4: 高级功能

#### P4-1: 节点封装功能检查

**涉及文件**:
- `frontend/packages/workflow/feature-encapsulate/src/encapsulate/encapsulate-lines-service.ts`
- `frontend/packages/workflow/feature-encapsulate/src/encapsulate/encapsulate-nodes-service.ts`

**验收标准**:
- [ ] 节点封装功能正常
- [ ] 解封装后连线正确恢复

---

#### P4-2: 调试画布模式

**启用方式**: 访问 `?playground_debug` URL 参数

**涉及文件**:
- `frontend/packages/workflow/render/src/workflow-render-contribution.ts` (第 120-122 行)

---

## 5. 验证计划

### 5.1 自动化测试

```bash
pnpm run build:app-web
pnpm run test:unit -- workflow
```

### 5.2 手动测试清单

| 测试项 | 预期结果 | 优先级 |
|--------|----------|--------|
| 打开节点面板 | 即时打开，数据完整 | P0 |
| 拖拽节点到画布 | 流畅放置，位置准确 | P0 |
| 从端口拖出连线 | 线条跟随鼠标 | P0 |
| 连线吸附到端口 | 精准吸附 | P0 |
| 连线上的 + 按钮 | 悬浮显示，点击弹出面板 | P0 |
| 删除节点 | 有确认弹窗 | P0 |
| 撤销/重做 | 正确回退/前进 | P0 |
| 节点验证错误 | 清晰标注 | P0 |
| 缩放画布 | 平滑缩放 | P1 |
| 平移画布 | 流畅拖拽 | P1 |
| 框选节点 | 精准选择 | P1 |
| 快捷键响应 | 即时响应 | P1 |

### 5.3 性能指标

| 指标 | 目标值 | 测量方法 |
|------|--------|----------|
| API 响应时间 (p95) | < 200ms | DevTools Network |
| 节点面板打开时间 | < 100ms | DevTools Performance |
| 拖拽帧率 | 60fps | DevTools Performance Monitor |
| 页面交互响应时间 | < 50ms | DevTools Performance |

---

## 6. 假设与决策

### 假设

1. Coze Studio 的体验作为基准参考
2. SecurityPlatform 的核心画布代码不需要重写（已验证 95%+ 一致）
3. 后端服务可以优化到与 Coze 相同的响应速度

### 决策

1. **不重写核心画布代码** — 核心代码已一致，问题在数据和服务层
2. **优先排查后端** — API 响应速度是最大影响因素
3. **先排查再修复** — 通过数据和日志定位根因，避免盲目修改

---

## 7. 执行建议

1. **后端排查优先** — 启动后端服务，用 DevTools 测量 API 响应时间
2. **量化对比** — 所有改进用数据说话
3. **逐步验证** — 每个修复项完成后手动验证体验改善
4. **保持同步** — 定期对比 Coze Studio 体验
