const { test, expect } = require('@playwright/test');

test('recruiter can understand, inspect, and play the model', async ({ page }) => {
  await page.goto('http://127.0.0.1:5098/');
  await expect(page.getByRole('heading', { name: /aether loom/i })).toBeVisible({ timeout: 20000 });
  await expect(page.getByText('AN ORIGINAL 243-WAYS GAME')).toBeVisible();

  await page.getByRole('button', { name: /pays & rules/i }).click();
  await expect(page.getByRole('heading', { name: /pays & feature/i })).toBeVisible();
  await expect(page.locator('.pays-modal tbody tr')).toHaveCount(6);
  await page.getByRole('button', { name: /close pays and rules/i }).click();

  await page.getByRole('button', { name: 'Analysis' }).click();
  await expect(page.getByText('96.566915%')).toBeVisible();
  await expect(page.getByText('BASE_REST').first()).toBeVisible();
  await page.getByRole('button', { name: 'Cabinet' }).click();

  const spin = page.getByRole('button', { name: /spin/i });
  await spin.click();
  await expect(spin).toBeEnabled({ timeout: 20000 });
  await expect(page.locator('.win-readout strong')).toContainText('credits');
  await page.getByRole('button', { name: 'Analysis' }).click();
  await expect(page.locator('.live-state dd').nth(1)).not.toHaveText('—');
});
