import { isCalendarDate, isGuid } from '../validation.js';

/**
 * @typedef {{ habitId: string, activityDate: string, quantity: number, idempotencyKey: string }} ActivitySubmission
 */

/**
 * Validate and submit one activity while preserving its idempotency key for
 * every outcome where the server has not confirmed acceptance.
 *
 * @param {ActivitySubmission} submission
 * @param {{
 *   send: (payload: ActivitySubmission) => Promise<Response>,
 *   createIdempotencyKey: () => string,
 *   onResponse: (response: Response) => Promise<void>,
 *   getResponseMessage: (response: Response, fallback: string) => Promise<string>
 * }} dependencies
 */
export async function submitActivity(submission, dependencies) {
  const { habitId, activityDate, quantity, idempotencyKey } = submission;

  if (!isGuid(habitId) || !isCalendarDate(activityDate) ||
      !Number.isInteger(quantity) || quantity < 1 || quantity > 1000 ||
      !isGuid(idempotencyKey)) {
    return {
      kind: 'invalid',
      status: 400,
      message: 'Choose a habit, a valid activity date and a quantity between 1 and 1,000.',
      idempotencyKey: isGuid(idempotencyKey) ? idempotencyKey : dependencies.createIdempotencyKey()
    };
  }

  let response;
  try {
    response = await dependencies.send(submission);
  } catch {
    return {
      kind: 'unavailable',
      status: 503,
      message: 'Event Tracking service is unavailable. Retry this submission safely.',
      idempotencyKey
    };
  }

  await dependencies.onResponse(response);

  if (!response.ok) {
    return {
      kind: 'rejected',
      status: [400, 404, 409].includes(response.status) ? response.status : 502,
      message: await dependencies.getResponseMessage(response, 'Your activity could not be recorded.'),
      idempotencyKey
    };
  }

  return {
    kind: 'success',
    status: 200,
    idempotencyKey: dependencies.createIdempotencyKey()
  };
}
