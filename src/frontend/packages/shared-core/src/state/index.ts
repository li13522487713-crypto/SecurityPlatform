import { computed, ref } from "vue";

export function createAsyncState<T>(defaultValue: T) {
  const data = ref<T>(defaultValue);
  const loading = ref(false);
  const error = ref<string | null>(null);

  return {
    data,
    loading,
    error,
    hasError: computed(() => Boolean(error.value))
  };
}
