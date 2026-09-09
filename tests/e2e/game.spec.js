const { test, expect } = require('@playwright/test');

test('recruiter can understand, inspect, and play the model', async ({ page }) => {
  await page.goto('http://127.0.0.1:5098/');
  await expect(page.getByRole('heading', { name: /weave the unstable/i })).toBeVisible({ timeout: 20000 });
  await expect(page.getByText('5 × 3 · 243 ways · physical reel strips')).toBeVisible();

  await page.getByRole('button', { name: /^∑ math inspector$/i }).click();
  await expect(page.getByText('96.5669%')).toBeVisible();
  await expect(page.getByText('BASE_REST')).toBeVisible();
  await page.getByRole('button', { name: /close math inspector/i }).click();

  const spin = page.getByRole('button', { name: /spin/i });
  await spin.click();
  await expect(spin).toBeEnabled({ timeout: 20000 });
  await expect(page.locator('.win-readout strong')).toContainText('credits');
  await page.getByRole('button', { name: /^∑ math inspector$/i }).click();
  await expect(page.locator('.math-panel dd.mono')).not.toHaveText('—');
});
