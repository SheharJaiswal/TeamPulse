import { dev } from '$app/environment';
import { fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { identityApiUrl, responseMessage } from '$lib/server/api';

interface AuthResponse {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
}

export const load: PageServerLoad = ({ cookies }) => {
  if (cookies.get('teampulse_session')) {
    redirect(303, '/habits');
  }
};

export const actions: Actions = {
  default: async ({ request, fetch, cookies }) => {
    const formData = await request.formData();
    const email = String(formData.get('email') ?? '').trim();
    const password = String(formData.get('password') ?? '');

    if (!email || !password) {
      return fail(400, { message: 'Enter your work email and password.' });
    }

    let response: Response;
    try {
      response = await fetch(`${identityApiUrl()}/api/identity/login`, {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ email, password })
      });
    } catch {
      return fail(503, { message: 'TeamPulse Identity is unavailable. Check that the API is running.' });
    }

    if (!response.ok) {
      return fail(response.status === 401 ? 401 : 400, {
        message: await responseMessage(response, 'Unable to sign in. Please try again.')
      });
    }

    const auth = (await response.json()) as AuthResponse;
    const expiresAt = Date.parse(auth.expiresAtUtc);
    if (!auth.accessToken || !Number.isFinite(expiresAt) || expiresAt <= Date.now()) {
      return fail(502, { message: 'Identity returned an invalid session. Please try again.' });
    }

    cookies.set('teampulse_session', auth.accessToken, {
      path: '/',
      httpOnly: true,
      sameSite: 'lax',
      secure: !dev,
      maxAge: Math.max(1, Math.floor((expiresAt - Date.now()) / 1000))
    });

    redirect(303, '/habits');
  }
};
