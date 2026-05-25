const { test, expect } = require("@playwright/test");

const smokePassword = "Password01!*";

function createSmokeEmail() {
  return `smoke-${Date.now()}-${Math.floor(Math.random() * 10_000)}@example.com`;
}

async function signIn(page) {
  const email = createSmokeEmail();

  await page.goto("/auth");
  await page.getByLabel("Email address").fill(email);
  await page.getByRole("button", { name: "Find my account" }).click();

  await expect(page).toHaveURL(/\/register/);
  await page.getByLabel("First name").fill("Smoke");
  await page.getByLabel("Last name").fill("Tester");
  await page.getByLabel("Password").fill(smokePassword);
  await page.getByLabel("I agree to the Terms and Privacy Policy").check();
  await page.getByRole("button", { name: "Create account" }).click();
  await expect(page).toHaveURL(/\/$/);
}

test("auth email-first register signs in", async ({ page }) => {
  await signIn(page);

  await expect(page.getByRole("heading", { level: 1, name: "Dashboard" })).toBeVisible();
});

test("desktop compact toggle and flyout submenu work", async ({ page }) => {
  await signIn(page);

  const compactToggle = page.locator("[data-sidebar-compact-toggle]");
  await compactToggle.click();
  await expect(page.locator("[data-app-frame]")).toHaveAttribute("data-sidebar-compact", "true");

  const reportsTrigger = page.locator("[data-menu-group][data-menu-key='reports'] [data-menu-trigger]");
  await reportsTrigger.click();

  const flyout = page.locator("[data-sidebar-flyout]");
  await expect(flyout).toBeVisible();
  await expect(flyout.getByRole("menuitem", { name: "Operational Health" })).toBeVisible();

  await page.keyboard.press("Escape");
  await expect(flyout).toBeHidden();
});

test("mobile nav opens and closes", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await signIn(page);

  const frame = page.locator("[data-app-frame]");
  const openTrigger = page.locator("[data-sidebar-open-trigger]");
  const overlay = page.locator("[data-sidebar-close-trigger].app-sidebar-overlay");

  await openTrigger.click();
  await expect(frame).toHaveAttribute("data-sidebar-open", "true");

  await overlay.click();
  await expect(frame).toHaveAttribute("data-sidebar-open", "false");
});
