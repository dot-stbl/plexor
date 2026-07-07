import { chromium } from 'playwright';
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1280, height: 1200 } });
await page.goto('http://localhost:4174/components', { waitUntil: 'networkidle' });
await page.waitForTimeout(500);

const trigger = page.locator('[data-slot="select-trigger"]').first();
const html = await trigger.evaluate((el) => {
  return {
    outer: el.outerHTML.substring(0, 800),
    width: el.getBoundingClientRect().width,
  };
});
console.log('Trigger HTML:');
console.log(html.outer);
console.log('Width:', html.width);

// Check computed CSS for trigger with details
const css = await trigger.evaluate((el) => {
  const cs = window.getComputedStyle(el);
  const valueEl = el.querySelector('[data-slot="select-value"]');
  const iconEl = el.querySelector('svg');
  const valueCS = valueEl ? window.getComputedStyle(valueEl) : null;
  const iconCS = iconEl ? window.getComputedStyle(iconEl) : null;
  return {
    trigger: {
      width: cs.width,
      display: cs.display,
      minWidth: cs.minWidth,
      padding: `${cs.paddingLeft} ${cs.paddingRight}`,
      gap: cs.gap,
    },
    value: valueEl ? {
      text: valueEl.textContent,
      width: valueCS.width,
      display: valueCS.display,
      minWidth: valueCS.minWidth,
    } : null,
    icon: iconEl ? {
      width: iconCS.width,
      height: iconCS.height,
      display: iconCS.display,
      class: iconEl.getAttribute('class'),
    } : null,
  };
});
console.log('--- CSS ---');
console.log(JSON.stringify(css, null, 2));

await browser.close();
