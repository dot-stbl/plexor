import { chromium } from 'playwright';
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1280, height: 1200 } });
await page.goto('http://localhost:4174/components', { waitUntil: 'networkidle' });
await page.waitForTimeout(500);

// Open the FIRST select (simple: Theme with Light/Dark/System)
const selects = await page.locator('[data-slot="select-trigger"]').all();
console.log('selects:', selects.length);
await selects[0].click();
await page.waitForTimeout(500);

const popup = page.locator('[data-slot="select-content"]').first();
const popupBox = await popup.boundingBox();
console.log('popup:', popupBox);
await popup.screenshot({ path: 'C:/tmp/popup-simple.png' });

// Also hover item "Dark" (index 1) and screenshot
const items = await page.locator('[data-slot="select-item"]').all();
console.log('items:', items.length);
await items[0].hover();
await page.waitForTimeout(200);
await popup.screenshot({ path: 'C:/tmp/popup-simple-hover.png' });

// Now open grouped (select 1)
await page.keyboard.press('Escape');
await page.waitForTimeout(300);
await selects[1].click();
await page.waitForTimeout(500);
const popup2 = page.locator('[data-slot="select-content"]').first();
const popup2Box = await popup2.boundingBox();
console.log('popup2:', popup2Box);
await popup2.screenshot({ path: 'C:/tmp/popup-grouped.png' });

const items2 = await page.locator('[data-slot="select-item"]').all();
console.log('items2:', items2.length);
if (items2.length > 0) {
  await items2[0].hover();
  await page.waitForTimeout(200);
  await popup2.screenshot({ path: 'C:/tmp/popup-grouped-hover.png' });
}

await browser.close();
