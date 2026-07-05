import { expect, test } from "@playwright/test";

/**
 * Hela kärnflödet genom riktig webbläsare mot compose-stacken:
 * registrera organisation → skapa kund → skapa fakturautkast → skicka → verifiera nummer/stämpel.
 */
test("registrera → kund → faktura → skicka", async ({ page }) => {
  const unique = Date.now();

  // Registrera organisation (self-service).
  await page.goto("/signup");
  await page.getByLabel("Organisationsnamn").fill(`E2E Bolag ${unique}`);
  await page.getByLabel("E-post").fill(`e2e-${unique}@test.se`);
  await page.getByLabel(/Lösenord/).fill("password1");
  await page.getByRole("button", { name: /skapa/i }).click();

  // Inloggad översikt (Huvudboken).
  await expect(page.getByText("Utestående")).toBeVisible();

  // Skapa kund.
  await page.getByRole("link", { name: "Kunder" }).click();
  await page.getByLabel("Namn").fill("E2E Kund AB");
  await page.getByRole("button", { name: "Spara" }).click();
  await expect(page.getByText("E2E Kund AB").first()).toBeVisible();

  // Skapa fakturautkast.
  await page.getByRole("link", { name: "Fakturor" }).click();
  await page.locator("select").first().selectOption({ label: "E2E Kund AB" });
  await page.getByPlaceholder("Beskrivning").fill("Konsultarbete");
  await page.getByPlaceholder("Antal").fill("10");
  await page.getByPlaceholder("À-pris").fill("1200");
  await page.getByRole("button", { name: "Skapa utkast" }).click();
  await expect(page.getByText("UTKAST", { exact: true })).toBeVisible();

  // Skicka — fakturan får nummer och stämplas SKICKAD.
  await page.getByRole("button", { name: "Skicka", exact: true }).click();
  await expect(page.getByText("SKICKAD", { exact: true })).toBeVisible();
  await expect(page.getByRole("cell", { name: "1", exact: true })).toBeVisible();
});
