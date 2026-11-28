import { env } from '$env/dynamic/private';

function normalizeBaseUrl(value: string | undefined, fallback: string): string {
  return (value?.trim() || fallback).replace(/\/+$/, '');
}

export function identityApiUrl(): string {
  return normalizeBaseUrl(env.IDENTITY_API_BASE_URL, 'http://localhost:5101');
}

export function habitApiUrl(): string {
  return normalizeBaseUrl(env.HABIT_API_BASE_URL, 'http://localhost:5102');
}

export function challengeApiUrl(): string {
  return normalizeBaseUrl(env.CHALLENGE_API_BASE_URL, 'http://localhost:5104');
}

export function eventTrackingApiUrl(): string {
  return normalizeBaseUrl(env.EVENT_TRACKING_API_BASE_URL, 'http://localhost:5103');
}

export function insightApiUrl(): string {
  return normalizeBaseUrl(env.INSIGHT_API_BASE_URL, 'http://localhost:5105');
}

export async function responseMessage(response: Response, fallback: string): Promise<string> {
  try {
    const body: { message?: string; title?: string } = await response.json();
    return body.message || body.title || fallback;
  } catch {
    return fallback;
  }
}
