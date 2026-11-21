import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, Cookies } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { habitApiUrl, identityApiUrl, responseMessage } from '$lib/server/api';
import { isGuid } from '$lib/validation.js';

interface Habit {
  id: string;
  name: string;
  description: string | null;
  category: string | null;
  frequency: number;
  targetCount: number;
  startDate: string;
  endDate: string | null;
  status: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  archivedAtUtc: string | null;
}

interface Employee {
  employeeId: string;
  email: string;
  displayName: string;
  organizationId: string;
  teamId: string;
  role: string;
}

interface HabitWriteRequest {
  name: string;
  description: string | null;
  category: string | null;
  frequency: number;
  targetCount: number;
  startDate: string;
  endDate: string | null;
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

function parseHabitForm(formData: FormData): { request: HabitWriteRequest | null; message?: string } {
  const name = String(formData.get('name') ?? '').trim();
  const description = String(formData.get('description') ?? '').trim();
  const category = String(formData.get('category') ?? '').trim();
  const frequency = Number(formData.get('frequency'));
  const targetCount = Number(formData.get('targetCount'));
  const startDate = String(formData.get('startDate') ?? '');
  const endDateValue = String(formData.get('endDate') ?? '');
  const endDate = endDateValue || null;

  if (!name || name.length > 100) {
    return { request: null, message: 'Give your habit a name between 1 and 100 characters.' };
  }
  if (description.length > 500 || category.length > 50) {
    return { request: null, message: 'Description or category is longer than allowed.' };
  }
  if (![1, 2].includes(frequency) || !Number.isInteger(targetCount) || targetCount < 1 || targetCount > 1000) {
    return { request: null, message: 'Choose a valid frequency and a target between 1 and 1,000.' };
  }
  if (!/^\d{4}-\d{2}-\d{2}$/.test(startDate) || (endDate && !/^\d{4}-\d{2}-\d{2}$/.test(endDate))) {
    return { request: null, message: 'Choose a valid start date and optional end date.' };
  }
  if (endDate && endDate < startDate) {
    return { request: null, message: 'The end date cannot be before the start date.' };
  }

  return {
    request: {
      name,
      description: description || null,
      category: category || null,
      frequency,
      targetCount,
      startDate,
      endDate
    }
  };
}

export const load: PageServerLoad = async ({ cookies, fetch }) => {
  const token = requireSession(cookies);
  const headers = { authorization: `Bearer ${token}` };

  let habitsResponse: Response;
  let employeeResponse: Response;
  try {
    [habitsResponse, employeeResponse] = await Promise.all([
      fetch(`${habitApiUrl()}/api/habits`, { headers }),
      fetch(`${identityApiUrl()}/api/identity/me`, { headers })
    ]);
  } catch {
    error(503, 'TeamPulse services are unavailable. Check that Identity and Habit APIs are running.');
  }

  await handleUnauthorized(habitsResponse, cookies);
  await handleUnauthorized(employeeResponse, cookies);

  if (!habitsResponse.ok || !employeeResponse.ok) {
    error(502, 'TeamPulse could not load your workspace. Please try again.');
  }

  return {
    habits: (await habitsResponse.json()) as Habit[],
    employee: (await employeeResponse.json()) as Employee
  };
};

export const actions: Actions = {
  create: async ({ request, cookies, fetch }) => {
    const token = requireSession(cookies);
    const parsed = parseHabitForm(await request.formData());
    if (!parsed.request) {
      return fail(400, { action: 'create', message: parsed.message ?? 'Check the habit details.' });
    }

    let response: Response;
    try {
      response = await fetch(`${habitApiUrl()}/api/habits`, {
        method: 'POST',
        headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' },
        body: JSON.stringify(parsed.request)
      });
    } catch {
      return fail(503, { action: 'create', message: 'Habit service is unavailable. Please try again.' });
    }

    await handleUnauthorized(response, cookies);
    if (!response.ok) {
      return fail(response.status === 400 ? 400 : 502, {
        action: 'create',
        message: await responseMessage(response, 'Your habit could not be saved.')
      });
    }

    return { action: 'create', success: true };
  },

  update: async ({ request, cookies, fetch }) => {
    const token = requireSession(cookies);
    const formData = await request.formData();
    const habitId = String(formData.get('habitId') ?? '');
    if (!isGuid(habitId)) {
      return fail(400, { action: 'update', message: 'Invalid habit selection.' });
    }

    const parsed = parseHabitForm(formData);
    if (!parsed.request) {
      return fail(400, { action: 'update', message: parsed.message ?? 'Check the habit details.' });
    }

    let response: Response;
    try {
      response = await fetch(`${habitApiUrl()}/api/habits/${habitId}`, {
        method: 'PUT',
        headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' },
        body: JSON.stringify(parsed.request)
      });
    } catch {
      return fail(503, { action: 'update', message: 'Habit service is unavailable. Please try again.' });
    }

    await handleUnauthorized(response, cookies);
    if (!response.ok) {
      return fail(response.status === 400 || response.status === 409 ? response.status : 502, {
        action: 'update',
        message: await responseMessage(response, 'Your habit could not be updated.')
      });
    }

    return { action: 'update', success: true };
  },

  archive: async ({ request, cookies, fetch }) => {
    const token = requireSession(cookies);
    const formData = await request.formData();
    const habitId = String(formData.get('habitId') ?? '');
    if (!isGuid(habitId)) {
      return fail(400, { action: 'archive', message: 'Invalid habit selection.' });
    }

    let response: Response;
    try {
      response = await fetch(`${habitApiUrl()}/api/habits/${habitId}/archive`, {
        method: 'POST',
        headers: { authorization: `Bearer ${token}` }
      });
    } catch {
      return fail(503, { action: 'archive', message: 'Habit service is unavailable. Please try again.' });
    }

    await handleUnauthorized(response, cookies);
    if (!response.ok && response.status !== 404) {
      return fail(502, {
        action: 'archive',
        message: await responseMessage(response, 'Your habit could not be archived.')
      });
    }
    return { action: 'archive', success: true };
  }
};
