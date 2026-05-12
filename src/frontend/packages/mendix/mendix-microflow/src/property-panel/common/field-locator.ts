export function locateFieldByPath(container: HTMLElement | null, fieldPath?: string): boolean {
  if (!container || !fieldPath) {
    return false;
  }
  const candidates = Array.from(container.querySelectorAll<HTMLElement>("[data-mf-field-path]"));
  const target = candidates.find(element => element.dataset.mfFieldPath === fieldPath);
  if (!target) {
    return false;
  }
  target.scrollIntoView({ behavior: "smooth", block: "center", inline: "nearest" });
  const focusable = target.querySelector<HTMLElement>("input, textarea, select, [role='combobox'], button, [tabindex]");
  try {
    focusable?.focus();
  } catch {
    // ignore focus errors for readonly/disabled widgets
  }
  return true;
}
