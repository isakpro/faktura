import { defineConfig } from "@playwright/test";

// E2E körs mot compose-stacken: `docker compose up -d --build` i repo-roten först.
export default defineConfig({
  testDir: "./e2e",
  timeout: 30_000,
  retries: process.env.CI ? 1 : 0,
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:8081",
    screenshot: "only-on-failure",
    trace: "retain-on-failure",
  },
  reporter: process.env.CI ? [["list"], ["html", { open: "never" }]] : "list",
});
