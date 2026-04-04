import { onUnmounted, ref } from "vue";

export function useAudioRecorder() {
  const isSupported = ref(
    typeof window !== "undefined"
    && typeof navigator !== "undefined"
    && Boolean(navigator.mediaDevices?.getUserMedia)
    && typeof MediaRecorder !== "undefined"
  );
  const isRecording = ref(false);
  const audioBlob = ref<Blob | null>(null);
  const audioUrl = ref<string>("");
  const error = ref<string | null>(null);

  let mediaRecorder: MediaRecorder | null = null;
  let mediaStream: MediaStream | null = null;
  const chunks: BlobPart[] = [];

  function resetAudio() {
    if (audioUrl.value) {
      URL.revokeObjectURL(audioUrl.value);
    }
    audioBlob.value = null;
    audioUrl.value = "";
  }

  async function startRecording() {
    if (!isSupported.value || isRecording.value) {
      return;
    }

    error.value = null;
    resetAudio();
    chunks.length = 0;

    try {
      mediaStream = await navigator.mediaDevices.getUserMedia({ audio: true });
      mediaRecorder = new MediaRecorder(mediaStream);
      mediaRecorder.ondataavailable = (evt) => {
        if (evt.data.size > 0) {
          chunks.push(evt.data);
        }
      };
      mediaRecorder.start();
      isRecording.value = true;
    } catch (err: unknown) {
      error.value = (err as Error).message || "录音启动失败";
      cleanupStream();
    }
  }

  async function stopRecording() {
    if (!mediaRecorder || !isRecording.value) {
      return;
    }

    await new Promise<void>((resolve) => {
      if (!mediaRecorder) {
        resolve();
        return;
      }
      mediaRecorder.onstop = () => resolve();
      mediaRecorder.stop();
    });

    const mimeType = mediaRecorder.mimeType || "audio/webm";
    const blob = new Blob(chunks, { type: mimeType });
    audioBlob.value = blob;
    audioUrl.value = URL.createObjectURL(blob);
    isRecording.value = false;
    cleanupStream();
  }

  function cleanupStream() {
    if (mediaStream) {
      mediaStream.getTracks().forEach((track) => track.stop());
      mediaStream = null;
    }
    mediaRecorder = null;
  }

  function clear() {
    if (isRecording.value && mediaRecorder) {
      mediaRecorder.stop();
      isRecording.value = false;
    }
    cleanupStream();
    resetAudio();
    error.value = null;
  }

  onUnmounted(() => {
    clear();
  });

  return {
    isSupported,
    isRecording,
    audioBlob,
    audioUrl,
    error,
    startRecording,
    stopRecording,
    clear
  };
}
