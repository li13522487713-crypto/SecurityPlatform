import tinymce from "tinymce/tinymce";

let tinyMcePromise: Promise<typeof tinymce> | null = null;

export async function ensureTinyMce(): Promise<typeof tinymce> {
  if (tinyMcePromise) {
    return tinyMcePromise;
  }

  tinyMcePromise = (async () => {
    const globalTinyMce = (window as Window & { tinymce?: typeof tinymce }).tinymce ?? tinymce;
    globalTinyMce.baseURL = "/tinymce";
    globalTinyMce.suffix = ".min";
    (window as Window & { tinymce?: typeof tinymce }).tinymce = globalTinyMce;
    return globalTinyMce;
  })();

  return tinyMcePromise;
}
