import { randomUUID } from 'node:crypto';
import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, Cookies } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { identityApiUrl, responseMessage } from '$lib/server/api';

interface Employee {
  employeeId: string;
  displayName: string;
  organizationId: string;
  role: string;
}
interface LifecycleResult {
  outcome: 'transitioned' | 'alreadyInDesiredState' | 'replayed' | 'unknown';
  employeeId: string;
  isActive: boolean;
  lifecycleVersion: number;
  eventId: string | null;
  correlationId: string;
}

function requireSession(cookies: Cookies): string {
  const token = cookies.get('teampulse_session');
  if (!token) redirect(303, '/login');
  return token;
}
function expireSession(cookies: Cookies): void {
  cookies.delete('teampulse_session', { path: '/' });
}
async function requireHrAdmin(token: string, cookies: Cookies, fetcher: typeof fetch): Promise<Employee> {
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
const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export const load: PageServerLoad = async ({ cookies, fetch }) => {
  const token = requireSession(cookies);
  const employee = await requireHrAdmin(token, cookies, fetch);
  return { employee };
};

export const actions: Actions = {
  deactivate: async ({ request, cookies, fetch }) => changeStatus(false, request, cookies, fetch),
  reactivate: async ({ request, cookies, fetch }) => changeStatus(true, request, cookies, fetch)
};

async function changeStatus(
  desiredIsActive: boolean, request: Request, cookies: Cookies, fetcher: typeof fetch
) {
  const token = requireSession(cookies);
  await requireHrAdmin(token, cookies, fetcher);
  const form = await request.formData();
  const employeeId = String(form.get('employeeId') ?? '').trim();
  const reasonCode = String(form.get('reasonCode') ?? '').trim();
  const note = String(form.get('note') ?? '').trim();

  if (!uuidPattern.test(employeeId)) {
    return fail(400, { action: desiredIsActive ? 'reactivate' : 'deactivate', message: 'Enter a valid employee ID (UUID).' });
  }
  if (!reasonCode || reasonCode.length > 100) {
    return fail(400, { action: desiredIsActive ? 'reactivate' : 'deactivate', message: 'Reason code is required and must be 1–100 characters.' });
  }
  if (note.length > 1000) {
    return fail(400, { action: desiredIsActive ? 'reactivate' : 'deactivate', message: 'Note cannot exceed 1,000 characters.' });
  }

  let response: Response;
  try {
    response = await fetcher(`${identityApiUrl()}/api/identity/employees/${employeeId}/${desiredIsActive ? 'reactivate' : 'deactivate'}`, {
      method: 'POST',
      headers: {
        authorization: `Bearer ${token}`,
        'content-type': 'application/json',
        'Idempotency-Key': randomUUID()
      },
      body: JSON.stringify({ reasonCode, note: note || null })
    });
  } catch {
    return fail(503, { action: desiredIsActive ? 'reactivate' : 'deactivate', message: 'Identity service is unavailable. Please try again.' });
  }
  if (response.status === 401) {
    expireSession(cookies);
    redirect(303, '/login');
  }
  if (!response.ok) {
    return fail(response.status === 400 || response.status === 404 || response.status === 409 ? response.status : 502, {
      action: desiredIsActive ? 'reactivate' : 'deactivate',
      message: await responseMessage(response, 'Employee status could not be changed.')
    });
  }
  return {
    action: desiredIsActive ? 'reactivate' : 'deactivate',
    result: (await response.json()) as LifecycleResult
  };
}
