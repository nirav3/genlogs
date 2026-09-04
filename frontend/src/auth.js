import config from './config.js';

let idToken = null;
const listeners = new Set();

function notify(signedIn) {
  for (const listener of listeners) {
    listener(signedIn);
  }
}

function handleCredentialResponse(response) {
  idToken = response.credential;
  notify(true);
}

export function initAuth(buttonContainer) {
  window.google.accounts.id.initialize({
    client_id: config.GOOGLE_CLIENT_ID,
    callback: handleCredentialResponse,
  });

  if (buttonContainer) {
    window.google.accounts.id.renderButton(buttonContainer, { theme: 'outline', size: 'large' });
  }
}

export function getIdToken() {
  return idToken;
}

export function isSignedIn() {
  return idToken !== null;
}

export function onAuthChange(callback) {
  listeners.add(callback);
  return () => listeners.delete(callback);
}

export function signOut() {
  idToken = null;
  notify(false);
}
