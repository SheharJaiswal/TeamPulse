const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const datePattern = /^\d{4}-\d{2}-\d{2}$/;

/** @param {string} value */
export function isGuid(value) {
  return guidPattern.test(value);
}

/** @param {string} value */
export function isCalendarDate(value) {
  if (!datePattern.test(value)) return false;
  const [year, month, day] = value.split('-').map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year &&
    date.getUTCMonth() === month - 1 &&
    date.getUTCDate() === day;
}

/** @param {Date} [date] */
export function toLocalDateInputValue(date = new Date()) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/**
 * API DateTime values represent UTC. Treat an ISO timestamp without an explicit
 * zone as UTC too, since MySQL DateTime columns can serialize without a suffix.
 * The returned Date can then be formatted in the browser's own time zone.
 * @param {string} value
 */
export function parseUtcTimestamp(value) {
  const hasZone = /(?:Z|[+-]\\d{2}:?\\d{2})$/i.test(value);
  return new Date(hasZone ? value : `${value}Z`);
}
