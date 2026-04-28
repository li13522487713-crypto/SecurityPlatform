# Mendix MVP Verify

## 命令

在 `src/frontend` 目录执行：

```bash
pnpm install
pnpm run test:unit
pnpm --filter atlas-app-web run lint
pnpm --filter atlas-app-web run build
```

## 验证点

- 可打开 `/space/:space_id/mendix-studio`
- 左导航出现 `Mendix Studio`
- 资源中心微流 Tab 存在“在 Mendix Studio 中打开”
- 可在 Studio 加载采购审批示例
- Domain Model 可新增 Entity/Attribute
- Page Builder 可添加组件并修改属性
- Microflow Designer 可编辑节点与连线，且可打开高级 `@atlas/microflow` 编辑器
- Workflow Designer 可编辑 `UserTask`/`Decision` 节点与边
- Security Editor 可查看角色与访问矩阵
- Runtime Preview 可执行提交动作并看到 `Status` 更新
- Debug Trace Drawer 可展示最近一次执行链路
- 底部 Errors Pane 可显示 Validator 输出
