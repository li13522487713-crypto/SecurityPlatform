<template>
  <!-- eslint-disable-next-line vue/no-v-html -- rendered is sanitized by DOMPurify -->
  <div class="markdown-body" v-html="rendered" />
</template>

<script setup lang="ts">
import { computed, watch, ref, onMounted } from "vue";
import DOMPurify from "dompurify";
import { marked } from "marked";
import hljs from "highlight.js";
import "highlight.js/styles/github.css";

const props = defineProps<{ content: string }>();

const rendered = ref("");

// Configure marked to use highlight.js
marked.setOptions({
  highlight: function (code, lang) {
    if (lang && hljs.getLanguage(lang)) {
      try {
        return hljs.highlight(code, { language: lang }).value;
      } catch (e) {
        console.error(e);
      }
    }
    return code; // use external default escaping
  },
  breaks: true,
  gfm: true
});

const MARKDOWN_ALLOWED_TAGS = [
  "p", "h1", "h2", "h3", "h4", "h5", "h6", 
  "strong", "em", "code", "pre", "ul", "ol", "li", 
  "blockquote", "hr", "a", "br", "span", "div", 
  "table", "thead", "tbody", "tr", "th", "td"
];
const MARKDOWN_ALLOWED_ATTR = ["href", "target", "rel", "class", "style"];
const SAFE_URI_REGEXP = /^(?:(?:https?|mailto|tel):|[/?#]|\.{1,2}\/)/i;

async function renderMarkdown(md: string) {
  if (!md) {
    rendered.value = "";
    return;
  }
  
  try {
    // Parse markdown to HTML
    const rawHtml = await marked.parse(md);
    
    // Sanitize the HTML
    rendered.value = DOMPurify.sanitize(rawHtml, {
      ALLOWED_TAGS: MARKDOWN_ALLOWED_TAGS,
      ALLOWED_ATTR: MARKDOWN_ALLOWED_ATTR,
      ALLOWED_URI_REGEXP: SAFE_URI_REGEXP
    });
  } catch (e) {
    console.error("Error rendering markdown:", e);
    rendered.value = escapeHtml(md);
  }
}

function escapeHtml(text: string): string {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

watch(() => props.content, (newContent) => {
  renderMarkdown(newContent);
}, { immediate: true });

onMounted(() => {
  if (!rendered.value && props.content) {
    renderMarkdown(props.content);
  }
});
</script>

<style scoped>
.markdown-body {
  line-height: 1.6;
  word-break: break-word;
}

.markdown-body :deep(pre) {
  background: #f5f5f5;
  border-radius: 6px;
  padding: 12px;
  overflow-x: auto;
  margin: 12px 0;
}

.markdown-body :deep(code) {
  background: rgba(0, 0, 0, 0.06);
  padding: 2px 4px;
  border-radius: 3px;
  font-family: "Courier New", Courier, monospace;
  font-size: 0.9em;
}

.markdown-body :deep(pre code) {
  background: transparent;
  padding: 0;
  border-radius: 0;
}

.markdown-body :deep(h1),
.markdown-body :deep(h2),
.markdown-body :deep(h3),
.markdown-body :deep(h4),
.markdown-body :deep(h5) {
  margin-top: 16px;
  margin-bottom: 8px;
  font-weight: 600;
}

.markdown-body :deep(h1) { font-size: 1.5em; }
.markdown-body :deep(h2) { font-size: 1.3em; }
.markdown-body :deep(h3) { font-size: 1.1em; }

.markdown-body :deep(blockquote) {
  border-left: 4px solid #d9d9d9;
  margin: 12px 0;
  padding-left: 12px;
  color: rgba(0, 0, 0, 0.55);
}

.markdown-body :deep(ul),
.markdown-body :deep(ol) {
  padding-left: 24px;
  margin: 8px 0;
}

.markdown-body :deep(li) {
  margin-bottom: 4px;
}

.markdown-body :deep(p) {
  margin: 8px 0;
}

.markdown-body :deep(a) {
  color: #1677ff;
  text-decoration: none;
}

.markdown-body :deep(a:hover) {
  text-decoration: underline;
}

.markdown-body :deep(hr) {
  border: none;
  border-top: 1px solid #e8e8e8;
  margin: 16px 0;
}

.markdown-body :deep(table) {
  border-collapse: collapse;
  width: 100%;
  margin: 12px 0;
}

.markdown-body :deep(th),
.markdown-body :deep(td) {
  border: 1px solid #e8e8e8;
  padding: 8px 12px;
  text-align: left;
}

.markdown-body :deep(th) {
  background: #fafafa;
  font-weight: 600;
}
</style>
