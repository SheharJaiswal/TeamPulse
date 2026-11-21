import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, Cookies } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { eventTrackingApiUrl, habitApiUrl, responseMessage } from '$lib/server/api';

interface Habit {
  id: string;
  name: string;
  startDate: string;
  endDate: string | null;
  status: number;
}

interface Activity {
  id: string;
  habitId: string;
  activityDate: string;
  quantity: number;
  submittedAtUtc: string;
}

function requireSession(cookies: Cookies): string {
  const token = cookies.get('teampulse_session');
  if (!token) redirect(303, '/login');
  return token;
}

function expireSession(cookies: Cookies): void {
  cookies.delete('teampulse_session', { path: '/' });
}

async function handleUnauthorized(response: Response, cookies: Cookies): Promise<void> {
  if (response.status === 401) {
    expireSession(cookies);
    redirect(303, '/login');
  }
}

export const load: PageServerLoad = async ({ cookies, fetch }) => {
  const token = requireSession(cookies);
  const headers = { authorization: `Bearer ${token}` };
  let habitsResponse: Response;
  let activitiesResponse: Response;
  try {
    [habitsResponse, activitiesResponse] = await Promise.all([
      fetch(`${habitApiUrl()}/api/habits`, { headers }),
      fetch(`${eventTrackingApiUrl()}/api/habit-activities`, { headers })
    ]);
  } catch {
    error(503, 'TeamPulse services are unavailable. Check that Identity, Habit and Event Tracking APIs are running.');
  }
  await handleUnauthorized(habitsResponse, cookies);
  await handleUnauthorized(activitiesResponse, cookies);
  if (!habitsResponse.ok || !activitiesResponse.ok) {
    error(502, 'TeamPulse could not load your activity history. Please try again.');
  }
  const habits = (await habitsResponse.json()) as Habit[];
  const activities = (await activitiesResponse.json()) as Activity[];
  return { habits, activities };
};

export const actions: Actions = {
  submit: async ({ request, cookies, fetch }) => {
    const token = requireSession(cookies);
    const formData = await request.formData();
    const habitId = String(formData.get('habitId') ?? '');
    const activityDate = String(formData.get('activityDate') ?? '');
    const quantity = Number(formData.get('quantity'));
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(habitId) ||
        !/^\d{4}-\d{2}-\d{2}$/.test(activityDate) ||
        !Number.isInteger(quantity) || quantity < 1 || quantity > 1000) {
      return fail(400, { message: 'Choose a habit, activity date and quantity between 1 and 1,000.' });
    }

    let response: Response;
    try {
      response = await fetch(`${eventTrackingApiUrl()}/api/habit-activities`, {
        method: 'POST',
        headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' },
        body: JSON.stringify({ habitId, activityDate, quantity, idempotencyKey: crypto.randomUUID() })
      });
    } catch {
      return fail(503, { message: 'Event Tracking service is unavailable. Please try again.' });
    }
    await handleUnauthorized(response, cookies);
    if (!response.ok) {
      return fail(response.status === 400 ? 400 : response.status === 404 ? 404 : 502, {
        message: await responseMessage(response, 'Your activity could not be recorded.')
      });
    }
    return { success: true };
  }
};
