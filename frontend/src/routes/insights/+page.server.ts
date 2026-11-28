import { error, redirect } from '@sveltejs/kit';
import type { Cookies } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { identityApiUrl, insightApiUrl } from '$lib/server/api';
import { buildDailyActivity } from '$lib/insight-summary.js';
import { isCalendarDate } from '$lib/validation.js';

interface Employee {
  employeeId: string;
  displayName: string;
  role: string;
}
interface ActivitySummaryDay {
  activityDate: string;
  habitActivityCount: number;
  challengeActivityCount: number;
}

function requireSession(cookies: Cookies): string {
  const token = cookies.get('teampulse_session');
  if (!token) redirect(303, '/login');
  return token;
}

function expireSession(cookies: Cookies): void {
  cookies.delete('teampulse_session', { path: '/' });
}

function utcDate(value: Date): string {
  return value.toISOString().slice(0, 10);
}

export const load: PageServerLoad = async ({ cookies, fetch, url }) => {
  const token = requireSession(cookies);
  const requestedFrom = url.searchParams.get('fromDate');
  const requestedTo = url.searchParams.get('toDate');
  if ((requestedFrom === null) !== (requestedTo === null)) {
    error(400, 'Both fromDate and toDate must be provided.');
  }

  const today = new Date();
  const defaultToDate = utcDate(today);
  const defaultFrom = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate() - 6));
  const fromDate = requestedFrom ?? utcDate(defaultFrom);
  const toDate = requestedTo ?? defaultToDate;
  if (!isCalendarDate(fromDate) || !isCalendarDate(toDate)) {
    error(400, 'Choose valid calendar dates for the activity summary.');
  }
  const fromDay = Date.parse(`${fromDate}T00:00:00.000Z`);
  const toDay = Date.parse(`${toDate}T00:00:00.000Z`);
  const dayDifference = (toDay - fromDay) / 86_400_000;
  if (dayDifference < 0 || dayDifference >= 30) {
    error(400, 'The activity summary date range must be ordered and no longer than 30 days.');
  }
  const headers = { authorization: `Bearer ${token}` };
  let summaryResponse: Response;
  let employeeResponse: Response;

  try {
    [summaryResponse, employeeResponse] = await Promise.all([
      fetch(`${insightApiUrl()}/api/insights/activity/daily?fromDate=${fromDate}&toDate=${toDate}`, { headers }),
      fetch(`${identityApiUrl()}/api/identity/me`, { headers })
    ]);
  } catch {
    error(503, 'Insight or Identity service is unavailable. Check that both APIs are running.');
  }

  if (summaryResponse.status === 401 || employeeResponse.status === 401) {
    expireSession(cookies);
    redirect(303, '/login');
  }
  if (!summaryResponse.ok || !employeeResponse.ok) {
    error(502, 'Your personal activity summary could not be loaded. Please try again.');
  }

  const rawDays = (await summaryResponse.json()) as ActivitySummaryDay[];
  const employee = (await employeeResponse.json()) as Employee;
  return {
    employee,
    fromDate,
    toDate,
    days: buildDailyActivity(rawDays, fromDate, toDate)
  };
};
