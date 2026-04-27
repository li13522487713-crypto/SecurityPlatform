# 微流前端验收清单（手工）

在 `src/frontend` 执行 `pnpm dev:app-web`，按顺序勾选。

1. 工作区 → 资源库 → 切换到 **微流** Tab，列表/表格与搜索正常。
2. **新建微流**：填写名称/模块/标签，创建成功并可跳转编辑器（若已接路由）。
3. **参数**：在编辑器中打开微流设置/参数区，增删参数（与实现一致即可）。
4. **模板**：节点面板「模板」若占位，应显示「即将支持」类文案，无 `NotImplemented`。
5. **编辑器**：从资源库进入，画布、节点板、属性、问题、调试面板可打开。
6. **拖节点、连线、属性**：与现有 MicroflowEditor 能力一致。
7. **决策分支 / 变量 / 表达式**：配置后校验无异常崩溃。
8. **校验**：工具栏校验；ProblemPanel 显示 `MicroflowValidationIssue`。
9. **测试运行**：打开测试运行/调试，Mock 轨迹可显示（不崩）。
10. **保存**：保存后 Local Adapter 下刷新仍存在。
11. **发布**：发布弹窗、版本号策略；成功后状态标签变化。
12. **版本 / 引用 / 回滚 / 复制版本**：抽屉/弹窗可打开，Mock 有数据时列表非空。
13. **大样例**：在开发环境调用 `createLargeMicroflowSample(120)` 或通过内部 API 打开，不白屏、明显卡顿可接受范围。
14. **缩放 150%**：浏览器缩放，主布局不重叠至不可用。
15. **快捷键**：与 microflow 包内快捷键表一致，无与全局热键严重冲突。
16. **app-web 边界**：除路由与 `workspaceId` 外，不手写 schema 校验/Runtime DTO（见代码检索）。
17. **其他 Tab / 工作流 / Coze**：不回归破坏资源库其他 Tab 与工作流/Coze 入口。
18. **P0 属性面板**：Retrieve/CreateObject/ChangeMembers/Commit/Delete/Rollback/CreateVariable/ChangeVariable/CallMicroflow/RestCall/LogMessage 均使用强类型字段，不出现 generic config 或 raw JSON dump。
19. **字段级错误**：清空输出变量、REST URL、CallMicroflow 参数或 Loop iterator 时，字段下方显示对应 `ValidationIssue.fieldPath`。
20. **变量联动**：修改 Retrieve/CreateObject/CreateVariable/CallMicroflow/RestCall 输出变量后，下游 VariableSelector 可见，重复名提示错误。
21. **FlowGram 同步**：修改 caption、REST method/url、Log level、CallMicroflow target、输出变量、Loop iterator 后，节点标题或副标题同步更新且视口不跳回原点。
22. **变量作用域 v2**：Retrieve 输出在节点前不可见、节点后可见；Decision 分支变量在 Merge 后显示 maybe；Loop 内可见 iterator 与 `$currentIndex`，Loop 外不可见。
23. **ErrorHandler 变量**：REST error handler 内可见 `$latestError` 与 `$latestHttpResponse`，主路径不可见；WebService error handler 内可见 `$latestSoapFault`。
24. **VariableSelector v2**：显示变量名、类型、来源、scope、visibility、readonly/unknown tag；ChangeVariable 不显示 readonly/system，Commit/Delete/Rollback 只显示 object/list。
25. **ExpressionEditor v2**：插入菜单使用同一 VariableIndex；object 可插属性，list<object> 提示需循环访问成员，maybe/unknown 有提示。
26. **Runtime 契约**：`toRuntimeDto().variables.all` 与 `toExecutionPlan().variableDeclarations` 非空且数量一致。
27. **Expression v2**：`$order/Status = Sales.OrderStatus.New`、`not empty($order)`、`if $order/TotalAmount > 100 then true else false` 可解析并显示 inferredType。
28. **Validator mode**：edit/save/publish/testRun 四种模式下 severity 差异符合 `validation-contract.md`，testRun 对 modeledOnly 报 error。
29. **P0 表达式字段**：Retrieve custom range、REST form body、LogMessage arguments、CallMicroflow mappings 均进入统一 `validateExpressions`。
30. **ProblemPanel 联动**：点击 issue 选中 object/flow；字段错误通过稳定 `fieldPath` 在属性面板显示。

## 自动化补充

- `pnpm --filter @atlas/mendix-studio-core run verify-contracts`
- `pnpm --filter @atlas/microflow run typecheck`
