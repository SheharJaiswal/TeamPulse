import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, Cookies } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { challengeApiUrl, identityApiUrl, responseMessage } from '$lib/server/api';
import { isGuid } from '$lib/validation.js';

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
  createdAtUtc?: string;
  joinedAtUtc?: string;
}

function requireSession(cookies: Cookies): string {
  const token = cookies.get('teampulse_session');
  if (!token) redirect(303, '/login');
  return token;
}
function expireSession(cookies: Cookies): void {
  cookies.delete('teampulse_session', { path: '/' });
}
async function getEmployee(token: string, cookies: Cookies, fetcher: typeof fetch): Promise<Employee> {
  let response: Response;
  try {
    response = await fetcher(`${identityApiUrl()}/api/identity/me`, {
      headers: { authorization: `Bearer ${token}` }
    });
  } catch {
    error(503, 'Identity service is unavailable. Please try again.');
  }
  if (response.status === 401) {
    expireSession(cookies);
    redirect(303, '/login');
  }
  if (!response.ok) error(502, 'Your TeamPulse profile could not be verified.');
  const employee = (await response.json()) as Employee;
  if (!['Employee', 'HrAdmin'].includes(employee.role)) error(403, 'Employee workspace access is required.');
  return employee;
}

export const load: PageServerLoad = async ({ cookies, fetch }) => {
  const token = requireSession(cookies);
  const employee = await getEmployee(token, cookies, fetch);
  const headers = { authorization: `Bearer ${token}` };
  let availableResponse: Response;
  let myResponse: Response;
  try {
    [availableResponse, myResponse] = await Promise.all([
      fetch(`${challengeApiUrl()}/api/challenges/available`, { headers }),
      fetch(`${challengeApiUrl()}/api/challenges/my`, { headers })
    ]);
  } catch {
    error(503, 'Challenge service is unavailable. Check that the Challenge API is running.');
  }
  if (availableResponse.status === 401 || myResponse.status === 401) {
    expireSession(cookies);
    redirect(303, '/login');
  }
  if (!availableResponse.ok || !myResponse.ok) {
    const status = availableResponse.status === 403 || myResponse.status === 403 ? 403 : 502;
    error(status, 'Your challenges could not be loaded. Please try again.');
  }
  return {
    employee,
    availableChallenges: (await availableResponse.json()) as Challenge[],
    myChallenges: (await myResponse.json()) as Challenge[]
  };
};

export const actions: Actions = {
  join: async ({ request, cookies, fetch }) => {
    const token = requireSession(cookies);
    const form = await request.formData();
    const challengeId = String(form.get('challengeId') ?? '');
    if (!isGuid(challengeId)) return fail(400, { message: 'Choose a valid challenge.' });

    let response: Response;
    try {
      response = await fetch(`${challengeApiUrl()}/api/challenges/${challengeId}/join`, {
        method: 'POST',
        headers: { authorization: `Bearer ${token}` }
      });
    } catch {
      return fail(503, { message: 'Challenge service is unavailable. Please try again.' });
    }
    if (response.status === 401) {
      expireSession(cookies);
      redirect(303, '/login');
    }
    if (!response.ok) {
      return fail([400, 403, 404, 409].includes(response.status) ? response.status : 502, {
        message: await responseMessage(response, 'You could not join this challenge.')
      });
    }
    return { success: true, message: 'You have joined the challenge.' };
  }
};
