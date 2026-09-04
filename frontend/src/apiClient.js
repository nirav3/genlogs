import config from './config.js';

export class LookupError extends Error {
  constructor(classification, message) {
    super(message);
    this.name = 'LookupError';
    this.classification = classification;
  }
}

const CLASSIFICATION_MESSAGES = {
  validation: 'The lane you searched for could not be understood. Please check the origin and destination and try again.',
  auth: 'Your sign-in has expired or is invalid. Please sign in again.',
  rate_limited: 'Too many requests. Please wait a moment and try again.',
  server: 'Something went wrong reaching the carrier lookup service. Please try again shortly.',
};

function classifyStatus(status) {
  if (status === 400) return 'validation';
  if (status === 401) return 'auth';
  if (status === 429) return 'rate_limited';
  return 'server';
}

export async function lookupCarriers(origin, destination, idToken) {
  let response;
  try {
    response = await fetch(`${config.API_BASE_URL}/api/carriers/lookup`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${idToken}`,
      },
      body: JSON.stringify({ origin, destination }),
    });
  } catch {
    throw new LookupError('server', CLASSIFICATION_MESSAGES.server);
  }

  if (!response.ok) {
    const classification = classifyStatus(response.status);
    throw new LookupError(classification, CLASSIFICATION_MESSAGES[classification]);
  }

  return response.json();
}
