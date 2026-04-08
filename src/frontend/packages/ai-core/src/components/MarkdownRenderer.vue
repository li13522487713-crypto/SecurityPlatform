<template>
  <div class="markdown-renderer">
    <VueMarkdownStream :content="displayedContent" :md="markdownIt" />
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from "vue";
import MarkdownIt from "markdown-it";
import hljs from "highlight.js";
import VueMarkdownStream, { useTypewriter } from "vue-markdown-stream";
import "vue-markdown-stream/dist/index.css";

const DEFAULT_TYPING_INTERVAL_MS = 32;
const DEFAULT_TYPING_RANGE: [number, number] = [1, 1];

interface MarkdownToken {
  attrGet(name: string): string | null;
  attrSet(name: string, value: string): void;
}

interface MarkdownRendererLike {
  renderToken(tokens: MarkdownToken[], index: number, options: object): string;
}

type LinkOpenRule = (
  tokens: MarkdownToken[],
  index: number,
  options: object,
  env: object,
  self: MarkdownRendererLike
) => string;

const props = withDefaults(defineProps<{
  content: string;
  streaming?: boolean;
}>(), {
  streaming: false
});

const displayedContent = ref(props.streaming ? "" : props.content);
const { startTyping } = useTypewriter();
const markdownIt = createMarkdownIt();

let stopTyping: (() => void) | null = null;
let pendingDelta = "";
let targetContent = props.streaming ? "" : props.content;

watch(
  () => [props.content, props.streaming] as const,
  ([content, streaming]) => {
    syncDisplayedContent(content, streaming);
  },
  { immediate: true }
);

onBeforeUnmount(() => {
  stopCurrentTyping();
});

function syncDisplayedContent(content: string, streaming: boolean) {
  const nextContent = content ?? "";

  if (!streaming && !hasTypingWork()) {
    replaceDisplayedContent(nextContent);
    return;
  }

  if (nextContent === targetContent) {
    return;
  }

  if (!nextContent.startsWith(targetContent)) {
    replaceDisplayedContent(nextContent);
    return;
  }

  const delta = nextContent.slice(targetContent.length);
  targetContent = nextContent;

  if (!delta) {
    return;
  }

  pendingDelta += delta;
  ensureTyping();
}

function ensureTyping() {
  if (stopTyping || pendingDelta.length === 0) {
    return;
  }

  const currentDelta = pendingDelta;
  pendingDelta = "";

  stopTyping = startTyping(
    currentDelta,
    DEFAULT_TYPING_INTERVAL_MS,
    DEFAULT_TYPING_RANGE,
    (segment) => {
      displayedContent.value += segment;
    },
    () => {
      stopTyping = null;
      if (pendingDelta.length > 0) {
        ensureTyping();
      }
    }
  );
}

function replaceDisplayedContent(content: string) {
  stopCurrentTyping();
  pendingDelta = "";
  targetContent = content;
  displayedContent.value = content;
}

function stopCurrentTyping() {
  if (stopTyping) {
    stopTyping();
    stopTyping = null;
  }
}

function hasTypingWork() {
  return stopTyping !== null || pendingDelta.length > 0;
}

function createMarkdownIt() {
  const instance = new MarkdownIt({
    html: false,
    linkify: true,
    breaks: true,
    highlight(code: string, lang: string) {
      return renderHighlightedCode(code, lang);
    }
  });

  const defaultLinkOpenRule = instance.renderer.rules.link_open as LinkOpenRule | undefined;

  instance.renderer.rules.link_open = (
    tokens: MarkdownToken[],
    index: number,
    options: object,
    env: object,
    self: MarkdownRendererLike
  ) => {
    const href = tokens[index].attrGet("href") ?? "";
    if (/^(https?:)?\/\//i.test(href) || href.startsWith("mailto:") || href.startsWith("tel:")) {
      tokens[index].attrSet("target", "_blank");
      tokens[index].attrSet("rel", "noopener noreferrer");
    }

    return defaultLinkOpenRule
      ? defaultLinkOpenRule(tokens, index, options, env, self)
      : self.renderToken(tokens, index, options);
  };

  return instance;
}

function renderHighlightedCode(code: string, lang: string): string {
  try {
    const highlighted = lang && hljs.getLanguage(lang)
      ? hljs.highlight(code, { language: lang }).value
      : hljs.highlightAuto(code).value;
    const languageClass = lang ? ` language-${lang}` : "";
    return `<pre class="hljs"><code class="hljs${languageClass}">${highlighted}</code></pre>`;
  } catch {
    return `<pre class="hljs"><code>${escapeHtml(code)}</code></pre>`;
  }
}

function escapeHtml(code: string): string {
  return code
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll("\"", "&quot;")
    .replaceAll("'", "&#39;");
}
</script>

<style scoped>
.markdown-renderer {
  min-width: 0;
}

.markdown-renderer :deep(.markdown-body) {
  width: 100%;
  height: auto;
  overflow: visible;
  background: transparent;
  font-size: inherit;
  line-height: inherit;
  word-break: break-word;
}

.markdown-renderer :deep(.markdown-body pre) {
  margin: 12px 0;
}
</style>
