import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, Cookies } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { eventTrackingApiUrl, habitApiUrl, responseMessage } from '$lib/server/api';
import { isCalendarDate, isGuid } from '$lib/validation.js';

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

const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const datePattern = /^\d{4}-\d{2}-\d{2}$/;

function isCalendarDate(value: string): boolean {
  if (!datePattern.test(value)) return false;
  const [year, month, day] = value.split('-').map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
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

export const load: PageServerLoad = async ({ cookies, fetch, url }) => {
  const token = requireSession(cookies);
  const habitId = url.searchParams.get('habitId') ?? '';
  const fromDate = url.searchParams.get('fromDate') ?? '';
  const toDate = url.searchParams.get('toDate') ?? '';

  if (habitId && !isGuid(habitId)) error(400, 'Choose a valid habit filter.');
  if ((fromDate && !isCalendarDate(fromDate)) || (toDate && !isCalendarDate(toDate))) {
    error(400, 'Choose valid calendar dates for the activity filter.');
  }
  if (fromDate && toDate && fromDate > toDate) {
    error(400, 'The start date cannot be after the end date.');
  }

  const headers = { authorization: `Bearer ${token}` };
  const query = new URLSearchParams();
  if (habitId) query.set('habitId', habitId);
  if (fromDate) query.set('fromDate', fromDate);
  if (toDate) query.set('toDate', toDate);
  const historyUrl = `${eventTrackingApiUrl()}/api/habit-activities${query.size ? `?${query.toString()}` : ''}`;

  let habitsResponse: Response;
  let activitiesResponse: Response;
  try {
    [habitsResponse, activitiesResponse] = await Promise.all([
      fetch(`${habitApiUrl()}/api/habits`, { headers }),
      fetch(historyUrl, { headers })
    ]);
  } catch {
    error(503, 'TeamPulse services are unavailable. Check that Identity, Habit and Event Tracking APIs are running.');
  }
  await handleUnauthorized(habitsResponse, cookies);
  await handleUnauthorized(activitiesResponse, cookies);
  if (!habitsResponse.ok || !activitiesResponse.ok) {
    error(502, 'TeamPulse could not load your activity history. Please try again.');
  }

  return {
    habits: (await habitsResponse.json()) as Habit[],
    activities: (await activitiesResponse.json()) as Activity[],
    filters: { habitId, fromDate, toDate },
    idempotencyKey: crypto.randomUUID()
  };
};

export const actions: Actions = {
  submit: async ({ request, cookies, fetch }) => {
    const token = requireSession(cookies);
    const formData = await request.formData();
    const habitId = String(formData.get('habitId') ?? '');
    const activityDate = String(formData.get('activityDate') ?? '');
    const quantity = Number(formData.get('quantity'));
    const idempotencyKey = String(formData.get('idempotencyKey') ?? '');

    if (!isGuid(habitId) || !isCalendarDate(activityDate) ||
        !Number.isInteger(quantity) || quantity < 1 || quantity > 1000 ||
        !isGuid(idempotencyKey)) {
      return fail(400, {
        message: 'Choose a habit, a valid activity date and a quantity between 1 and 1,000.',
        idempotencyKey: isGuid(idempotencyKey) ? idempotencyKey : crypto.randomUUID()
      });
    }

    let response: Response;
    try {
      response = await fetch(`${eventTrackingApiUrl()}/api/habit-activities`, {
        method: 'POST',
        headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' },
        body: JSON.stringify({ habitId, activityDate, quantity, idempotencyKey })
      });
    } catch {
      return fail(503, {
        message: 'Event Tracking service is unavailable. Retry this submission safely.',
        idempotencyKey
      });
    }
    await handleUnauthorized(response, cookies);
    if (!response.ok) {
      return fail(response.status === 400 || response.status === 404 || response.status === 409 ? response.status : 502, {
        message: await responseMessage(response, 'Your activity could not be recorded.'),
        idempotencyKey
      });
    }
    return { success: true, idempotencyKey: crypto.randomUUID() };
  }
};
