import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, Cookies } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { challengeApiUrl, eventTrackingApiUrl, identityApiUrl, responseMessage } from '$lib/server/api';
import { isCalendarDate, isGuid } from '$lib/validation.js';

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
  registrationClosedAtUtc?: string | null;
  teamsFinalizedAtUtc?: string | null;
  createdAtUtc?: string;
  responseStatus?: 'Joined' | 'Declined';
  joinedAtUtc?: string | null;
  teamName?: string | null;
  goalInstructions: string;
  goalTarget: number;
  goalUnit: string;
}
interface ActivityEntry { id: string; challengeId: string; activityDate: string; quantity: number; submittedAtUtc: string; }
interface ActivityProgress { challengeId: string; totalQuantity: number; entries: ActivityEntry[]; }
interface MyChallengeWithProgress extends Challenge {
  progress: ActivityProgress | null;
  progressAvailable: boolean;
  activityKey: string;
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
  const joinedChallenges = (await myResponse.json()) as Challenge[];
  const myChallenges = await Promise.all(joinedChallenges.map(async challenge => {
    const activityKey = globalThis.crypto.randomUUID();
    if (challenge.responseStatus !== 'Joined') {
      return { ...challenge, progress: null, progressAvailable: true, activityKey } as MyChallengeWithProgress;
    }
    try {
      const progressResponse = await fetch(`${eventTrackingApiUrl()}/api/challenge-activities/mine?challengeId=${challenge.id}`, { headers });
      if (progressResponse.status === 401) {
        expireSession(cookies);
        redirect(303, '/login');
      }
      if (!progressResponse.ok) return { ...challenge, progress: null, progressAvailable: false, activityKey } as MyChallengeWithProgress;
      return { ...challenge, progress: (await progressResponse.json()) as ActivityProgress, progressAvailable: true, activityKey } as MyChallengeWithProgress;
    } catch {
      return { ...challenge, progress: null, progressAvailable: false, activityKey } as MyChallengeWithProgress;
    }
  }));
  return {
    employee,
    todayDate: new Date().toISOString().slice(0, 10),
    availableChallenges: (await availableResponse.json()) as Challenge[],
    myChallenges
  };
};

export const actions: Actions = {
  logActivity: async ({ request, cookies, fetch }) => {
    const token = requireSession(cookies);
    const form = await request.formData();
    const challengeId = String(form.get('challengeId') ?? '');
    const idempotencyKey = String(form.get('idempotencyKey') ?? '');
    const activityDate = String(form.get('activityDate') ?? '');
    const quantity = Number(form.get('quantity'));
    if (!isGuid(challengeId) || !isGuid(idempotencyKey) || !isCalendarDate(activityDate) ||
      !Number.isFinite(quantity) || quantity <= 0 || quantity > 1000000 || Math.round(quantity * 100) !== quantity * 100) {
      return fail(400, { message: 'Enter a valid activity date and quantity (greater than zero, up to 1,000,000, max two decimals).' });
    }
    let response: Response;
    try {
      response = await fetch(`${eventTrackingApiUrl()}/api/challenge-activities`, {
        method: 'POST',
        headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' },
        body: JSON.stringify({ challengeId, activityDate, quantity, idempotencyKey })
      });
    } catch {
      return fail(503, { message: 'Activity Tracking service is unavailable. Please try again.' });
    }
    if (response.status === 401) {
      expireSession(cookies);
      redirect(303, '/login');
    }
    if (!response.ok) return fail([400, 403, 404, 409].includes(response.status) ? response.status : 502,
      { message: await responseMessage(response, 'Your challenge activity could not be saved.') });
    return { success: true, message: 'Activity logged. Your challenge progress has been updated.' };
  },
  join: async ({ request, cookies, fetch }) => {
    const token = requireSession(cookies);
    const form = await request.formData();
    const challengeId = String(form.get('challengeId') ?? '');
    if (!isGuid(challengeId)) return fail(400, { message: 'Choose a valid challenge.' });

    let response: Response;
    try {
      response = await fetch(`${challengeApiUrl()}/api/challenges/${challengeId}/response`, {
        method: 'POST',
        headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' },
        body: JSON.stringify({ status: 'Joined' })
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
  },
  decline: async ({ request, cookies, fetch }) => {
    const token = requireSession(cookies);
    const form = await request.formData();
    const challengeId = String(form.get('challengeId') ?? '');
    if (!isGuid(challengeId)) return fail(400, { message: 'Choose a valid challenge.' });

    let response: Response;
    try {
      response = await fetch(`${challengeApiUrl()}/api/challenges/${challengeId}/response`, {
        method: 'POST',
        headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' },
        body: JSON.stringify({ status: 'Declined' })
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
        message: await responseMessage(response, 'Your response could not be saved.')
      });
    }
    return { success: true, message: 'You have declined this challenge.' };
  }
};
