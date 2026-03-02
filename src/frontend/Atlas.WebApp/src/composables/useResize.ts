import { ref, onMounted, onBeforeUnmount, onBeforeMount } from 'vue';

const { body } = document;
const WIDTH = 992; // refer to Bootstrap's responsive design

export function useResize() {
  const isMobile = ref(false);

  const _isMobile = () => {
    const rect = body.getBoundingClientRect();
    return rect.width - 1 < WIDTH;
  };

  const resizeHandler = () => {
    if (!document.hidden) {
      const mobile = _isMobile();
      isMobile.value = mobile;
    }
  };

  onBeforeMount(() => {
    window.addEventListener('resize', resizeHandler);
  });

  onMounted(() => {
    const mobile = _isMobile();
    isMobile.value = mobile;
  });

  onBeforeUnmount(() => {
    window.removeEventListener('resize', resizeHandler);
  });

  return {
    isMobile
  };
}
