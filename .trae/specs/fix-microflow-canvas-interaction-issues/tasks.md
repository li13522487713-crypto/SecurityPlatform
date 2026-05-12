# Tasks

- [x] Task 1: 修复拖拽后 suppressNextNodeClickRef 过度抑制问题
  - [x] 1.1 分析当前拖拽事件流，确认 suppressNextNodeClickRef 的设置点和消费点
  - [x] 1.2 修改为使用坐标阈值判断（拖拽移动距离 < 3px 才视为 click）
  - [x] 1.3 验证：拖拽节点 A 后点击节点 B，B 的选择正常触发

- [x] Task 2: 修复全局 click listener 同时关闭三个面板的问题
  - [x] 2.1 拆分 document click listener，每个面板独立关闭逻辑
  - [x] 2.2 使用 pointerdown 替代 click 监听（避免 React 合成事件时序问题）
  - [x] 2.3 验证：Quick Insert 和 Context Menu 可独立打开/关闭

- [x] Task 3: 修复 Quick Insert 面板边界处理
  - [x] 3.1 添加面板位置计算逻辑，处理 left/top 为负值的情况
  - [x] 3.2 使用面板容器边界而非 window.innerWidth/innerHeight
  - [x] 3.3 验证：在画布四角双击，面板始终完全可见

- [x] Task 4: 修复工具栏按钮点击事件冒泡
  - [x] 4.1 在 FlowGramMicroflowToolbar 容器添加 onClickCapture={e => e.stopPropagation()}
  - [x] 4.2 验证：点击工具栏按钮不触发画布空白点击逻辑

- [x] Task 5: 修复 onDoubleClick 事件阶段一致性
  - [x] 5.1 将 onDoubleClick 改为 onDoubleClickCapture 确保与其余事件一致
  - [x] 5.2 验证：双击节点能正确触发 onNodeDoubleClick

- [x] Task 6: 对齐 Mendix 标准：节点双击打开属性对话框
  - [x] 6.1 创建 Modal 形式的属性编辑对话框组件
  - [x] 6.2 节点双击时打开对话框而非仅打开右侧面板
  - [x] 6.3 验证：双击 Start 节点弹出完整的属性配置对话框

- [x] Task 7: 对齐 Mendix 标准：空白点击关闭属性面板
  - [x] 7.1 修改画布空白点击逻辑：取消选择时同时 closePropertiesPanel
  - [x] 7.2 验证：点击空白画布后属性面板自动关闭

- [x] Task 8: 对齐 Mendix 标准：App Explorer 默认展开
  - [x] 8.1 修改 ExplorerSplitLayout 默认状态为展开
  - [x] 8.2 移除 isTempExpanded 永不重置的问题
  - [x] 8.3 验证：进入微流设计器时 App Explorer 默认展开显示

- [x] Task 9: 更新 E2E 测试
  - [x] 9.1 更新"节点双击"测试：验证打开属性对话框
  - [x] 9.2 新增"空白点击关闭属性面板"测试
  - [x] 9.3 新增"拖拽后点击其他节点正常选择"测试
  - [x] 9.4 新增"Quick Insert 面板边界不溢出"测试
  - [x] 9.5 运行全量 E2E 并确认通过

# Task Dependencies

- Task 2 depends on Task 1
- Task 4 depends on Task 1
- Task 6 depends on Task 5
- Task 7 depends on Task 4
- Task 8 is independent
- Task 9 depends on Task 1, 2, 3, 4, 5, 6, 7, 8
