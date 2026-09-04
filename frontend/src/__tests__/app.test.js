import { jest } from '@jest/globals';

function buildDom() {
  document.body.innerHTML = `
    <div data-role="setup-error" hidden></div>
    <div data-role="signin-container"></div>
    <div data-role="origin-container"></div>
    <div data-role="destination-container"></div>
    <button data-role="search-button" disabled>Search</button>
    <div data-role="status"></div>
    <div data-role="map"></div>
    <ol data-role="route-summary"></ol>
    <div data-role="route-status"></div>
    <div data-role="carrier-list"></div>
  `;
  return document.body;
}

function fakeValidConfig() {
  return {
    GOOGLE_CLIENT_ID: 'test-client-id.apps.googleusercontent.com',
    GOOGLE_MAPS_API_KEY: 'test-maps-api-key',
    API_BASE_URL: 'http://localhost:9999',
  };
}

function buildMocks() {
  const authMock = {
    initAuth: jest.fn(),
    getIdToken: jest.fn(() => 'fake-id-token'),
    isSignedIn: jest.fn(() => false),
    onAuthChange: jest.fn((cb) => {
      authMock._callback = cb;
    }),
    signOut: jest.fn(),
  };

  const placesMock = {
    initPlaces: jest.fn(),
    getOrigin: jest.fn(() => ({ id: 'origin-id', formattedAddress: 'New York, NY, USA' })),
    getDestination: jest.fn(() => ({ id: 'dest-id', formattedAddress: 'Washington, DC, USA' })),
    onSelectionChange: jest.fn((cb) => {
      placesMock._callback = cb;
    }),
  };

  const mapRoutesMock = {
    initMap: jest.fn(),
    renderRoutes: jest.fn().mockResolvedValue({ routes: [] }),
  };

  const carrierListMock = {
    initCarrierList: jest.fn(),
    renderCarriers: jest.fn(),
    clearCarrierList: jest.fn(),
  };

  const apiClientMock = {
    lookupCarriers: jest.fn().mockResolvedValue({ carriers: [] }),
  };

  return { authMock, placesMock, mapRoutesMock, carrierListMock, apiClientMock };
}

async function loadAppWithMocks(mocks) {
  jest.resetModules();
  jest.unstable_mockModule('../auth.js', () => mocks.authMock);
  jest.unstable_mockModule('../places.js', () => mocks.placesMock);
  jest.unstable_mockModule('../mapRoutes.js', () => mocks.mapRoutesMock);
  jest.unstable_mockModule('../carrierList.js', () => mocks.carrierListMock);
  jest.unstable_mockModule('../apiClient.js', () => mocks.apiClientMock);
  return import('../app.js');
}

describe('app.js', () => {
  let root;
  let mocks;
  let app;

  beforeEach(async () => {
    root = buildDom();
    mocks = buildMocks();
    globalThis.__GENLOGS_SKIP_AUTO_INIT__ = true;
    app = await loadAppWithMocks(mocks);
  });

  afterEach(() => {
    delete globalThis.__GENLOGS_SKIP_AUTO_INIT__;
    document.head.querySelectorAll('script').forEach((el) => el.remove());
  });

  describe('7.1 config validation', () => {
    test('valid config starts the app and initializes auth', () => {
      const result = app.init(root, fakeValidConfig());

      expect(result.started).toBe(true);
      expect(mocks.authMock.initAuth).toHaveBeenCalledTimes(1);
      expect(root.querySelector('[data-role="setup-error"]').hidden).toBe(true);
    });

    test('missing config value shows a setup error and skips SDK initialization', () => {
      const badConfig = { ...fakeValidConfig(), GOOGLE_MAPS_API_KEY: '' };

      const result = app.init(root, badConfig);

      expect(result.started).toBe(false);
      const errorEl = root.querySelector('[data-role="setup-error"]');
      expect(errorEl.hidden).toBe(false);
      expect(errorEl.textContent).toMatch(/GOOGLE_MAPS_API_KEY/);
      expect(mocks.authMock.initAuth).not.toHaveBeenCalled();
      expect(mocks.placesMock.initPlaces).not.toHaveBeenCalled();
      expect(mocks.mapRoutesMock.initMap).not.toHaveBeenCalled();
      expect(document.head.querySelector('script[src*="maps.googleapis.com"]')).toBeNull();
    });

    test('placeholder config value (still the example template) is treated as missing', () => {
      const badConfig = { ...fakeValidConfig(), GOOGLE_CLIENT_ID: 'REPLACE_WITH_GOOGLE_OAUTH_CLIENT_ID' };

      const result = app.init(root, badConfig);

      expect(result.started).toBe(false);
      expect(root.querySelector('[data-role="setup-error"]').textContent).toMatch(/GOOGLE_CLIENT_ID/);
    });
  });

  describe('7.2 search availability gating', () => {
    test('search stays disabled until both sign-in and both resolved places are true', () => {
      app.init(root, fakeValidConfig());
      const button = root.querySelector('[data-role="search-button"]');

      expect(button.disabled).toBe(true);

      mocks.placesMock._callback({
        origin: { id: 'o' },
        destination: null,
      });
      expect(button.disabled).toBe(true);

      mocks.placesMock._callback({
        origin: { id: 'o' },
        destination: { id: 'd' },
      });
      expect(button.disabled).toBe(true);

      mocks.authMock._callback(true);
      expect(button.disabled).toBe(false);

      mocks.authMock._callback(false);
      expect(button.disabled).toBe(true);
    });
  });

  describe('7.3 search submission', () => {
    test('clicking search calls apiClient.lookupCarriers and mapRoutes.renderRoutes together and shows loading until both resolve', async () => {
      let resolveLookup;
      let resolveRoutes;
      mocks.apiClientMock.lookupCarriers.mockReturnValue(
        new Promise((resolve) => {
          resolveLookup = resolve;
        })
      );
      mocks.mapRoutesMock.renderRoutes.mockReturnValue(
        new Promise((resolve) => {
          resolveRoutes = resolve;
        })
      );

      app.init(root, fakeValidConfig());
      mocks.authMock._callback(true);
      mocks.placesMock._callback({ origin: { id: 'o' }, destination: { id: 'd' } });

      const button = root.querySelector('[data-role="search-button"]');
      const status = root.querySelector('[data-role="status"]');
      expect(button.disabled).toBe(false);

      button.click();
      await Promise.resolve();

      expect(mocks.apiClientMock.lookupCarriers).toHaveBeenCalledTimes(1);
      expect(mocks.mapRoutesMock.renderRoutes).toHaveBeenCalledTimes(1);
      expect(mocks.carrierListMock.clearCarrierList).toHaveBeenCalledTimes(1);
      expect(status.textContent).toMatch(/searching/i);
      expect(button.disabled).toBe(true);

      resolveLookup({ carriers: [{ name: 'UPS Inc.', trucksPerDay: 5 }] });
      resolveRoutes({ routes: [] });
      await Promise.resolve();
      await Promise.resolve();
      await Promise.resolve();

      expect(mocks.carrierListMock.renderCarriers).toHaveBeenCalledWith([
        { name: 'UPS Inc.', trucksPerDay: 5 },
      ]);
      expect(status.textContent).toBe('');
      expect(button.disabled).toBe(false);
    });
  });

  describe('7.4 error classification handling', () => {
    async function triggerSearchWithError(classification) {
      app.init(root, fakeValidConfig());
      mocks.authMock._callback(true);
      mocks.placesMock._callback({ origin: { id: 'o' }, destination: { id: 'd' } });

      const error = new Error('lookup failed');
      error.classification = classification;
      mocks.apiClientMock.lookupCarriers.mockRejectedValue(error);

      const button = root.querySelector('[data-role="search-button"]');
      button.click();
      await Promise.resolve();
      await Promise.resolve();
      await Promise.resolve();

      return root.querySelector('[data-role="status"]');
    }

    test('auth classification calls auth.signOut() and shows a re-sign-in prompt', async () => {
      const status = await triggerSearchWithError('auth');
      expect(mocks.authMock.signOut).toHaveBeenCalledTimes(1);
      expect(status.textContent).toMatch(/sign in again/i);
    });

    test('validation classification shows a distinct message and does not sign out', async () => {
      const status = await triggerSearchWithError('validation');
      expect(mocks.authMock.signOut).not.toHaveBeenCalled();
      expect(status.textContent).toMatch(/check the selected origin and destination/i);
    });

    test('rate_limited classification shows a distinct message and does not sign out', async () => {
      const status = await triggerSearchWithError('rate_limited');
      expect(mocks.authMock.signOut).not.toHaveBeenCalled();
      expect(status.textContent).toMatch(/too many requests/i);
    });

    test('server classification shows a distinct generic message and does not sign out', async () => {
      const status = await triggerSearchWithError('server');
      expect(mocks.authMock.signOut).not.toHaveBeenCalled();
      expect(status.textContent).toMatch(/something went wrong/i);
    });

    test('a failing search clears the carrier list rather than leaving a prior result looking current', async () => {
      await triggerSearchWithError('server');
      expect(mocks.carrierListMock.clearCarrierList).toHaveBeenCalledTimes(1);
      expect(mocks.carrierListMock.renderCarriers).not.toHaveBeenCalled();
    });
  });

  describe('7.5 Maps script injection', () => {
    test('injects the Maps JS API script tag at runtime using the configured API key, not hardcoded', () => {
      const config = fakeValidConfig();
      app.init(root, config);

      const script = document.head.querySelector('script[src*="maps.googleapis.com"]');
      expect(script).not.toBeNull();
      expect(script.src).toContain(encodeURIComponent(config.GOOGLE_MAPS_API_KEY));
    });

    test('on script load, initializes places and the map', () => {
      app.init(root, fakeValidConfig());
      const script = document.head.querySelector('script[src*="maps.googleapis.com"]');

      script.onload();

      expect(mocks.placesMock.initPlaces).toHaveBeenCalledTimes(1);
      expect(mocks.mapRoutesMock.initMap).toHaveBeenCalledTimes(1);
    });
  });
});
