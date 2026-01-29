import { ref, computed } from 'vue';
import type { ApprovalFlowTree } from '@/types/approval-tree';

export function useApprovalTreeHistory() {
  const history = ref<ApprovalFlowTree[]>([]);
  const currentIndex = ref(-1);
  
  const pushState = (state: ApprovalFlowTree) => {
    // 删除当前索引之后的历史
    if (currentIndex.value < history.value.length - 1) {
        history.value = history.value.slice(0, currentIndex.value + 1);
    }
    
    // 添加新状态
    const snapshot = JSON.parse(JSON.stringify(state));
    history.value.push(snapshot);
    currentIndex.value++;
    
    // 限制历史记录数量
    if (history.value.length > 50) {
      history.value.shift();
      currentIndex.value--;
    }
  };
  
  const undo = (): ApprovalFlowTree | null => {
    if (currentIndex.value > 0) {
      currentIndex.value--;
      return JSON.parse(JSON.stringify(history.value[currentIndex.value]));
    }
    return null;
  };
  
  const redo = (): ApprovalFlowTree | null => {
    if (currentIndex.value < history.value.length - 1) {
      currentIndex.value++;
      return JSON.parse(JSON.stringify(history.value[currentIndex.value]));
    }
    return null;
  };
  
  const canUndo = computed(() => currentIndex.value > 0);
  const canRedo = computed(() => currentIndex.value < history.value.length - 1);
  
  return { pushState, undo, redo, canUndo, canRedo };
}
