import type { Locator, Page } from "@playwright/test";
import { expect } from "@playwright/test";

// ---------------------------------------------------------------------------
// Global mouse position tracking (shared across all pages)
// ---------------------------------------------------------------------------

const mousePositionMap = new WeakMap<Page, { x: number; y: number }>();

export function getMousePosition(page: Page): { x: number; y: number } | undefined {
  return mousePositionMap.get(page);
}

export function setMousePosition(page: Page, pos: { x: number; y: number }) {
  mousePositionMap.set(page, pos);
}

// ---------------------------------------------------------------------------
// Math helpers
// ---------------------------------------------------------------------------

export function randomBetween(min: number, max: number): number {
  return min + Math.random() * (max - min);
}

export function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

/**
 * Log-normal-ish random — useful for human reaction/press durations where
 * the distribution is right-skewed (most values near median, occasional long tail).
 */
function logNormalRandom(median: number, sigma = 0.35): number {
  const u1 = Math.random() || 1e-10;
  const u2 = Math.random();
  const z = Math.sqrt(-2 * Math.log(u1)) * Math.cos(2 * Math.PI * u2);
  return median * Math.exp(sigma * z);
}

// ---------------------------------------------------------------------------
// Cubic Bezier path generation
// ---------------------------------------------------------------------------

interface Point {
  x: number;
  y: number;
}

function cubicBezier(p0: Point, p1: Point, p2: Point, p3: Point, t: number): Point {
  const u = 1 - t;
  const tt = t * t;
  const uu = u * u;
  const uuu = uu * u;
  const ttt = tt * t;
  return {
    x: uuu * p0.x + 3 * uu * t * p1.x + 3 * u * tt * p2.x + ttt * p3.x,
    y: uuu * p0.y + 3 * uu * t * p1.y + 3 * u * tt * p2.y + ttt * p3.y
  };
}

/**
 * Generate two random control points for a cubic Bezier that produces a
 * natural hand-arc from `start` to `end`. The control points are offset
 * perpendicular to the movement vector — mimicking wrist rotation.
 */
function randomControlPoints(start: Point, end: Point): [Point, Point] {
  const dx = end.x - start.x;
  const dy = end.y - start.y;
  const distance = Math.hypot(dx, dy);

  const nx = -dy / (distance || 1);
  const ny = dx / (distance || 1);

  const spread = clamp(distance * 0.15, 8, 80);
  const offset1 = randomBetween(-spread, spread);
  const offset2 = randomBetween(-spread, spread);

  const cp1: Point = {
    x: start.x + dx * randomBetween(0.2, 0.4) + nx * offset1,
    y: start.y + dy * randomBetween(0.2, 0.4) + ny * offset1
  };
  const cp2: Point = {
    x: start.x + dx * randomBetween(0.6, 0.8) + nx * offset2,
    y: start.y + dy * randomBetween(0.6, 0.8) + ny * offset2
  };

  return [cp1, cp2];
}

// ---------------------------------------------------------------------------
// Easing: 5-phase human kinematics
//   0-15%  slow start     (muscle inertia)
//  15-50%  acceleration
//  50-75%  cruise
//  75-95%  deceleration   (approaching target)
//  95-100% fine approach   (precision zone)
// ---------------------------------------------------------------------------

function humanEasing(t: number): number {
  if (t <= 0) return 0;
  if (t >= 1) return 1;

  if (t < 0.15) {
    const p = t / 0.15;
    return 0.15 * (p * p * p);
  }
  if (t < 0.50) {
    const p = (t - 0.15) / 0.35;
    return 0.15 * 1 + 0.35 * (3 * p * p - 2 * p * p * p);
  }
  if (t < 0.75) {
    const p = (t - 0.50) / 0.25;
    return 0.50 + 0.25 * p;
  }
  if (t < 0.95) {
    const p = (t - 0.75) / 0.20;
    return 0.75 + 0.20 * (p * (2 - p));
  }
  const p = (t - 0.95) / 0.05;
  return 0.95 + 0.05 * (p * p);
}

// ---------------------------------------------------------------------------
// Fitts's law adaptive step calculation
// ---------------------------------------------------------------------------

interface MoveOptions {
  /** Target element width for Fitts calculation (default: 40) */
  targetWidth?: number;
  /** Minimum number of animation steps */
  minSteps?: number;
  /** Maximum number of animation steps */
  maxSteps?: number;
}

function fittsSteps(distance: number, targetWidth = 40, minSteps = 12, maxSteps = 48): number {
  if (distance < 5) return minSteps;
  const id = Math.log2(distance / Math.max(targetWidth, 4) + 1);
  return clamp(Math.round(8 + id * 7), minSteps, maxSteps);
}

function fittsStepDelay(distance: number, targetWidth = 40): { base: number; jitter: number } {
  const id = Math.log2(distance / Math.max(targetWidth, 4) + 1);
  const base = clamp(6 + id * 3, 6, 24);
  const jitter = clamp(4 + id * 2, 3, 14);
  return { base, jitter };
}

// ---------------------------------------------------------------------------
// Core: moveMouseHumanLike
// ---------------------------------------------------------------------------

export async function moveMouseHumanLike(
  page: Page,
  destination: Point,
  options?: MoveOptions & { from?: Point }
): Promise<void> {
  const viewport = page.viewportSize() ?? { width: 1440, height: 900 };
  const start: Point = options?.from ??
    mousePositionMap.get(page) ?? {
      x: randomBetween(24, Math.max(48, viewport.width * 0.18)),
      y: randomBetween(24, Math.max(48, viewport.height * 0.16))
    };

  const distance = Math.hypot(destination.x - start.x, destination.y - start.y);

  if (distance < 3) {
    await page.mouse.move(destination.x, destination.y, { steps: 1 });
    mousePositionMap.set(page, destination);
    return;
  }

  const targetWidth = options?.targetWidth ?? 40;
  const steps = fittsSteps(distance, targetWidth, options?.minSteps, options?.maxSteps);
  const { base: delayBase, jitter: delayJitter } = fittsStepDelay(distance, targetWidth);

  const [cp1, cp2] = randomControlPoints(start, destination);

  for (let step = 1; step <= steps; step += 1) {
    const rawT = step / steps;
    const easedT = humanEasing(rawT);

    const point = cubicBezier(start, cp1, cp2, destination, easedT);

    const normalAngle = Math.atan2(destination.y - start.y, destination.x - start.x) + Math.PI / 2;
    const jitterMagnitude = Math.max(0.3, (1 - rawT) * 3.5) * randomBetween(-1, 1);
    point.x += Math.cos(normalAngle) * jitterMagnitude;
    point.y += Math.sin(normalAngle) * jitterMagnitude;

    await page.mouse.move(point.x, point.y, { steps: 1 });
    await page.waitForTimeout(delayBase + randomBetween(0, delayJitter));
  }

  // Overshoot & micro-correction for long distances
  if (distance > 300) {
    const overshootMag = randomBetween(3, 8);
    const angle = Math.atan2(destination.y - start.y, destination.x - start.x);
    const overshoot: Point = {
      x: destination.x + Math.cos(angle) * overshootMag,
      y: destination.y + Math.sin(angle) * overshootMag
    };
    await page.mouse.move(overshoot.x, overshoot.y, { steps: 1 });
    await page.waitForTimeout(randomBetween(18, 36));

    const correctionSteps = Math.round(randomBetween(3, 5));
    for (let i = 1; i <= correctionSteps; i += 1) {
      const t = i / correctionSteps;
      await page.mouse.move(
        overshoot.x + (destination.x - overshoot.x) * t,
        overshoot.y + (destination.y - overshoot.y) * t,
        { steps: 1 }
      );
      await page.waitForTimeout(randomBetween(12, 28));
    }
  }

  await page.mouse.move(destination.x, destination.y, { steps: 1 });
  mousePositionMap.set(page, destination);
}

// ---------------------------------------------------------------------------
// Drag: human-like drag from point A to point B
// ---------------------------------------------------------------------------

export interface DragOptions {
  /** Steps hint for the main drag segment */
  stepsHint?: number;
  /** Delay after mousedown before moving (grip time) */
  gripDelay?: { min: number; max: number };
  /** Whether to add a hesitation pause near the target */
  hesitateNearTarget?: boolean;
}

export async function humanDrag(
  page: Page,
  from: Point,
  to: Point,
  options?: DragOptions
): Promise<void> {
  const gripMin = options?.gripDelay?.min ?? 60;
  const gripMax = options?.gripDelay?.max ?? 120;
  const hesitate = options?.hesitateNearTarget ?? true;

  await moveMouseHumanLike(page, from, { targetWidth: 24 });
  await page.waitForTimeout(randomBetween(20, 50));
  await page.mouse.down();
  await page.waitForTimeout(randomBetween(gripMin, gripMax));

  const distance = Math.hypot(to.x - from.x, to.y - from.y);
  const mainSteps = clamp(
    options?.stepsHint ?? Math.round(distance / 18),
    14, 40
  );

  const [cp1, cp2] = randomControlPoints(from, to);

  const hesitateAt = hesitate ? randomBetween(0.78, 0.88) : 1.1;

  for (let step = 1; step <= mainSteps; step += 1) {
    const rawT = step / mainSteps;
    const easedT = humanEasing(rawT);
    const point = cubicBezier(from, cp1, cp2, to, easedT);

    const normalAngle = Math.atan2(to.y - from.y, to.x - from.x) + Math.PI / 2;
    const jitter = Math.max(0.5, (1 - rawT) * 2.5) * randomBetween(-1, 1);
    point.x += Math.cos(normalAngle) * jitter;
    point.y += Math.sin(normalAngle) * jitter;

    await page.mouse.move(point.x, point.y, { steps: 1 });
    await page.waitForTimeout(randomBetween(8, 18));

    if (rawT >= hesitateAt && rawT < hesitateAt + 1 / mainSteps + 0.01) {
      await page.waitForTimeout(randomBetween(50, 80));
    }
  }

  await page.mouse.move(to.x, to.y, { steps: 1 });
  await page.waitForTimeout(randomBetween(24, 52));
  await page.mouse.up();
  mousePositionMap.set(page, to);
}

// ---------------------------------------------------------------------------
// Resolve locator center point with scroll & visibility
// ---------------------------------------------------------------------------

export async function resolveLocatorPoint(
  page: Page,
  locator: Locator,
  position?: Point
): Promise<Point> {
  await expect(locator).toBeVisible({ timeout: 15_000 });
  await locator.scrollIntoViewIfNeeded();
  const box = await locator.boundingBox();
  expect(box).toBeTruthy();
  if (!box) {
    throw new Error("目标元素未能解析出可交互区域。");
  }

  const x = clamp(
    box.x + (position?.x ?? box.width / 2),
    box.x + 1,
    box.x + Math.max(1, box.width - 1)
  );
  const y = clamp(
    box.y + (position?.y ?? box.height / 2),
    box.y + 1,
    box.y + Math.max(1, box.height - 1)
  );

  return { x, y };
}

// ---------------------------------------------------------------------------
// Compute a randomized click position inside a bounding box
// ---------------------------------------------------------------------------

export function randomClickTarget(box: { x: number; y: number; width: number; height: number }, position?: Point): Point {
  const hPad = Math.min(18, Math.max(6, box.width * 0.18));
  const vPad = Math.min(16, Math.max(6, box.height * 0.18));
  return {
    x: clamp(
      box.x + (position?.x ?? randomBetween(hPad, Math.max(hPad, box.width - hPad))),
      box.x + 1,
      box.x + Math.max(1, box.width - 1)
    ),
    y: clamp(
      box.y + (position?.y ?? randomBetween(vPad, Math.max(vPad, box.height - vPad))),
      box.y + 1,
      box.y + Math.max(1, box.height - 1)
    )
  };
}

// ---------------------------------------------------------------------------
// Human timing helpers
// ---------------------------------------------------------------------------

/** "Gaze" delay — simulates visual confirmation before clicking */
export function gazeDelay(): number {
  return logNormalRandom(70, 0.4);
}

/** Mouse-down hold duration — log-normal, occasionally "hesitant" long press */
export function pressHoldDuration(): number {
  return clamp(logNormalRandom(65, 0.35), 28, 180);
}

/** Thinking pause between sequential UI actions */
export function thinkingPause(): number {
  return randomBetween(80, 250);
}

/** Short gaze-shift delay between form fields */
export function gazeShiftDelay(): number {
  return randomBetween(50, 150);
}

/** Per-character typing delay (common chars fast, special chars slow) */
export function typingCharDelay(char: string): number {
  if (/[a-zA-Z0-9 ]/.test(char)) {
    return randomBetween(28, 72);
  }
  if (/[!@#$%^&*()_+\-=[\]{};':"\\|,.<>/?]/.test(char)) {
    return randomBetween(65, 140);
  }
  return randomBetween(40, 90);
}
