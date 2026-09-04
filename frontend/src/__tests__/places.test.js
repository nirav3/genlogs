import { jest } from '@jest/globals';

function makeStubPlace(overrides = {}) {
  return {
    id: 'place-id-123',
    displayName: 'New York City, NY',
    formattedAddress: 'New York, NY, USA',
    fetchFields: jest.fn().mockResolvedValue(undefined),
    ...overrides,
  };
}

function setupPlacesMock() {
  window.google = {
    maps: {
      places: {
        PlaceAutocompleteElement: class {
          constructor() {
            const el = document.createElement('div');
            return el;
          }
        },
      },
    },
  };
}

async function freshPlacesModule() {
  jest.resetModules();
  return import('../places.js');
}

describe('places.js', () => {
  afterEach(() => {
    delete window.google;
  });

  test('getOrigin/getDestination return null before any place is selected', async () => {
    setupPlacesMock();
    const places = await freshPlacesModule();
    const originContainer = document.createElement('div');
    const destinationContainer = document.createElement('div');

    await places.initPlaces({ originContainer, destinationContainer });

    expect(places.getOrigin()).toBeNull();
    expect(places.getDestination()).toBeNull();
  });

  test('resolves the selected place on gmp-select and notifies onSelectionChange listeners', async () => {
    setupPlacesMock();
    const places = await freshPlacesModule();
    const originContainer = document.createElement('div');
    const destinationContainer = document.createElement('div');

    const { originElement, destinationElement } = await places.initPlaces({ originContainer, destinationContainer });

    const listener = jest.fn();
    places.onSelectionChange(listener);

    const originPlace = makeStubPlace({ displayName: 'New York City, NY' });
    const event = new Event('gmp-select');
    event.placePrediction = { toPlace: () => originPlace };
    originElement.dispatchEvent(event);

    await Promise.resolve();
    await Promise.resolve();

    expect(originPlace.fetchFields).toHaveBeenCalledWith({
      fields: ['id', 'displayName', 'formattedAddress', 'location'],
    });
    expect(places.getOrigin()).toBe(originPlace);
    expect(places.getDestination()).toBeNull();
    expect(listener).toHaveBeenCalledWith({ origin: originPlace, destination: null });

    const destinationPlace = makeStubPlace({ displayName: 'Washington, DC' });
    const destEvent = new Event('gmp-select');
    destEvent.placePrediction = { toPlace: () => destinationPlace };
    destinationElement.dispatchEvent(destEvent);

    await Promise.resolve();
    await Promise.resolve();

    expect(places.getDestination()).toBe(destinationPlace);
    expect(listener).toHaveBeenCalledWith({ origin: originPlace, destination: destinationPlace });
  });
});
