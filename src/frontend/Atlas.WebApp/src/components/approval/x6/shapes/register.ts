/**
 * 统一注册所有 X6 Vue Shape 节点
 *
 * 在 Graph 初始化前调用一次 registerAllShapes()。
 */

import { register } from '@antv/x6-vue-shape';
import StartNodeShape from './StartNodeShape.vue';
import ApproveNodeShape from './ApproveNodeShape.vue';
import CopyNodeShape from './CopyNodeShape.vue';
import ConditionHeaderShape from './ConditionHeaderShape.vue';
import ConditionBranchShape from './ConditionBranchShape.vue';
import InclusiveBranchShape from './InclusiveBranchShape.vue';
import RouteBranchShape from './RouteBranchShape.vue';
import CallProcessNodeShape from './CallProcessNodeShape.vue';
import TimerNodeShape from './TimerNodeShape.vue';
import TriggerNodeShape from './TriggerNodeShape.vue';
import EndNodeShape from './EndNodeShape.vue';
import AddButtonShape from './AddButtonShape.vue';
import { LAYOUT } from '../layout';

let _registered = false;

export function registerAllShapes() {
  if (_registered) return;
  _registered = true;

  // ── 发起人 ──
  register({
    shape: 'dd-start-node',
    width: LAYOUT.NODE_W,
    height: LAYOUT.NODE_H,
    component: StartNodeShape,
  });

  // ── 审批人 ──
  register({
    shape: 'dd-approve-node',
    width: LAYOUT.NODE_W,
    height: LAYOUT.NODE_H,
    component: ApproveNodeShape,
  });

  // ── 抄送人 ──
  register({
    shape: 'dd-copy-node',
    width: LAYOUT.NODE_W,
    height: LAYOUT.NODE_H,
    component: CopyNodeShape,
  });

  // ── 条件头 ──
  register({
    shape: 'dd-condition-header',
    width: LAYOUT.NODE_W,
    height: LAYOUT.COND_HEADER_H,
    component: ConditionHeaderShape,
  });

  // ── 条件分支 ──
  register({
    shape: 'dd-condition-branch',
    width: LAYOUT.NODE_W,
    height: LAYOUT.COND_BRANCH_H,
    component: ConditionBranchShape,
  });

  // ── 包容分支 ──
  register({
    shape: 'dd-inclusive-branch',
    width: LAYOUT.NODE_W,
    height: LAYOUT.COND_BRANCH_H,
    component: InclusiveBranchShape,
  });

  // ── 路由分支 ──
  register({
    shape: 'dd-route-node',
    width: LAYOUT.NODE_W,
    height: LAYOUT.NODE_H,
    component: RouteBranchShape,
  });

  // ── 子流程 ──
  register({
    shape: 'dd-call-process-node',
    width: LAYOUT.NODE_W,
    height: LAYOUT.NODE_H,
    component: CallProcessNodeShape,
  });

  // ── 定时器 ──
  register({
    shape: 'dd-timer-node',
    width: LAYOUT.NODE_W,
    height: LAYOUT.NODE_H,
    component: TimerNodeShape,
  });

  // ── 触发器 ──
  register({
    shape: 'dd-trigger-node',
    width: LAYOUT.NODE_W,
    height: LAYOUT.NODE_H,
    component: TriggerNodeShape,
  });

  // ── 结束节点 ──
  register({
    shape: 'dd-end-node',
    width: LAYOUT.NODE_W,
    height: LAYOUT.END_H,
    component: EndNodeShape,
  });

  // ── 添加按钮 ──
  register({
    shape: 'dd-add-button',
    width: LAYOUT.ADD_BTN_W,
    height: LAYOUT.ADD_BTN_H,
    component: AddButtonShape,
  });
}

/** 从 layout shapeType 映射到 X6 shape 名 */
export function shapeNameFromType(
  shapeType: string,
): string {
  const map: Record<string, string> = {
    start: 'dd-start-node',
    approve: 'dd-approve-node',
    copy: 'dd-copy-node',
    'condition-header': 'dd-condition-header',
    'condition-branch': 'dd-condition-branch',
    'inclusive': 'dd-inclusive-branch',
    'route': 'dd-route-node',
    'callProcess': 'dd-call-process-node',
    'timer': 'dd-timer-node',
    'trigger': 'dd-trigger-node',
    end: 'dd-end-node',
    'add-button': 'dd-add-button',
    parallel: 'dd-approve-node', // 并行审批复用审批人样式
  };
  return map[shapeType] || 'dd-approve-node';
}
