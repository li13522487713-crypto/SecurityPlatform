import { readFile, writeFile } from "node:fs/promises";
import { resolve } from "node:path";

const targetFile = resolve(process.cwd(), "src/types/api-generated.ts");
const source = await readFile(targetFile, "utf8");

const successJsonPattern =
  /return response\.text\(\)\.then\(\(_responseText\) => \{\r?\n\s*let (result\d+): any = null;\r?\n\s*\1 = _responseText === "" \? null : JSON\.parse\(_responseText, this\.jsonParseReviver\) as ([^;]+);\r?\n\s*return \1;\r?\n\s*\}\);/g;

let transformed = source.replace(
  successJsonPattern,
  "return readJsonBody<$2>(response, this.jsonParseReviver);"
);

const helperName = "function readJsonBody<T>(";
if (!transformed.includes(helperName)) {
  const helperBlock = `
function reviveJsonTree(value: unknown, reviver: (key: string, value: unknown) => unknown, key = ""): unknown {
    if (Array.isArray(value)) {
        const revivedArray = value.map((item, index) => reviveJsonTree(item, reviver, String(index)));
        return reviver(key, revivedArray);
    }

    if (value !== null && typeof value === "object") {
        const revivedObject: { [key: string]: unknown } = {};
        for (const [childKey, childValue] of Object.entries(value as { [key: string]: unknown })) {
            revivedObject[childKey] = reviveJsonTree(childValue, reviver, childKey);
        }
        return reviver(key, revivedObject);
    }

    return reviver(key, value);
}

async function readJsonBody<T>(response: Response, reviver: ((key: string, value: any) => any) | undefined): Promise<T> {
    if (response.status === 204) {
        return null as any;
    }

    const contentLength = response.headers?.get("content-length");
    if (contentLength === "0") {
        return null as any;
    }

    const contentType = response.headers?.get("content-type") ?? "";
    if (contentType.includes("application/json") || contentType.includes("+json")) {
        const json = await response.json();
        if (!reviver) {
            return (json ?? null) as T;
        }

        return reviveJsonTree(json, reviver as (key: string, value: unknown) => unknown) as T;
    }

    const text = await response.text();
    if (text === "") {
        return null as any;
    }

    return JSON.parse(text, reviver) as T;
}
`;

  const marker = "export class ApiException extends Error {";
  const markerIndex = transformed.indexOf(marker);
  if (markerIndex < 0) {
    throw new Error("无法在 api-generated.ts 中找到 ApiException 标记，无法插入 readJsonBody helper。");
  }
  transformed = `${transformed.slice(0, markerIndex)}${helperBlock}\n${transformed.slice(markerIndex)}`;
}

if (transformed !== source) {
  await writeFile(targetFile, transformed, "utf8");
}
