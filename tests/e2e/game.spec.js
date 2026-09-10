const { test, expect } = require('@playwright/test');

test('recruiter can understand, inspect, and play the model', async ({ page }) => {
  await page.goto(process.env.GAME_URL || 'http://127.0.0.1:5098/');
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

test('feature preview remains locked and preserves paid credits', async ({ page }) => {
  test.setTimeout(60000);
  await page.goto(process.env.GAME_URL || 'http://127.0.0.1:5098/');
  const preview = page.getByRole('button', { name: 'Preview feature', exact: true });
  await expect(preview).toBeVisible({ timeout: 20000 });
  await page.getByRole('button', { name: 'Quick off', exact: true }).click();
  const initialCredit = await page.locator('.credit-block strong').first().textContent();
  await preview.click();
  await expect(page.locator('.preview-label')).toContainText('NO WAGER');
  const spin = page.getByRole('button', { name: 'Spin', exact: true });
  await expect(spin).toBeDisabled();
  await page.getByRole('button', { name: 'Analysis', exact: true }).click();
  await expect(page.locator('.session-strip')).toContainText('Paid spins 0');
  await page.getByRole('button', { name: 'Cabinet', exact: true }).click();
  // Check multiple reel-stop and feature-result intervals, where the old UI unlocked.
  for (let i = 0; i < 6; i++) {
    await page.waitForTimeout(240);
    await expect(spin).toBeDisabled();
    await expect(page.getByRole('button', { name: 'Increase bet' })).toBeDisabled();
  }
  await expect(page.getByRole('button', { name: 'Replay feature preview' })).toBeEnabled({ timeout: 30000 });
  await expect(page.locator('.credit-block strong').first()).toHaveText(initialCredit);
  await expect(page.locator('.feature-banner')).toContainText('Preview complete');
  await expect(page.locator('.tension-ladder')).toHaveAttribute('aria-label', 'Tension state Rest');
  await page.getByRole('button', { name: 'Analysis', exact: true }).click();
  await expect(page.locator('.session-strip')).toContainText('Paid spins 0');
});

test('rules trap focus, Escape returns it, and Space plays one round', async ({ page }) => {
  await page.goto(process.env.GAME_URL || 'http://127.0.0.1:5098/');
  const rules = page.getByRole('button', { name: 'Pays & rules' });
  await expect(rules).toBeVisible({ timeout: 20000 });
  await rules.click();
  const close = page.getByRole('button', { name: 'Close pays and rules' });
  await expect(close).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(close).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(rules).toBeFocused();
  await page.locator('h1').click();
  await page.keyboard.press('Space');
  await expect(page.getByRole('button', { name: 'Spin', exact: true })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Spin', exact: true })).toBeEnabled({ timeout: 30000 });
  await page.getByRole('button', { name: 'Analysis', exact: true }).click();
  await expect(page.locator('.session-strip')).toContainText('Paid spins 1');
});

test('mobile cabinet and analysis fit without horizontal scrolling', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto(process.env.GAME_URL || 'http://127.0.0.1:5098/');
  await expect(page.getByRole('heading', { name: /aether loom/i })).toBeVisible({ timeout: 20000 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
  await page.getByRole('button', { name: 'Analysis', exact: true }).click();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
  await expect(page.getByText('96.566915%')).toBeVisible();
  await page.getByRole('button', { name: 'Pays & rules' }).click();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
});
