import { test, expect } from '@playwright/test';

const STORIES_URL = '**/api/stories**';

const mockResponse = {
  items: [
    { id: 1, title: 'Alpha Story', url: 'https://alpha.com', by: 'user1', time: 1700000001, score: 100 },
    { id: 2, title: 'Beta Story', url: null, by: 'user2', time: 1700000002, score: 200 },
  ],
  total: 2,
  page: 1,
  pageSize: 20
};

test.beforeEach(async ({ page }) => {
  await page.route(STORIES_URL, route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockResponse) })
  );
});

test('shows page heading', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByText('Hacker News')).toBeVisible();
  await expect(page.getByText('/ newest')).toBeVisible();
});

test('displays story titles', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByText('Alpha Story')).toBeVisible();
  await expect(page.getByText('Beta Story')).toBeVisible();
});

test('story with url renders as a link', async ({ page }) => {
  await page.goto('/');
  const link = page.getByRole('link', { name: 'Alpha Story' });
  await expect(link).toBeVisible();
  await expect(link).toHaveAttribute('href', 'https://alpha.com');
});

test('story without url renders as plain text', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByText('Beta Story')).toBeVisible();
  const betaLink = page.getByRole('link', { name: 'Beta Story' });
  await expect(betaLink).not.toBeVisible();
});

test('search input is present', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByPlaceholder('Filter by title...')).toBeVisible();
});

test('paginator is present', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('group', { name: /select page/i })).toBeVisible();
});

test('clicking next page sends page=2 to the API', async ({ page }) => {
  await page.unrouteAll();
  await page.route(STORIES_URL, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [{ id: 1, title: 'Alpha Story', url: 'https://alpha.com', by: 'user1', time: 1700000001, score: 100 }],
        total: 50,
        page: 1,
        pageSize: 20
      })
    })
  );

  await page.goto('/');

  const nextRequest = page.waitForRequest(req =>
    req.url().includes('/api/stories') && req.url().includes('page=2')
  );
  await page.getByLabel('Next page').click();

  const req = await nextRequest;
  expect(req.url()).toContain('page=2');
});
