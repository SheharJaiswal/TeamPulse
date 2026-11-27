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
interface Participant {
  employeeId: string;
  displayName: string;
  email: string;
  respondedAtUtc: string;
  teamId: string | null;
  teamName: string | null;
}
interface TeamMember {
  employeeId: string;
  displayName: string;
  email: string;
}
interface Team {
  id: string;
  name: string;
  memberCount: number;
  members: TeamMember[];
}
interface Registration {
  challengeId: string;
  challengeName: string;
  description: string | null;
  startDate: string;
  endDate: string;
  registrationDeadline: string;
  registrationClosedAtUtc: string | null;
  teamsFinalizedAtUtc: string | null;
  goalInstructions: string;
  goalTarget: number;
  goalUnit: string;
  invitedCount: number;
  joinedCount: number;
  declinedCount: number;
  pendingCount: number;
  participants: Participant[];
  teams: Team[];
}
function requireSession(cookies: Cookies): string {
  const token = cookies.get('teampulse_session');
  if (!token) redirect(303, '/login');
  return token;
}
function expireSession(cookies: Cookies): void {
  cookies.delete('teampulse_session', { path: '/' });
}
async function verifyHr(token: string, cookies: Cookies, fetcher: typeof fetch): Promise<void> {
  let response: Response;
  try {
    response = await fetcher(`${identityApiUrl()}/api/identity/me`, { headers: { authorization: `Bearer ${token}` } });
  } catch {
    error(503, 'Identity service is unavailable.');
  }
  if (response.status === 401) {
    expireSession(cookies);
    redirect(303, '/login');
  }
  if (!response.ok) error(502, 'Your TeamPulse profile could not be verified.');
  const employee = (await response.json()) as Employee;
  if (employee.role !== 'HrAdmin') error(403, 'HR administrator access is required.');
}
async function callApi(fetcher: typeof fetch, token: string, url: string, init?: RequestInit): Promise<Response> {
  const headers = new Headers(init?.headers);
  headers.set('authorization', `Bearer ${token}`);
  return fetcher(url, { ...init, headers });
}
export const load: PageServerLoad = async ({ cookies, fetch, params }) => {
  const token = requireSession(cookies);
  await verifyHr(token, cookies, fetch);
  if (!isGuid(params.challengeId)) error(400, 'Invalid challenge identifier.');
  let response: Response;
  try {
    response = await callApi(fetch, token, `${challengeApiUrl()}/api/challenges/${params.challengeId}/registration`);
  } catch {
    error(503, 'Challenge service is unavailable.');
  }
  if (response.status === 401) {
    expireSession(cookies);
    redirect(303, '/login');
  }
  if (!response.ok) error(response.status === 404 ? 404 : response.status === 403 ? 403 : 502, 'Challenge registration could not be loaded.');
  return { registration: (await response.json()) as Registration };
};
export const actions: Actions = {
  close: async ({ cookies, fetch, params }) => {
    const token = requireSession(cookies);
    await verifyHr(token, cookies, fetch);
    const challengeId = params.challengeId ?? '';
    if (!isGuid(challengeId)) return fail(400, { message: 'Invalid challenge identifier.' });
    let response: Response;
    try {
      response = await callApi(fetch, token, `${challengeApiUrl()}/api/challenges/${challengeId}/registration/close`, { method: 'POST' });
    } catch {
      return fail(503, { message: 'Challenge service is unavailable.' });
    }
    if (response.status === 401) {
      expireSession(cookies);
      redirect(303, '/login');
    }
    if (!response.ok) return fail(response.status === 409 ? 409 : 502, { message: await responseMessage(response, 'Registration could not be closed.') });
    return { success: true, message: 'Registration is closed. Pending employees will not be included in team formation.' };
  },
  createTeam: async ({ cookies, fetch, params, request }) => {
    const token = requireSession(cookies);
    await verifyHr(token, cookies, fetch);
    const challengeId = params.challengeId ?? '';
    if (!isGuid(challengeId)) return fail(400, { message: 'Invalid challenge identifier.' });
    const form = await request.formData();
    const name = String(form.get('teamName') ?? '').trim();
    const employeeIds = form.getAll('employeeIds').map(value => String(value));
    if (!name || name.length > 100) return fail(400, { message: 'Team name must contain 1 to 100 characters.' });
    if (employeeIds.length === 0 || employeeIds.some(id => !isGuid(id)) || new Set(employeeIds).size !== employeeIds.length) {
      return fail(400, { message: 'Select at least one joined employee for this team.' });
    }
    let createResponse: Response;
    try {
      createResponse = await callApi(fetch, token, `${challengeApiUrl()}/api/challenges/${challengeId}/teams`, {
        method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ name, employeeIds })
      });
    } catch {
      return fail(503, { message: 'Challenge service is unavailable.' });
    }
    if (createResponse.status === 401) {
      expireSession(cookies);
      redirect(303, '/login');
    }
    if (!createResponse.ok) return fail(createResponse.status === 409 || createResponse.status === 400 ? createResponse.status : 502, {
      message: await responseMessage(createResponse, 'Team could not be created or its members could not be assigned.')
    });
    return { success: true, message: `${name} created with ${employeeIds.length} employees.` };
  },
  renameTeam: async ({ cookies, fetch, params, request }) => {
    const token = requireSession(cookies);
    await verifyHr(token, cookies, fetch);
    const challengeId = params.challengeId ?? '';
    if (!isGuid(challengeId)) return fail(400, { message: 'Invalid challenge identifier.' });
    const form = await request.formData();
    const teamId = String(form.get('teamId') ?? '');
    const name = String(form.get('teamName') ?? '').trim();
    if (!isGuid(teamId)) return fail(400, { message: 'Invalid team identifier.' });
    if (!name || name.length > 100) return fail(400, { message: 'Team name must contain 1 to 100 characters.' });
    let response: Response;
    try {
      response = await callApi(fetch, token, `${challengeApiUrl()}/api/challenges/${challengeId}/teams/${teamId}`, {
        method: 'PUT', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ name })
      });
    } catch { return fail(503, { message: 'Challenge service is unavailable.' }); }
    if (response.status === 401) { expireSession(cookies); redirect(303, '/login'); }
    if (!response.ok) return fail(response.status === 409 || response.status === 400 ? response.status : 502, {
      message: await responseMessage(response, 'Team could not be renamed.')
    });
    return { success: true, message: `Team renamed to ${name}.` };
  },
  deleteTeam: async ({ cookies, fetch, params, request }) => {
    const token = requireSession(cookies);
    await verifyHr(token, cookies, fetch);
    const challengeId = params.challengeId ?? '';
    if (!isGuid(challengeId)) return fail(400, { message: 'Invalid challenge identifier.' });
    const form = await request.formData();
    const teamId = String(form.get('teamId') ?? '');
    if (!isGuid(teamId)) return fail(400, { message: 'Invalid team identifier.' });
    let response: Response;
    try {
      response = await callApi(fetch, token, `${challengeApiUrl()}/api/challenges/${challengeId}/teams/${teamId}`, { method: 'DELETE' });
    } catch { return fail(503, { message: 'Challenge service is unavailable.' }); }
    if (response.status === 401) { expireSession(cookies); redirect(303, '/login'); }
    if (!response.ok) return fail(response.status === 409 ? 409 : 502, {
      message: await responseMessage(response, 'Team could not be deleted.')
    });
    return { success: true, message: 'Team deleted. Its employees are now unassigned and can be assigned to another team.' };
  },
  finalize: async ({ cookies, fetch, params }) => {
    const token = requireSession(cookies);
    await verifyHr(token, cookies, fetch);
    const challengeId = params.challengeId ?? '';
    if (!isGuid(challengeId)) return fail(400, { message: 'Invalid challenge identifier.' });
    let response: Response;
    try {
      response = await callApi(fetch, token, `${challengeApiUrl()}/api/challenges/${challengeId}/teams/finalize`, { method: 'POST' });
    } catch {
      return fail(503, { message: 'Challenge service is unavailable.' });
    }
    if (response.status === 401) {
      expireSession(cookies);
      redirect(303, '/login');
    }
    if (!response.ok) return fail(response.status === 409 ? 409 : 502, { message: await responseMessage(response, 'Teams could not be finalized.') });
    return { success: true, message: 'Teams finalized. Employee team assignments are now locked.' };
  }
};
