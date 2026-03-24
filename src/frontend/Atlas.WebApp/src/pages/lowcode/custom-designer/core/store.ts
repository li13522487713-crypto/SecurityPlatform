import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import type { ComponentSchema } from './types';

export const useDesignerStore = defineStore('customDesigner', () => {
  const schema = ref<ComponentSchema>({
    id: 'root',
    type: 'page',
    name: 'Page',
    props: {},
    styles: { padding: '24px', minHeight: '100vh', backgroundColor: '#f0f2f5' },
    children: []
  });

  const selectedId = ref<string | null>(null);
  const hoverId = ref<string | null>(null);
  const isPreviewMode = ref(false);
  const deviceType = ref<'desktop' | 'tablet' | 'mobile'>('desktop');

  // History management
  const past = ref<string[]>([]);
  const future = ref<string[]>([]);

  const commit = () => {
    past.value.push(JSON.stringify(schema.value));
    future.value = [];
  };

  const undo = () => {
    if (past.value.length === 0) return;
    future.value.push(JSON.stringify(schema.value));
    schema.value = JSON.parse(past.value.pop()!);
  };

  const redo = () => {
    if (future.value.length === 0) return;
    past.value.push(JSON.stringify(schema.value));
    schema.value = JSON.parse(future.value.pop()!);
  };

  const setSchema = (newSchema: ComponentSchema) => {
    commit();
    schema.value = newSchema;
  };

  const findComponent = (node: ComponentSchema, id: string): ComponentSchema | null => {
    if (node.id === id) return node;
    if (node.children) {
      for (const child of node.children) {
        const found = findComponent(child, id);
        if (found) return found;
      }
    }
    return null;
  };

  const selectedComponent = computed(() => {
    if (!selectedId.value) return null;
    return findComponent(schema.value, selectedId.value);
  });

  return {
    schema,
    selectedId,
    hoverId,
    isPreviewMode,
    deviceType,
    selectedComponent,
    commit,
    undo,
    redo,
    setSchema,
    canUndo: computed(() => past.value.length > 0),
    canRedo: computed(() => future.value.length > 0)
  };
});
