import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, Cookies } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { challengeApiUrl, identityApiUrl, responseMessage } from '$lib/server/api';
import { isCalendarDate } from '$lib/validation.js';

interface Employee {
  employeeId: string;
  displayName: string;
  organizationId: string;
  role: string;
}
interface Challenge {
  id: string;
  name: string;
  description: string | null;
  startDate: string;
  endDate: string;
  createdAtUtc: string;
}

function requireSession(cookies: Cookies): string {
  const token = cookies.get('teampulse_session');
  if (!token) redirect(303, '/login');
  return token;
}
function expireSession(cookies: Cookies): void {
  cookies.delete('teampulse_session', { path: '/' });
}
async function getHrEmployee(token: string, cookies: Cookies, fetcher: typeof fetch): Promise<Employee> {
  let response: Response;
  try {
    response = await fetcher(`${identityApiUrl()}/api/identity/me`, { headers: { authorization: `Bearer ${token}` } });
  } catch {
    error(503, 'Identity service is unavailable. Please try again.');
  }
  if (response.status === 401) {
    expireSession(cookies);
    redirect(303, '/login');
  }
  if (!response.ok) error(502, 'Your TeamPulse profile could not be verified.');
  const employee = (await response.json()) as Employee;
  if (employee.role !== 'HrAdmin') error(403, 'HR administrator access is required.');
  return employee;
}

export const load: PageServerLoad = async ({ cookies, fetch }) => {
  const token = requireSession(cookies);
  const employee = await getHrEmployee(token, cookies, fetch);
  let response: Response;
  try {
    response = await fetch(`${challengeApiUrl()}/api/challenges`, { headers: { authorization: `Bearer ${token}` } });
  } catch {
    error(503, 'Challenge service is unavailable. Check that the Challenge API is running.');
  }
  if (response.status === 401) {
    expireSession(cookies);
    redirect(303, '/login');
  }
  if (!response.ok) error(response.status === 403 ? 403 : 502, 'Challenges could not be loaded.');
  return { employee, challenges: (await response.json()) as Challenge[] };
};

export const actions: Actions = {
  create: async ({ request, cookies, fetch }) => {
    const token = requireSession(cookies);
    await getHrEmployee(token, cookies, fetch);
    const form = await request.formData();
    const name = String(form.get('name') ?? '').trim();
    const description = String(form.get('description') ?? '').trim();
    const startDate = String(form.get('startDate') ?? '');
    const endDate = String(form.get('endDate') ?? '');
    if (!name || name.length > 120) return fail(400, { message: 'Challenge name must contain 1 to 120 characters.' });
    if (description.length > 1000) return fail(400, { message: 'Description cannot exceed 1,000 characters.' });
    if (!isCalendarDate(startDate) || !isCalendarDate(endDate) || endDate < startDate) {
      return fail(400, { message: 'Choose valid start and end dates. End date cannot be before start date.' });
    }
    let response: Response;
    try {
      response = await fetch(`${challengeApiUrl()}/api/challenges`, {
        method: 'POST',
        headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' },
        body: JSON.stringify({ name, description: description || null, startDate, endDate })
      });
    } catch {
      return fail(503, { message: 'Challenge service is unavailable. Please try again.' });
    }
    if (response.status === 401) {
      expireSession(cookies);
      redirect(303, '/login');
    }
    if (!response.ok) return fail(response.status === 400 ? 400 : response.status === 403 ? 403 : 502,
      { message: await responseMessage(response, 'Challenge could not be created.') });
    return { success: true };
  }
};
