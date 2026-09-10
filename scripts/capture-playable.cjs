const { chromium } = require('@playwright/test');
const fs = require('node:fs');

(async () => {
  const browser = await chromium.launch();
  const base = process.env.GAME_URL || 'http://127.0.0.1:5098/';
  fs.mkdirSync('docs/images', { recursive: true });
  for (const [name, width, height] of [['gameplay', 1440, 1100], ['mobile', 390, 844]]) {
    const page = await browser.newPage({ viewport: { width, height } });
    await page.goto(base);
    await page.getByRole('heading', { name: /aether loom/i }).waitFor();
    await page.evaluate(() => document.fonts.ready);
    await page.waitForFunction(() => performance.getEntriesByType('resource').some(r => r.name.includes('symbols-atlas')));
    await page.waitForTimeout(300);
    await page.screenshot({ path: `docs/images/${name}.png`, fullPage: true });
    if (name === 'gameplay') {
      await page.getByRole('button', { name: 'Analysis', exact: true }).click();
      await page.screenshot({ path: 'docs/images/analysis.png', fullPage: true });
      await page.getByRole('button', { name: 'Cabinet', exact: true }).click();
      await page.getByRole('button', { name: 'Preview feature', exact: true }).click();
      await page.locator('.feature-banner').filter({ hasText: 'Free spin 1' }).waitFor();
      await page.waitForFunction(() => document.querySelectorAll('.reel.rolling').length === 0);
      await page.screenshot({ path: 'docs/images/feature.png', fullPage: true });
    }
    await page.close();
  }
  await browser.close();
  console.log('Captured cabinet, analysis, feature, and mobile views.');
})().catch(error => { console.error(error); process.exit(1); });
