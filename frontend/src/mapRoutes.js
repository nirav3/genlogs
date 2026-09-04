const METERS_PER_MILE = 1609.344;

const SELECTED_STYLE = { strokeColor: '#1a56db', strokeWeight: 6, strokeOpacity: 1 };
const UNSELECTED_STYLE = { strokeColor: '#9aa3b0', strokeWeight: 3, strokeOpacity: 0.6 };

let map = null;
let directionsService = null;
let trafficLayer = null;
let renderers = []; // current on-map renderers, index = display rank (0 = fastest)
let currentResult = null; // the DirectionsResult the current renderers were built from
let currentRouteCount = 0;
let selectedIndex = 0;
let statusElement = null;
let summaryElement = null;

function removeRenderers() {
  for (const renderer of renderers) {
    renderer.setMap(null);
  }
  renderers = [];
}

function setStatus(message) {
  if (statusElement) {
    statusElement.textContent = message;
  }
}

function clearSummary() {
  if (summaryElement) {
    summaryElement.textContent = '';
  }
}

function routeDurationSeconds(route) {
  // Prefer the traffic-aware estimate when Google returns one (requires drivingOptions
  // on the request, see renderRoutes) — falls back to the traffic-free duration for
  // legs/routes Google didn't have current traffic data for.
  return route.legs.reduce((total, leg) => {
    const duration = leg.duration_in_traffic || leg.duration;
    return total + (duration ? duration.value : 0);
  }, 0);
}

function routeDistanceMeters(route) {
  return route.legs.reduce((total, leg) => total + (leg.distance ? leg.distance.value : 0), 0);
}

function formatDuration(totalSeconds) {
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.round((totalSeconds % 3600) / 60);
  if (hours === 0) {
    return `${minutes} min${minutes === 1 ? '' : 's'}`;
  }
  if (minutes === 0) {
    return `${hours} hr${hours === 1 ? '' : 's'}`;
  }
  return `${hours} hr${hours === 1 ? '' : 's'} ${minutes} min${minutes === 1 ? '' : 's'}`;
}

function formatMiles(totalMeters) {
  const miles = totalMeters / METERS_PER_MILE;
  // One decimal place under 10 miles (short lanes deserve more precision), whole miles above.
  return `${miles < 10 ? miles.toFixed(1) : Math.round(miles)} mi`;
}

// Rebuilds every on-map renderer from scratch for the current selection, rather than
// restyling renderers already on the map. DirectionsRenderer doesn't support that reliably:
// setOptions() alone doesn't repaint an already-drawn route, and the documented workaround of
// re-applying setDirections() to force a repaint has its own trap — it silently resets
// routeIndex back to 0, so every renderer ends up showing the same (fastest) route instead of
// its own. Recreating renderers from the stored DirectionsResult sidesteps both problems.
function renderMapRoutes() {
  removeRenderers();
  for (let index = 0; index < currentRouteCount; index++) {
    const selected = index === selectedIndex;
    const renderer = new window.google.maps.DirectionsRenderer({
      map,
      suppressMarkers: !selected,
      polylineOptions: selected ? SELECTED_STYLE : UNSELECTED_STYLE,
      zIndex: selected ? 2 : 1,
    });
    renderer.setDirections(currentResult);
    renderer.setRouteIndex(index);
    // Polylines are clickable by default — clicking a route's line on the map selects it,
    // same as clicking its entry in the summary list below.
    renderer.addListener('click', () => selectRoute(index));
    renderers.push(renderer);
  }
}

function updateSummarySelection() {
  if (!summaryElement) return;
  summaryElement.querySelectorAll('.route-summary-item').forEach((item, index) => {
    const selected = index === selectedIndex;
    item.classList.toggle('is-selected', selected);
    item.setAttribute('aria-pressed', String(selected));
  });
}

function selectRoute(index) {
  if (index < 0 || index >= currentRouteCount || index === selectedIndex) return;
  selectedIndex = index;
  renderMapRoutes();
  updateSummarySelection();
}

function renderSummary(routes) {
  if (!summaryElement) return;

  summaryElement.textContent = '';
  routes.forEach((route, index) => {
    const item = document.createElement('li');
    item.className = 'route-summary-item';
    // Clickable list entries mirror clicking the route's line on the map (see selectRoute) —
    // either one selects/highlights the same route.
    item.tabIndex = 0;
    item.setAttribute('role', 'button');
    item.setAttribute('aria-pressed', 'false');
    item.addEventListener('click', () => selectRoute(index));
    item.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        selectRoute(index);
      }
    });

    const label = document.createElement('span');
    label.className = 'route-summary-label';
    label.textContent = index === 0 ? 'Fastest route' : `Route ${index + 1}`;

    const duration = document.createElement('span');
    duration.className = 'route-summary-duration';
    duration.textContent = formatDuration(routeDurationSeconds(route));

    const distance = document.createElement('span');
    distance.className = 'route-summary-distance';
    distance.textContent = formatMiles(routeDistanceMeters(route));

    item.appendChild(label);
    item.appendChild(duration);
    item.appendChild(distance);
    summaryElement.appendChild(item);
  });
}

export function initMap({ mapContainer, statusContainer, summaryContainer }) {
  map = new window.google.maps.Map(mapContainer, { center: { lat: 39.8283, lng: -98.5795 }, zoom: 4 });
  directionsService = new window.google.maps.DirectionsService();
  trafficLayer = new window.google.maps.TrafficLayer();
  trafficLayer.setMap(map);
  statusElement = statusContainer || null;
  summaryElement = summaryContainer || null;
  currentResult = null;
  currentRouteCount = 0;
  selectedIndex = 0;
  removeRenderers();
  clearSummary();
  return map;
}

export async function renderRoutes(originPlace, destinationPlace) {
  if (!map || !directionsService) {
    throw new Error('mapRoutes.initMap must be called before renderRoutes');
  }

  removeRenderers();
  clearSummary();
  setStatus('');
  currentResult = null;
  currentRouteCount = 0;
  selectedIndex = 0;

  const request = {
    origin: { placeId: originPlace.id },
    destination: { placeId: destinationPlace.id },
    travelMode: window.google.maps.TravelMode.DRIVING,
    provideRouteAlternatives: true,
    // departureTime must be "now" or a future Date for Google to return duration_in_traffic
    // on each leg — without this the API only ever returns the traffic-free estimate.
    drivingOptions: {
      departureTime: new Date(),
      trafficModel: window.google.maps.TrafficModel.BEST_GUESS,
    },
  };

  let result;
  try {
    result = await directionsService.route(request);
  } catch {
    setStatus('No route was found between these locations.');
    return { routes: [] };
  }

  const routes = result.routes || [];
  if (routes.length === 0) {
    setStatus('No route was found between these locations.');
    return { routes: [] };
  }

  const sortedRoutes = [...routes].sort(
    (a, b) => routeDurationSeconds(a) - routeDurationSeconds(b)
  );
  const topRoutes = sortedRoutes.slice(0, 3);

  currentResult = { ...result, routes: sortedRoutes };
  currentRouteCount = topRoutes.length;

  renderMapRoutes();
  renderSummary(topRoutes);
  updateSummarySelection();

  return { routes: topRoutes };
}
