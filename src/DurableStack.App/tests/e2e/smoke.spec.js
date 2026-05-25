const { test, expect } = require("@playwright/test");

test.setTimeout(60_000);

const smokePassword = "Password01!*";

function createSmokeEmail() {
  return `smoke-${Date.now()}-${Math.floor(Math.random() * 10_000)}@example.com`;
}

async function signIn(page) {
  const email = createSmokeEmail();
  const organizationName = `Smoke Org ${Date.now()}`;
  const projectName = `Smoke Project ${Date.now()}`;

  await page.goto("/auth");
  await page.getByLabel("Email address").fill(email);
  await page.getByRole("button", { name: "Find my account" }).click();

  await expect(page).toHaveURL(/\/register/);
  await page.getByLabel("First name").fill("Smoke");
  await page.getByLabel("Last name").fill("Tester");
  await page.getByLabel("Password").fill(smokePassword);
  await page.getByLabel("I agree to the Terms and Privacy Policy").check();
  await page.getByRole("button", { name: "Create account" }).click();

  await expect(page).toHaveURL(/\/onboarding$/);
  await expect(page.getByText("Account created successfully")).toBeVisible();
  await page.getByLabel("Company name").fill(organizationName);
  await page.getByRole("button", { name: "Continue to project setup" }).click();

  await expect(page).toHaveURL(/\/onboarding\/project$/);
  await page.getByLabel("Project name").fill(projectName);
  await page.getByRole("button", { name: "Continue to tenant setup" }).click();

  await expect(page).toHaveURL(/\/onboarding\/tenant$/);
  await page.getByRole("button", { name: "Provision tenant and continue" }).click();

  await expect(page).toHaveURL(/\/onboarding\/complete$/);
  await page.getByRole("button", { name: "Copy client secret" }).click();
  await expect(page.locator("[data-copy-client-secret-status]")).toContainText(/Client secret copied|Unable to copy automatically/i);
  await page.getByLabel("I saved this client secret securely.").check();
  await page.getByRole("button", { name: "Open dashboard" }).click();
  await expect(page).toHaveURL(/\/$/);
}

async function signInPersonal(page) {
  const email = createSmokeEmail();
  const projectName = `Smoke Project ${Date.now()}`;

  await page.goto("/auth");
  await page.getByLabel("Email address").fill(email);
  await page.getByRole("button", { name: "Find my account" }).click();

  await expect(page).toHaveURL(/\/register/);
  await page.getByLabel("First name").fill("Smoke");
  await page.getByLabel("Last name").fill("Tester");
  await page.getByLabel("Password").fill(smokePassword);
  await page.getByLabel("I agree to the Terms and Privacy Policy").check();
  await page.getByRole("button", { name: "Create account" }).click();

  await expect(page).toHaveURL(/\/onboarding$/);
  await page.getByLabel("Is this registration for a company?").selectOption("false");
  await expect(page.getByLabel("Company name")).toBeHidden();
  await expect(page.locator("[data-personal-name-field] em", { hasText: "Smoke Tester" })).toBeVisible();

  await page.getByRole("button", { name: "Continue to project setup" }).click();

  await expect(page).toHaveURL(/\/onboarding\/project$/);
  await page.getByLabel("Project name").fill(projectName);
  await page.getByRole("button", { name: "Continue to tenant setup" }).click();

  await expect(page).toHaveURL(/\/onboarding\/tenant$/);
  await page.getByRole("button", { name: "Provision tenant and continue" }).click();

  await expect(page).toHaveURL(/\/onboarding\/complete$/);
  await page.getByRole("button", { name: "Copy client secret" }).click();
  await expect(page.locator("[data-copy-client-secret-status]")).toContainText(/Client secret copied|Unable to copy automatically/i);
  await page.getByLabel("I saved this client secret securely.").check();
  await page.getByRole("button", { name: "Open dashboard" }).click();
  await expect(page).toHaveURL(/\/$/);
}

test("auth email-first register signs in", async ({ page }) => {
  await signIn(page);

  await expect(page.getByRole("heading", { level: 1, name: "Dashboard" })).toBeVisible();
  await expect(page.locator("[data-toast]")).toHaveCount(1);
  await expect(page.locator("[data-toast] .app-toast-body")).toContainText("Onboarding complete");
});

test("auth onboarding personal path uses display name", async ({ page }) => {
  await signInPersonal(page);

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
