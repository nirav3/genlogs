import { jest } from '@jest/globals';

function makeRoute(durationSeconds, distanceMeters = 0) {
  return {
    legs: [{ duration: { value: durationSeconds }, distance: { value: distanceMeters } }],
  };
}

function setupGoogleMapsMock({ routeResult, routeError } = {}) {
  const rendererInstances = [];
  const trafficLayerInstances = [];
  const routeRequests = [];

  window.google = {
    maps: {
      Map: class {
        constructor(container, opts) {
          this.container = container;
          this.opts = opts;
        }
      },
      DirectionsService: class {
        route(request) {
          routeRequests.push(request);
          if (routeError) {
            return Promise.reject(routeError);
          }
          return Promise.resolve(routeResult);
        }
      },
      DirectionsRenderer: class {
        constructor(opts) {
          this.opts = opts;
          this.directions = null;
          this.routeIndex = null;
          this.map = opts && opts.map;
          this.options = { ...opts };
          this._listeners = {};
          rendererInstances.push(this);
        }
        setDirections(result) {
          this.directions = result;
          this.setDirectionsCallCount = (this.setDirectionsCallCount || 0) + 1;
        }
        getDirections() {
          return this.directions;
        }
        setRouteIndex(index) {
          this.routeIndex = index;
        }
        setMap(map) {
          this.map = map;
        }
        setOptions(options) {
          this.options = { ...this.options, ...options };
        }
        addListener(event, handler) {
          (this._listeners[event] ||= []).push(handler);
        }
        fire(event) {
          for (const handler of this._listeners[event] || []) {
            handler();
          }
        }
      },
      TrafficLayer: class {
        constructor() {
          this.map = null;
          trafficLayerInstances.push(this);
        }
        setMap(map) {
          this.map = map;
        }
      },
      TravelMode: { DRIVING: 'DRIVING' },
      TrafficModel: { BEST_GUESS: 'BEST_GUESS' },
    },
  };

  return { rendererInstances, trafficLayerInstances, routeRequests };
}

async function freshMapRoutesModule() {
  jest.resetModules();
  return import('../mapRoutes.js');
}

const originPlace = { id: 'place-origin' };
const destinationPlace = { id: 'place-destination' };

describe('mapRoutes.js', () => {
  afterEach(() => {
    delete window.google;
  });

  test('zero routes: shows a "no route found" state and renders no map routes', async () => {
    const { rendererInstances } = setupGoogleMapsMock({ routeResult: { routes: [] } });
    const mapRoutes = await freshMapRoutesModule();
    const statusContainer = document.createElement('div');

    await mapRoutes.initMap({ mapContainer: document.createElement('div'), statusContainer });
    const outcome = await mapRoutes.renderRoutes(originPlace, destinationPlace);

    expect(outcome.routes).toHaveLength(0);
    expect(statusContainer.textContent).toMatch(/no route/i);
    expect(rendererInstances).toHaveLength(0);
  });

  test('directions request failure: shows a "no route found" state rather than a stale/empty map', async () => {
    const { rendererInstances } = setupGoogleMapsMock({ routeError: new Error('ZERO_RESULTS') });
    const mapRoutes = await freshMapRoutesModule();
    const statusContainer = document.createElement('div');

    await mapRoutes.initMap({ mapContainer: document.createElement('div'), statusContainer });
    const outcome = await mapRoutes.renderRoutes(originPlace, destinationPlace);

    expect(outcome.routes).toHaveLength(0);
    expect(statusContainer.textContent).toMatch(/no route/i);
    expect(rendererInstances).toHaveLength(0);
  });

  test('4+ alternative routes: renders exactly 3, in ascending-duration order', async () => {
    const routes = [makeRoute(5000), makeRoute(1000), makeRoute(3000), makeRoute(2000)];
    const { rendererInstances } = setupGoogleMapsMock({ routeResult: { routes } });
    const mapRoutes = await freshMapRoutesModule();

    await mapRoutes.initMap({ mapContainer: document.createElement('div'), statusContainer: document.createElement('div') });
    const outcome = await mapRoutes.renderRoutes(originPlace, destinationPlace);

    expect(outcome.routes).toHaveLength(3);
    const durations = outcome.routes.map((r) => r.legs[0].duration.value);
    expect(durations).toEqual([1000, 2000, 3000]);

    expect(rendererInstances).toHaveLength(3);
    rendererInstances.forEach((renderer, index) => {
      expect(renderer.routeIndex).toBe(index);
      expect(renderer.directions.routes[renderer.routeIndex].legs[0].duration.value).toBe(durations[index]);
    });
  });

  test('fewer than 3 routes available: renders all of them without treating it as an error', async () => {
    const routes = [makeRoute(2000), makeRoute(1000)];
    const { rendererInstances } = setupGoogleMapsMock({ routeResult: { routes } });
    const mapRoutes = await freshMapRoutesModule();
    const statusContainer = document.createElement('div');

    await mapRoutes.initMap({ mapContainer: document.createElement('div'), statusContainer });
    const outcome = await mapRoutes.renderRoutes(originPlace, destinationPlace);

    expect(outcome.routes).toHaveLength(2);
    expect(rendererInstances).toHaveLength(2);
    expect(statusContainer.textContent).toBe('');
  });

  describe('route summary (duration/distance)', () => {
    test('renders duration and distance for each route, fastest first and labeled', async () => {
      // 8100s = 2h 15m, 362102.4m = exactly 225 mi (>=10mi branch, whole-number rounding)
      // 3900s = 1h 5m, 4828.032m = exactly 3 mi (<10mi branch, one-decimal rounding)
      const routes = [makeRoute(8100, 362102.4), makeRoute(3900, 4828.032)];
      setupGoogleMapsMock({ routeResult: { routes } });
      const mapRoutes = await freshMapRoutesModule();
      const summaryContainer = document.createElement('ol');

      await mapRoutes.initMap({
        mapContainer: document.createElement('div'),
        statusContainer: document.createElement('div'),
        summaryContainer,
      });
      await mapRoutes.renderRoutes(originPlace, destinationPlace);

      const items = summaryContainer.querySelectorAll('.route-summary-item');
      expect(items).toHaveLength(2);

      expect(items[0].querySelector('.route-summary-label').textContent).toMatch(/fastest/i);
      expect(items[0].querySelector('.route-summary-duration').textContent).toBe('1 hr 5 mins');
      expect(items[0].querySelector('.route-summary-distance').textContent).toBe('3.0 mi');

      expect(items[1].querySelector('.route-summary-label').textContent).toMatch(/route 2/i);
      expect(items[1].querySelector('.route-summary-duration').textContent).toBe('2 hrs 15 mins');
      expect(items[1].querySelector('.route-summary-distance').textContent).toBe('225 mi');
    });

    test('clears stale summary entries when a route lookup finds no route', async () => {
      const routes = [makeRoute(3900, 80467)];
      setupGoogleMapsMock({ routeResult: { routes } });
      const mapRoutes = await freshMapRoutesModule();
      const summaryContainer = document.createElement('ol');

      await mapRoutes.initMap({
        mapContainer: document.createElement('div'),
        statusContainer: document.createElement('div'),
        summaryContainer,
      });
      await mapRoutes.renderRoutes(originPlace, destinationPlace);
      expect(summaryContainer.querySelectorAll('.route-summary-item')).toHaveLength(1);

      window.google.maps.DirectionsService.prototype.route = () => Promise.resolve({ routes: [] });
      await mapRoutes.renderRoutes(originPlace, destinationPlace);

      expect(summaryContainer.querySelectorAll('.route-summary-item')).toHaveLength(0);
    });
  });

  describe('live traffic', () => {
    test('initMap attaches a TrafficLayer to the map', async () => {
      const { trafficLayerInstances } = setupGoogleMapsMock({ routeResult: { routes: [] } });
      const mapRoutes = await freshMapRoutesModule();

      const map = await mapRoutes.initMap({
        mapContainer: document.createElement('div'),
        statusContainer: document.createElement('div'),
      });

      expect(trafficLayerInstances).toHaveLength(1);
      expect(trafficLayerInstances[0].map).toBe(map);
    });

    test('requests traffic-aware routing via drivingOptions', async () => {
      const { routeRequests } = setupGoogleMapsMock({ routeResult: { routes: [makeRoute(1000)] } });
      const mapRoutes = await freshMapRoutesModule();

      await mapRoutes.initMap({ mapContainer: document.createElement('div'), statusContainer: document.createElement('div') });
      await mapRoutes.renderRoutes(originPlace, destinationPlace);

      expect(routeRequests).toHaveLength(1);
      expect(routeRequests[0].drivingOptions).toBeDefined();
      expect(routeRequests[0].drivingOptions.departureTime).toBeInstanceOf(Date);
      expect(routeRequests[0].drivingOptions.trafficModel).toBe('BEST_GUESS');
    });

    test('uses duration_in_traffic for sorting and display when present, falling back to duration otherwise', async () => {
      // Route A: no traffic data, plain duration 1000s (would sort first if traffic were ignored).
      const routeA = { legs: [{ duration: { value: 1000 } }] };
      // Route B: plain duration is a slow 5000s, but duration_in_traffic says it's actually only 200s
      // right now — must sort/display first, proving duration_in_traffic is what's actually used.
      const routeB = { legs: [{ duration: { value: 5000 }, duration_in_traffic: { value: 200 } }] };

      setupGoogleMapsMock({ routeResult: { routes: [routeA, routeB] } });
      const mapRoutes = await freshMapRoutesModule();
      const summaryContainer = document.createElement('ol');

      await mapRoutes.initMap({
        mapContainer: document.createElement('div'),
        statusContainer: document.createElement('div'),
        summaryContainer,
      });
      const outcome = await mapRoutes.renderRoutes(originPlace, destinationPlace);

      expect(outcome.routes[0]).toBe(routeB);
      expect(outcome.routes[1]).toBe(routeA);

      const items = summaryContainer.querySelectorAll('.route-summary-item');
      expect(items[0].querySelector('.route-summary-duration').textContent).toBe('3 mins');
      expect(items[1].querySelector('.route-summary-duration').textContent).toBe('17 mins');
    });
  });

  describe('route selection (clickable routes)', () => {
    async function renderThreeRoutes() {
      const routes = [makeRoute(1000), makeRoute(2000), makeRoute(3000)];
      const mocks = setupGoogleMapsMock({ routeResult: { routes } });
      const mapRoutes = await freshMapRoutesModule();
      const summaryContainer = document.createElement('ol');

      await mapRoutes.initMap({
        mapContainer: document.createElement('div'),
        statusContainer: document.createElement('div'),
        summaryContainer,
      });
      await mapRoutes.renderRoutes(originPlace, destinationPlace);

      return { ...mocks, mapRoutes, summaryContainer };
    }

    test('the fastest route is selected (bold, markers shown) by default', async () => {
      const { rendererInstances, summaryContainer } = await renderThreeRoutes();

      expect(rendererInstances[0].options.suppressMarkers).toBe(false);
      expect(rendererInstances[1].options.suppressMarkers).toBe(true);
      expect(rendererInstances[2].options.suppressMarkers).toBe(true);

      const items = summaryContainer.querySelectorAll('.route-summary-item');
      expect(items[0].classList.contains('is-selected')).toBe(true);
      expect(items[1].classList.contains('is-selected')).toBe(false);
    });

    test('clicking a route\'s line on the map selects it, dims the others, and every renderer keeps its own distinct routeIndex', async () => {
      const { rendererInstances, summaryContainer } = await renderThreeRoutes();
      const initial = [...rendererInstances];

      initial[2].fire('click');

      // The 3 renderers from the initial render must be taken off the map, not left stacked
      // underneath new ones.
      initial.forEach((renderer) => expect(renderer.map).toBeNull());

      // DirectionsRenderer.setDirections() resets its own routeIndex to 0 as a side effect —
      // rebuilding renderers via setRouteIndex() after a fresh setDirections() is what
      // guards against every renderer silently collapsing onto route 0.
      const current = rendererInstances.slice(-3);
      expect(current).toHaveLength(3);
      expect(current.map((r) => r.routeIndex)).toEqual([0, 1, 2]);

      expect(current[0].options.suppressMarkers).toBe(true);
      expect(current[2].options.suppressMarkers).toBe(false);
      expect(current[2].options.polylineOptions.strokeWeight).toBeGreaterThan(
        current[0].options.polylineOptions.strokeWeight
      );

      const items = summaryContainer.querySelectorAll('.route-summary-item');
      expect(items[2].classList.contains('is-selected')).toBe(true);
      expect(items[0].classList.contains('is-selected')).toBe(false);
    });

    test('clicking a route\'s entry in the summary list selects it on the map too', async () => {
      const { rendererInstances, summaryContainer } = await renderThreeRoutes();

      const items = summaryContainer.querySelectorAll('.route-summary-item');
      items[1].dispatchEvent(new Event('click', { bubbles: true }));

      const current = rendererInstances.slice(-3);
      expect(current.map((r) => r.routeIndex)).toEqual([0, 1, 2]);
      expect(current[1].options.suppressMarkers).toBe(false);
      expect(items[1].classList.contains('is-selected')).toBe(true);
      expect(items[1].getAttribute('aria-pressed')).toBe('true');
    });

    test('summary list items are keyboard-activatable', async () => {
      const { rendererInstances, summaryContainer } = await renderThreeRoutes();

      const items = summaryContainer.querySelectorAll('.route-summary-item');
      items[1].dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));

      const current = rendererInstances.slice(-3);
      expect(current[1].options.suppressMarkers).toBe(false);
      expect(items[1].classList.contains('is-selected')).toBe(true);
    });

    test('a new search resets selection back to the fastest route', async () => {
      const { rendererInstances, mapRoutes, summaryContainer } = await renderThreeRoutes();
      rendererInstances[2].fire('click');
      expect(summaryContainer.querySelectorAll('.route-summary-item')[2].classList.contains('is-selected')).toBe(true);

      const nextRoutes = [makeRoute(500), makeRoute(1500)];
      window.google.maps.DirectionsService.prototype.route = () => Promise.resolve({ routes: nextRoutes });
      await mapRoutes.renderRoutes(originPlace, destinationPlace);

      const items = summaryContainer.querySelectorAll('.route-summary-item');
      expect(items[0].classList.contains('is-selected')).toBe(true);
    });
  });
});
