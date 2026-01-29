import type { InjectionKey } from 'vue';
import type { TreeNode, ConditionBranch } from './approval-tree';

export const AddNodeKey = Symbol('AddNode') as InjectionKey<(parentId: string, nodeType: string) => void>;
export const DeleteNodeKey = Symbol('DeleteNode') as InjectionKey<(nodeId: string) => void>;
export const SelectNodeKey = Symbol('SelectNode') as InjectionKey<(node: TreeNode | ConditionBranch | null) => void>;
export const AddConditionBranchKey = Symbol('AddConditionBranch') as InjectionKey<(nodeId: string) => void>;
export const DeleteConditionBranchKey = Symbol('DeleteConditionBranch') as InjectionKey<(branchId: string) => void>;
