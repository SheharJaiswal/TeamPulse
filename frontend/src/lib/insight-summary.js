/**
 * Fill a date interval with zero-value days so charts don't skip dates without activity.
 * Dates are calendar values in UTC, matching the Insight API's DateOnly range.
 * @param {{activityDate:string,habitActivityCount:number,challengeActivityCount:number}[]} records
 * @param {string} fromDate
 * @param {string} toDate
 */
export function buildDailyActivity(records, fromDate, toDate) {
  const byDate = new Map(records.map((record) => [record.activityDate, record]));
  const days = [];
  const cursor = new Date(`${fromDate}T00:00:00.000Z`);
  const end = new Date(`${toDate}T00:00:00.000Z`);

  while (cursor <= end) {
    const activityDate = cursor.toISOString().slice(0, 10);
    const record = byDate.get(activityDate);
    const habitActivityCount = record?.habitActivityCount ?? 0;
    const challengeActivityCount = record?.challengeActivityCount ?? 0;
    days.push({
      activityDate,
      habitActivityCount,
      challengeActivityCount,
      totalActivityCount: habitActivityCount + challengeActivityCount
    });
    cursor.setUTCDate(cursor.getUTCDate() + 1);
  }

  return days;
}
