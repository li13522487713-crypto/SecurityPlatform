# 未使用 `useI18n` 的 Vue 组件清单（审计）

**生成方式**：在 `src/frontend/Atlas.WebApp` 下对 `src/**/*.vue` 扫描不含字符串 `useI18n` 的文件（可用 Node 复现，见文末）。**未命中 ≠ 一定缺 i18n**（可能使用 `i18n.global`、`translate()`、或无用户可见文案）。

**统计（当前仓库快照）**：共 **19** 个文件。

## 文件列表

| 路径 | 人工备注 |
|------|----------|
| `App.vue` | 根组件，通常无业务文案 |
| `components/ai/ChatMessage.vue` | 建议复核气泡内固定文案 |
| `components/ai/MarkdownRenderer.vue` | 渲染层，多为内容无 i18n |
| `components/ai/workflow/AiNode.vue` | 建议复核节点展示文案 |
| `components/ai/workflow/HttpNodeConfig.vue` | 建议复核表单标签 |
| `components/amis/AmisEditor.vue` | 低代码编辑器，文案多在 schema |
| `components/amis/amis-renderer.vue` | 同 Amis 数据驱动 |
| `components/approval/ApprovalTreeEditor.vue` | 建议复核树节点文案 |
| `components/approval/TreeNodeRenderer.vue` | 建议复核 |
| `components/approval/X6PreviewCanvas.vue` | 画布预览，多为图形 |
| `components/approval/designer/DesignerFlowProcess.vue` | 建议复核 |
| `components/common/EmptyState.vue` | 若对外暴露文案应走 props + 父级 `t()` |
| `components/layout/BreadcrumbView.vue` | 标题走 `resolveBreadcrumbTitle` / `titleKey`，无 composable 亦可 |
| `components/layout/MasterDetailLayout.vue` | 布局壳 |
| `components/layout/RouterContainer.vue` | 路由容器 |
| `components/layout/SidebarItem.vue` | 侧栏项，标题多来自菜单数据 |
| `components/layout/SidebarMenu.vue` | 同上 |
| `components/layout/TagsView.vue` | 已用 `i18n.global` + `resolveRouteTitle` |
| `pages/ApprovalFlowManagePage.vue` | **建议优先复核** 页面级可见字符串 |

## 已在本期 i18n 计划中处理的壳层

以下组件曾在盘点中列为高优先级，**已接入 `useI18n`**，故不会出现在上表：`ConsoleLayout.vue`、`UnifiedContextBar.vue`、`ProjectSwitcher.vue`。

## 复现命令（Node）

在 `src/frontend/Atlas.WebApp` 目录执行：

```bash
node -e "const fs=require('fs');const path=require('path');function walk(d,a=[]){for(const f of fs.readdirSync(d,{withFileTypes:true})){const p=path.join(d,f.name);if(f.isDirectory())walk(p,a);else if(f.name.endsWith('.vue'))a.push(p);}return a;}const root=path.join(process.cwd(),'src');const files=walk(root);files.filter(f=>!fs.readFileSync(f,'utf8').includes('useI18n')).sort().forEach(f=>console.log(path.relative(root,f).split(path.sep).join('/')));"
```

（若本机已安装 ripgrep，亦可：`rg --glob \"*.vue\" -L useI18n src`。）
