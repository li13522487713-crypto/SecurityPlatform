import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  
  page.on('console', msg => console.log('BROWSER CONSOLE:', msg.type(), msg.text()));
  page.on('pageerror', error => console.log('BROWSER PAGE ERROR:', error.message));
  page.on('requestfailed', request => console.log('BROWSER REQUEST FAILED:', request.url(), request.failure()?.errorText));

  // Try fetching license first to ensure backend is up
  try {
    const r = await page.goto('http://127.0.0.1:5002/internal/health/live');
    console.log('Health check:', r?.status());
  } catch (e) {}

  await page.goto('http://127.0.0.1:5181');
  
  // Login
  try {
    console.log('Waiting for login to finish, this may take a bit as we simulate it or we can just run the test file with DEBUG=pw:api');
    await browser.close();
  } catch (e) {
    await browser.close();
  }
})();
