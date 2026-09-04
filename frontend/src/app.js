import defaultConfig from './config.js';
import * as auth from './auth.js';
import * as places from './places.js';
import * as mapRoutes from './mapRoutes.js';
import * as carrierList from './carrierList.js';
import { lookupCarriers } from './apiClient.js';

const REQUIRED_CONFIG_KEYS = ['GOOGLE_CLIENT_ID', 'GOOGLE_MAPS_API_KEY', 'API_BASE_URL'];
const PLACEHOLDER_PREFIX = 'REPLACE_WITH_';

const STATUS_MESSAGES = {
  loading: 'Searching for carriers and routes…',
  auth: 'Your sign-in has expired or is invalid. Please sign in again.',
  validation: 'Please check the selected origin and destination and try again.',
  rate_limited: 'Too many requests. Please wait a moment and try again.',
  server: 'Something went wrong. Please try again shortly.',
};

export function validateConfig(config) {
  const missing = REQUIRED_CONFIG_KEYS.filter((key) => {
    const value = config && config[key];
    return !value || value.startsWith(PLACEHOLDER_PREFIX);
  });
  return { valid: missing.length === 0, missing };
}

export function showSetupError(root, missingKeys) {
  const el = root.querySelector('[data-role="setup-error"]');
  if (!el) return;
  el.hidden = false;
  el.textContent =
    `Setup error: missing or placeholder config value(s): ${missingKeys.join(', ')}. ` +
    'See frontend/README.md for local setup.';
}

export function injectMapsScript(apiKey, { onload, onerror } = {}) {
  const script = document.createElement('script');
  script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&libraries=places`;
  script.async = true;
  if (onload) {
    script.onload = onload;
  }
  // Without this, a script that fails to load at all (network block, ad
  // blocker, bad key causing a hard failure) fails completely silently: no
  // onload ever fires, so places/map never initialize and nothing tells the
  // user or the console why.
  script.onerror = () => {
    if (onerror) onerror();
  };
  document.head.appendChild(script);
  return script;
}

export function createAppState() {
  return { signedIn: false, originResolved: false, destinationResolved: false };
}

export function isSearchEnabled(state) {
  return Boolean(state.signedIn && state.originResolved && state.destinationResolved);
}

function setStatus(root, message, { role = 'status' } = {}) {
  const el = root.querySelector('[data-role="status"]');
  if (!el) return;
  el.textContent = message || '';
  el.dataset.state = message ? role : '';
}

function updateSearchButton(root, state) {
  const button = root.querySelector('[data-role="search-button"]');
  if (!button) return;
  button.disabled = !isSearchEnabled(state);
}

async function handleSearch(root, state) {
  const originPlace = places.getOrigin();
  const destinationPlace = places.getDestination();
  const idToken = auth.getIdToken();

  const button = root.querySelector('[data-role="search-button"]');
  if (button) {
    button.disabled = true;
  }
  // Clear immediately, not just on success: a new search — or one that ends in
  // an error — must never leave the previous lane's results looking current
  // (portal-ui spec: "does not display a stale or partial carrier list").
  carrierList.clearCarrierList();
  setStatus(root, STATUS_MESSAGES.loading, { role: 'loading' });

  try {
    const [lookupResult] = await Promise.all([
      lookupCarriers(originPlace.formattedAddress, destinationPlace.formattedAddress, idToken),
      mapRoutes.renderRoutes(originPlace, destinationPlace),
    ]);

    carrierList.renderCarriers(lookupResult.carriers);
    setStatus(root, '');
  } catch (err) {
    const classification = err && err.classification ? err.classification : 'server';
    setStatus(root, STATUS_MESSAGES[classification] || STATUS_MESSAGES.server, { role: classification });
    if (classification === 'auth') {
      auth.signOut();
    }
  } finally {
    updateSearchButton(root, state);
  }
}

export function init(root = document, config = defaultConfig) {
  const { valid, missing } = validateConfig(config);
  if (!valid) {
    showSetupError(root, missing);
    return { started: false };
  }

  const state = createAppState();

  const signinContainer = root.querySelector('[data-role="signin-container"]');
  const originContainer = root.querySelector('[data-role="origin-container"]');
  const destinationContainer = root.querySelector('[data-role="destination-container"]');
  const mapContainer = root.querySelector('[data-role="map"]');
  const routeSummaryContainer = root.querySelector('[data-role="route-summary"]');
  const routeStatusContainer = root.querySelector('[data-role="route-status"]');
  const carrierListContainer = root.querySelector('[data-role="carrier-list"]');
  const searchButton = root.querySelector('[data-role="search-button"]');

  carrierList.initCarrierList(carrierListContainer);
  updateSearchButton(root, state);

  const authStatusEl = root.querySelector('[data-role="auth-status"]');

  auth.onAuthChange((signedIn) => {
    state.signedIn = signedIn;
    updateSearchButton(root, state);
    if (authStatusEl) {
      authStatusEl.textContent = signedIn ? 'Signed in.' : '';
    }
    if (!signedIn) {
      setStatus(root, STATUS_MESSAGES.auth, { role: 'auth' });
    } else {
      setStatus(root, '');
    }
  });

  places.onSelectionChange(({ origin, destination }) => {
    state.originResolved = Boolean(origin);
    state.destinationResolved = Boolean(destination);
    updateSearchButton(root, state);
  });

  auth.initAuth(signinContainer);

  injectMapsScript(config.GOOGLE_MAPS_API_KEY, {
    onload: () => {
      // initPlaces/initMap read window.google.maps synchronously — a
      // try/catch here (rather than relying on Promise rejection) is what
      // actually surfaces a failure, since these calls are not async.
      try {
        places.initPlaces({ originContainer, destinationContainer });
        mapRoutes.initMap({
          mapContainer,
          statusContainer: routeStatusContainer,
          summaryContainer: routeSummaryContainer,
        });
      } catch (err) {
        console.error('Failed to initialize Places/Maps:', err);
        setStatus(root, 'Failed to load Google Maps. Please refresh and try again.', { role: 'server' });
      }
    },
    onerror: () => {
      setStatus(root, 'Failed to load Google Maps. Please refresh and try again.', { role: 'server' });
    },
  });

  if (searchButton) {
    searchButton.addEventListener('click', () => {
      handleSearch(root, state);
    });
  }

  return { started: true, state };
}

if (typeof document !== 'undefined' && !globalThis.__GENLOGS_SKIP_AUTO_INIT__) {
  document.addEventListener('DOMContentLoaded', () => init());
}
