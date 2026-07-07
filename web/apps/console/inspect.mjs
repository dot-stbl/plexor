import { chromium } from 'playwright';
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1280, height: 1200 } });
await page.goto('http://localhost:4174/components', { waitUntil: 'networkidle' });
await page.waitForTimeout(500);

const trigger = page.locator('[data-slot="select-trigger"]').first();
const info = await trigger.evaluate((el) => {
  const cs = window.getComputedStyle(el);
  return {
    width: cs.width,
    height: cs.height,
    display: cs.display,
    minWidth: cs.minWidth,
    maxWidth: cs.maxWidth,
    padding: `${cs.paddingLeft} ${cs.paddingRight}`,
    gap: cs.gap,
    fontSize: cs.fontSize,
    computedBox: el.getBoundingClientRect(),
  };
});
console.log('trigger:', JSON.stringify(info, null, 2));

// Check parent
const parentInfo = await trigger.evaluate((el) => {
  const parent = el.parentElement;
  if (!parent) return null;
  const cs = window.getComputedStyle(parent);
  return {
    tag: parent.tagName,
    class: parent.className,
    display: cs.display,
    width: cs.width,
    gap: cs.gap,
  };
});
console.log('parent:', JSON.stringify(parentInfo, null, 2));

// Grandparent
const grandInfo = await trigger.evaluate((el) => {
  const parent = el.parentElement?.parentElement;
  if (!parent) return null;
  const cs = window.getComputedStyle(parent);
  return {
    tag: parent.tagName,
    class: parent.className,
    display: cs.display,
    width: cs.width,
  };
});
console.log('grandparent:', JSON.stringify(grandInfo, null, 2));

await browser.close();
