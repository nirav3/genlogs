let originPlace = null;
let destinationPlace = null;
const listeners = new Set();

function notify() {
  for (const listener of listeners) {
    listener({ origin: originPlace, destination: destinationPlace });
  }
}

async function resolvePlace(placePrediction) {
  const place = placePrediction.toPlace();
  await place.fetchFields({ fields: ['id', 'displayName', 'formattedAddress', 'location'] });
  return place;
}

function wireAutocompleteElement(element, onResolved) {
  element.addEventListener('gmp-select', async (event) => {
    const place = await resolvePlace(event.placePrediction);
    onResolved(place);
    notify();
  });
}

export function initPlaces({ originContainer, destinationContainer }) {
  const originElement = new window.google.maps.places.PlaceAutocompleteElement();
  originContainer.appendChild(originElement);
  wireAutocompleteElement(originElement, (place) => {
    originPlace = place;
  });

  const destinationElement = new window.google.maps.places.PlaceAutocompleteElement();
  destinationContainer.appendChild(destinationElement);
  wireAutocompleteElement(destinationElement, (place) => {
    destinationPlace = place;
  });

  return { originElement, destinationElement };
}

export function getOrigin() {
  return originPlace;
}

export function getDestination() {
  return destinationPlace;
}

export function onSelectionChange(callback) {
  listeners.add(callback);
  return () => listeners.delete(callback);
}
