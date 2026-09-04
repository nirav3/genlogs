import { jest } from '@jest/globals';
import { lookupCarriers, LookupError } from '../apiClient.js';
import config from '../config.js';

function mockFetchOnce(status, body) {
  global.fetch = jest.fn().mockResolvedValue({
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  });
}

describe('apiClient.lookupCarriers', () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  test('success path: sends correct URL, method, headers, body and returns carriers unmodified', async () => {
    const carriers = [
      { name: 'Knight-Swift', trucksPerDay: 42 },
      { name: 'J.B. Hunt', trucksPerDay: 30 },
    ];
    mockFetchOnce(200, { carriers });

    const result = await lookupCarriers('New York City, NY', 'Washington, DC', 'fake-id-token');

    expect(global.fetch).toHaveBeenCalledTimes(1);
    const [url, options] = global.fetch.mock.calls[0];
    expect(url).toBe(`${config.API_BASE_URL}/api/carriers/lookup`);
    expect(options.method).toBe('POST');
    expect(options.headers['Content-Type']).toBe('application/json');
    expect(options.headers['Authorization']).toBe('Bearer fake-id-token');
    expect(JSON.parse(options.body)).toEqual({
      origin: 'New York City, NY',
      destination: 'Washington, DC',
    });
    expect(result).toEqual({ carriers });
  });

  test('classifies 400 as validation', async () => {
    mockFetchOnce(400, { title: 'raw validation error payload' });
    await expect(lookupCarriers('a', 'b', 'tok')).rejects.toMatchObject({
      classification: 'validation',
    });
  });

  test('classifies 401 as auth', async () => {
    mockFetchOnce(401, { title: 'raw auth error payload' });
    await expect(lookupCarriers('a', 'b', 'tok')).rejects.toMatchObject({
      classification: 'auth',
    });
  });

  test('classifies 429 as rate_limited', async () => {
    mockFetchOnce(429, { title: 'raw rate limit payload' });
    await expect(lookupCarriers('a', 'b', 'tok')).rejects.toMatchObject({
      classification: 'rate_limited',
    });
  });

  test('classifies 500 as server', async () => {
    mockFetchOnce(500, { title: 'raw server error payload' });
    await expect(lookupCarriers('a', 'b', 'tok')).rejects.toMatchObject({
      classification: 'server',
    });
  });

  test('classifies a network failure (fetch rejects) as server', async () => {
    global.fetch = jest.fn().mockRejectedValue(new TypeError('Failed to fetch'));
    await expect(lookupCarriers('a', 'b', 'tok')).rejects.toMatchObject({
      classification: 'server',
    });
  });

  test('error message never contains the raw response body text', async () => {
    mockFetchOnce(400, { title: 'raw validation error payload', errors: { origin: ['bad'] } });
    try {
      await lookupCarriers('a', 'b', 'tok');
      throw new Error('expected lookupCarriers to throw');
    } catch (err) {
      expect(err).toBeInstanceOf(LookupError);
      expect(err.message).not.toMatch(/raw validation error payload/);
    }
  });
});
