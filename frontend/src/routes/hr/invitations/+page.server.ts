import { error, fail, redirect } from '@sveltejs/kit';
import type { Actions, Cookies } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { identityApiUrl, responseMessage } from '$lib/server/api';

interface Employee {
  employeeId: string;
  email: string;
  displayName: string;
  organizationId: string;
  teamId: string | null;
  role: string;
}

interface Invitation {
  invitationId: string;
  email: string;
  displayName: string;
  organizationId: string;
  teamId: string | null;
  status: string;
  createdAtUtc: string;
  expiresAtUtc: string;
  deliveryGeneration: number;
}

function requireSession(cookies: Cookies): string {
  const token = cookies.get('teampulse_session');
  if (!token) redirect(303, '/login');
  return token;
}

function expireSession(cookies: Cookies): void {
  cookies.delete('teampulse_session', { path: '/' });
}

async function requireHrAdmin(
  token: string,
  cookies: Cookies,
  fetcher: typeof fetch
): Promise<Employee> {
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
  if (employee.role !== 'HrAdmin') error(403, 'HR administrator access is required.');
  return employee;
}

function upstreamFailureStatus(status: number): number {
  return [400, 403, 404, 409, 410, 422, 429].includes(status) ? status : 502;
}

async function invitationAction(
  invitationId: string,
  action: 'resend' | 'cancel' | 'inspect',
  token: string,
  cookies: Cookies,
  fetcher: typeof fetch
): Promise<{ invitation?: Invitation; message?: string; status?: number }> {
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(invitationId)) {
    return { status: 400, message: 'Enter a valid invitation ID.' };
  }

  const suffix = action === 'inspect' ? '' : `/${action}`;
  let response: Response;
  try {
    response = await fetcher(
      `${identityApiUrl()}/api/identity/invitations/${invitationId}${suffix}`,
      {
        method: action === 'inspect' ? 'GET' : 'POST',
        headers: { authorization: `Bearer ${token}` }
      }
    );
  } catch {
    return { status: 503, message: 'Identity service is unavailable. Please try again.' };
  }

  if (response.status === 401) {
    expireSession(cookies);
    redirect(303, '/login');
  }
  if (!response.ok) {
    return {
      status: upstreamFailureStatus(response.status),
      message: await responseMessage(response, 'The invitation could not be updated.')
    };
  }
  return { invitation: (await response.json()) as Invitation };
}

export const load: PageServerLoad = async ({ cookies, fetch }) => {
  const token = requireSession(cookies);
  const employee = await requireHrAdmin(token, cookies, fetch);
  return { employee };
};

export const actions: Actions = {
  create: async ({ request, cookies, fetch }) => {
    const token = requireSession(cookies);
    await requireHrAdmin(token, cookies, fetch);
    const formData = await request.formData();
    const email = String(formData.get('email') ?? '').trim();
    const displayName = String(formData.get('displayName') ?? '').trim();
    const organizationId = String(formData.get('organizationId') ?? '').trim();
    const teamId = String(formData.get('teamId') ?? '').trim();

    if (!email || email.length > 320 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      return fail(400, { action: 'create', message: 'Enter a valid work email address.' });
    }
    if (!displayName || displayName.length > 150 || !organizationId || organizationId.length > 100 ||
        teamId.length > 100) {
      return fail(400, { action: 'create', message: 'Name and organization are required. Team is optional and cannot exceed 100 characters.' });
    }

    let response: Response;
    try {
      response = await fetch(`${identityApiUrl()}/api/identity/invitations`, {
        method: 'POST',
        headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' },
        body: JSON.stringify({ email, displayName, organizationId, teamId: teamId || null })
      });
    } catch {
      return fail(503, { action: 'create', message: 'Identity service is unavailable. Please try again.' });
    }

    if (response.status === 401) {
      expireSession(cookies);
      redirect(303, '/login');
    }
    if (!response.ok) {
      return fail(response.status === 409 ? 409 : response.status === 400 ? 400 : 502, {
        action: 'create',
        message: await responseMessage(response, 'The invitation could not be created.')
      });
    }
    return { action: 'create', invitation: (await response.json()) as Invitation };
  },

  inspect: async ({ request, cookies, fetch }) => {
    const token = requireSession(cookies);
    await requireHrAdmin(token, cookies, fetch);
    const formData = await request.formData();
    const result = await invitationAction(
      String(formData.get('invitationId') ?? '').trim(), 'inspect', token, cookies, fetch
    );
    if (result.message) return fail(result.status ?? 502, { action: 'inspect', message: result.message });
    return { action: 'inspect', invitation: result.invitation };
  },

  resend: async ({ request, cookies, fetch }) => {
    const token = requireSession(cookies);
    await requireHrAdmin(token, cookies, fetch);
    const formData = await request.formData();
    const result = await invitationAction(
      String(formData.get('invitationId') ?? '').trim(), 'resend', token, cookies, fetch
    );
    if (result.message) return fail(result.status ?? 502, { action: 'resend', message: result.message });
    return { action: 'resend', invitation: result.invitation };
  },

  cancel: async ({ request, cookies, fetch }) => {
    const token = requireSession(cookies);
    await requireHrAdmin(token, cookies, fetch);
    const formData = await request.formData();
    const result = await invitationAction(
      String(formData.get('invitationId') ?? '').trim(), 'cancel', token, cookies, fetch
    );
    if (result.message) return fail(result.status ?? 502, { action: 'cancel', message: result.message });
    return { action: 'cancel', invitation: result.invitation };
  }
};
