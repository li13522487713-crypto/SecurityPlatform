import { ref } from 'vue';
import type { Ref } from 'vue';

export interface UseDrawerFormOptions<T> {
  initData?: () => T;
  onSubmit: (data: T) => Promise<void>;
  onSuccess?: () => void;
}

export function useDrawerForm<T>(options: UseDrawerFormOptions<T>) {
  const visible = ref(false);
  const loading = ref(false);
  const formData = ref<T>() as Ref<T>;
  
  const resetForm = () => {
    if (options.initData) {
      formData.value = options.initData();
    } else {
      formData.value = {} as T;
    }
  };

  const openForm = (data?: T) => {
    if (data) {
      // deep clone for typical form objects
      formData.value = JSON.parse(JSON.stringify(data));
    } else {
      resetForm();
    }
    visible.value = true;
  };

  const closeForm = () => {
    visible.value = false;
  };

  const handleSubmit = async () => {
    loading.value = true;
    try {
      await options.onSubmit(formData.value);
      visible.value = false;
      if (options.onSuccess) {
        options.onSuccess();
      }
    } finally {
      loading.value = false;
    }
  };

  return {
    visible,
    loading,
    formData,
    openForm,
    closeForm,
    handleSubmit,
    resetForm
  };
}
