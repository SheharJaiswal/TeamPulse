import { fail } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { identityApiUrl, responseMessage } from '$lib/server/api';

export const load: PageServerLoad = ({ url, setHeaders }) => {
  // Invitation tokens must not be retained by shared caches or sent as referrers.
  setHeaders({
    'cache-control': 'no-store',
    'referrer-policy': 'no-referrer'
  });

  return { token: url.searchParams.get('token') ?? '' };
};

export const actions: Actions = {
  default: async ({ request, fetch }) => {
    const formData = await request.formData();
    const token = String(formData.get('token') ?? '');
    const password = String(formData.get('password') ?? '');
    const confirmPassword = String(formData.get('confirmPassword') ?? '');

    if (!token) {
      return fail(400, { message: 'This activation link is missing its invitation token.' });
    }
    if (password.length < 12 || password.length > 256) {
      return fail(400, { message: 'Choose a password between 12 and 256 characters.' });
    }
    if (password !== confirmPassword) {
      return fail(400, { message: 'The passwords do not match.' });
    }

    let response: Response;
    try {
      response = await fetch(`${identityApiUrl()}/api/identity/invitations/activate`, {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ token, password })
      });
    } catch {
      return fail(503, { message: 'TeamPulse Identity is unavailable. Please try again shortly.' });
    }

    if (!response.ok) {
      return fail(response.status === 429 ? 429 : 400, {
        message: await responseMessage(
          response,
          'This invitation is invalid or can no longer be used.'
        )
      });
    }

    return { success: true, message: 'Your account is active. You can now sign in.' };
  }
};
