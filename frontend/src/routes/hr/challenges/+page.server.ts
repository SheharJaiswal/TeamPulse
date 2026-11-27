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
  registrationDeadline: string;
  registrationClosedAtUtc: string | null;
  teamsFinalizedAtUtc: string | null;
  createdAtUtc: string;
  goalInstructions: string;
  goalTarget: number;
  goalUnit: string;
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
    const registrationDeadline = String(form.get('registrationDeadline') ?? '');
    const goalInstructions = String(form.get('goalInstructions') ?? '').trim();
    const goalTarget = Number(form.get('goalTarget'));
    const goalUnit = String(form.get('goalUnit') ?? '').trim();
    if (!name || name.length > 120) return fail(400, { message: 'Challenge name must contain 1 to 120 characters.' });
    if (description.length > 1000) return fail(400, { message: 'Description cannot exceed 1,000 characters.' });
    if (!goalInstructions || goalInstructions.length > 500 || !Number.isFinite(goalTarget) || goalTarget <= 0 || goalTarget > 1000000 || Math.abs(goalTarget * 100 - Math.round(goalTarget * 100)) > 0.000001 || !goalUnit || goalUnit.length > 30) {
      return fail(400, { message: 'Enter action instructions (1–500 characters), a target up to 1,000,000 with at most two decimals, and a unit (1–30 characters).' });
    }
    if (!isCalendarDate(startDate) || !isCalendarDate(endDate) || !isCalendarDate(registrationDeadline) ||
      endDate < startDate || registrationDeadline < new Date().toISOString().slice(0, 10) || registrationDeadline > startDate) {
      return fail(400, { message: 'Choose valid dates. Registration deadline must be today or before the challenge start date.' });
    }
    let response: Response;
    try {
      response = await fetch(`${challengeApiUrl()}/api/challenges`, {
        method: 'POST',
        headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' },
        body: JSON.stringify({ name, description: description || null, startDate, endDate, registrationDeadline, goalInstructions, goalTarget, goalUnit })
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
