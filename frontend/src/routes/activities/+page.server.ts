import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, Cookies } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { eventTrackingApiUrl, habitApiUrl, identityApiUrl, responseMessage } from '$lib/server/api';
import { isCalendarDate, isGuid } from '$lib/validation.js';
import { submitActivity } from '$lib/server/activity-submission.js';

interface Habit {
  id: string;
  name: string;
  startDate: string;
  endDate: string | null;
  status: number;
}

interface Employee {
  employeeId: string;
  email: string;
  displayName: string;
  organizationId: string;
  teamId: string;
  role: string;
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
  let employeeResponse: Response;
  try {
    [habitsResponse, activitiesResponse] = await Promise.all([
      fetch(`${habitApiUrl()}/api/habits`, { headers }),
      fetch(historyUrl, { headers }),
      fetch(`${identityApiUrl()}/api/identity/me`, { headers })
    ]);
  } catch {
    error(503, 'TeamPulse services are unavailable. Check that Identity, Habit and Event Tracking APIs are running.');
  }
  await handleUnauthorized(habitsResponse, cookies);
  await handleUnauthorized(activitiesResponse, cookies);
  await handleUnauthorized(employeeResponse, cookies);
  if (!habitsResponse.ok || !activitiesResponse.ok || !employeeResponse.ok) {
    error(502, 'TeamPulse could not load your activity history. Please try again.');
  }

  return {
    habits: (await habitsResponse.json()) as Habit[],
    employee: (await employeeResponse.json()) as Employee,
    activities: (await activitiesResponse.json()) as Activity[],
    filters: { habitId, fromDate, toDate },
    idempotencyKey: crypto.randomUUID()
  };
};

export const actions: Actions = {
  submit: async ({ request, cookies, fetch }) => {
    const token = requireSession(cookies);
    const formData = await request.formData();
    const submission = {
      habitId: String(formData.get('habitId') ?? ''),
      activityDate: String(formData.get('activityDate') ?? ''),
      quantity: Number(formData.get('quantity')),
      idempotencyKey: String(formData.get('idempotencyKey') ?? '')
    };

    const result = await submitActivity(submission, {
      createIdempotencyKey: () => crypto.randomUUID(),
      send: (payload) => fetch(`${eventTrackingApiUrl()}/api/habit-activities`, {
        method: 'POST',
        headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' },
        body: JSON.stringify(payload)
      }),
      onResponse: (response) => handleUnauthorized(response, cookies),
      getResponseMessage: responseMessage
    });

    if (result.kind === 'success') {
      return { success: true, idempotencyKey: result.idempotencyKey };
    }

    return fail(result.status, {
      message: result.message,
      idempotencyKey: result.idempotencyKey
    });
  }
};
