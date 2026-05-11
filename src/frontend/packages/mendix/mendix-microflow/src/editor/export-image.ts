export const EXPORT_IMAGE_OPTIONS = {
  cacheBust: true,
  pixelRatio: 2,
  backgroundColor: "#ffffff",
} as const;

export function sanitizeExportFileName(name: string | undefined): string {
  const trimmed = String(name ?? "").trim();
  const base = trimmed || "microflow";
  return base.replace(/[<>:"/\\|?*\u0000-\u001F]+/g, "_");
}

export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.rel = "noopener";
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 1_000);
}

export async function exportCanvasAsPng(
  canvasElement: HTMLElement,
  microflowName: string | undefined,
): Promise<{ ok: true; fileName: string } | { ok: false; error: string }> {
  try {
    const { toBlob } = await import("html-to-image");
    const blob = await toBlob(canvasElement, EXPORT_IMAGE_OPTIONS);
    if (!blob) {
      return { ok: false, error: "PNG export returned an empty image." };
    }
    const fileName = `${sanitizeExportFileName(microflowName)}.png`;
    downloadBlob(blob, fileName);
    return { ok: true, fileName };
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    return { ok: false, error: `PNG export failed: ${message}` };
  }
}

