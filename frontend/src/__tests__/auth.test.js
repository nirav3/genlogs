import { jest } from '@jest/globals';

function setupGoogleMock() {
  const initialize = jest.fn();
  const renderButton = jest.fn();
  window.google = {
    accounts: {
      id: { initialize, renderButton },
    },
  };
  return { initialize, renderButton };
}

async function freshAuthModule() {
  jest.resetModules();
  return import('../auth.js');
}

describe('auth.js', () => {
  afterEach(() => {
    delete window.google;
  });

  test('initAuth initializes Google Identity Services with the configured client ID and renders the button', async () => {
    const { initialize, renderButton } = setupGoogleMock();
    const auth = await freshAuthModule();
    const container = document.createElement('div');

    auth.initAuth(container);

    expect(initialize).toHaveBeenCalledTimes(1);
    expect(initialize.mock.calls[0][0]).toMatchObject({
      client_id: expect.any(String),
      callback: expect.any(Function),
    });
    expect(renderButton).toHaveBeenCalledWith(container, expect.any(Object));
  });

  test('signed-in callback populates the token and fires onAuthChange(true)', async () => {
    const { initialize } = setupGoogleMock();
    const auth = await freshAuthModule();
    auth.initAuth(document.createElement('div'));

    const listener = jest.fn();
    auth.onAuthChange(listener);

    expect(auth.isSignedIn()).toBe(false);
    expect(auth.getIdToken()).toBeNull();

    const credentialCallback = initialize.mock.calls[0][0].callback;
    credentialCallback({ credential: 'fake-id-token' });

    expect(auth.getIdToken()).toBe('fake-id-token');
    expect(auth.isSignedIn()).toBe(true);
    expect(listener).toHaveBeenCalledWith(true);
  });

  test('signOut clears the in-memory token and fires onAuthChange(false)', async () => {
    const { initialize } = setupGoogleMock();
    const auth = await freshAuthModule();
    auth.initAuth(document.createElement('div'));

    const credentialCallback = initialize.mock.calls[0][0].callback;
    credentialCallback({ credential: 'fake-id-token' });
    expect(auth.getIdToken()).toBe('fake-id-token');

    const listener = jest.fn();
    auth.onAuthChange(listener);

    auth.signOut();

    expect(auth.getIdToken()).toBeNull();
    expect(auth.isSignedIn()).toBe(false);
    expect(listener).toHaveBeenCalledWith(false);
  });
});
