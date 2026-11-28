<script lang="ts">
  import { onMount } from 'svelte';
  import { goto } from '$app/navigation';
  import { getLocalSevenDayActivityRange } from '$lib/insight-summary.js';
  import type { PageData } from './$types';

  export let data: PageData;

  onMount(() => {
    const { fromDate, toDate } = getLocalSevenDayActivityRange();

    // Activity dates are entered in the employee's browser calendar.
    // Refresh the server load with that same local 7-day range.
    if (data.fromDate !== fromDate || data.toDate !== toDate) {
      void goto(`/insights?fromDate=${fromDate}&toDate=${toDate}`, {
        replaceState: true,
        invalidateAll: true
      });
    }
  });

  $: totalHabits = data.days.reduce((sum, day) => sum + day.habitActivityCount, 0);
  $: totalChallenges = data.days.reduce((sum, day) => sum + day.challengeActivityCount, 0);
  $: totalActivities = totalHabits + totalChallenges;
  $: peak = Math.max(1, ...data.days.map((day) => day.totalActivityCount));

  function dateLabel(value: string, options: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric' }): string {
    const [year, month, day] = value.split('-').map(Number);
    return new Intl.DateTimeFormat(undefined, options).format(new Date(year, month - 1, day));
  }

  function barHeight(value: number): string {
    return value === 0 ? '3%' : `${Math.max(8, value / peak * 100)}%`;
  }
</script>

<svelte:head>
  <title>My activity summary · TeamPulse</title>
  <meta name="description" content="A private daily summary of your habit and challenge activity." />
</svelte:head>

<div class="app-shell">
  <aside class="sidebar">
    <a class="brand-lockup" href="/habits"><span class="brand-mark">✓</span><span>TeamPulse</span></a>
    <p class="nav-label">WORKSPACE</p>
    <a class="nav-link" href="/habits"><span class="nav-icon">◷</span> My habits</a>
    <a class="nav-link" href="/activities"><span class="nav-icon">✓</span> Activity history</a>
    <a class="nav-link active" href="/insights" aria-current="page"><span class="nav-icon">▥</span> Activity summary</a>
    <a class="nav-link" href="/challenges"><span class="nav-icon">✳</span> Challenges</a>
    {#if data.employee.role === 'HrAdmin'}
      <a class="nav-link" href="/hr/challenges"><span class="nav-icon">＋</span> Manage challenges</a>
      <a class="nav-link" href="/hr/invitations"><span class="nav-icon">✉</span> Employee invitations</a>
    {/if}
    <nav class="mobile-workspace-nav" aria-label="Workspace">
      <a class="mobile-workspace-link" href="/habits">My habits</a>
      <a class="mobile-workspace-link" href="/activities">Activity history</a>
      <a class="mobile-workspace-link active" href="/insights" aria-current="page">Activity summary</a>
      <a class="mobile-workspace-link" href="/challenges">Challenges</a>
      {#if data.employee.role === 'HrAdmin'}
        <a class="mobile-workspace-link" href="/hr/challenges">Manage challenges</a>
        <a class="mobile-workspace-link" href="/hr/invitations">Invitations</a>
      {/if}
    </nav>
    <div class="sidebar-note"><span class="note-icon">✳</span><strong>Progress, not perfection.</strong><p>Every small action counts. Your summary is private to you.</p></div>
    <div class="sidebar-profile">
      <div class="avatar">{data.employee.displayName.slice(0, 1).toUpperCase()}</div>
      <div class="profile-copy"><strong>{data.employee.displayName}</strong><span>{data.employee.role}</span></div>
      <form method="POST" action="/logout"><button class="icon-button" aria-label="Sign out" title="Sign out">↗</button></form>
    </div>
  </aside>

  <main class="main-content">
    <header class="topbar">
      <span class="breadcrumb">Workspace / <strong>Activity summary</strong></span>
      <div class="topbar-right"><a class="text-link" href="/activities">Record activity</a><span class="status-dot"></span><span>Personal space</span><div class="avatar small">{data.employee.displayName.slice(0, 1).toUpperCase()}</div></div>
    </header>
    <section class="page-content">
      <div class="welcome-row">
        <div><p class="eyebrow">YOUR DAILY MOMENTUM</p><h1>Activity summary</h1><p class="muted">A private view of your recorded habit and challenge activity for the last 7 days.</p></div>
        <span class="count-pill">{dateLabel(data.fromDate)} – {dateLabel(data.toDate)}</span>
      </div>

      <div class="stats-grid insight-stats">
        <article class="stat-card"><div class="stat-icon mint">✓</div><span class="stat-label">Total activity</span><strong class="stat-value">{totalActivities}</strong><span class="stat-caption">Recorded entries over 7 days</span></article>
        <article class="stat-card"><div class="stat-icon lavender">◷</div><span class="stat-label">Habit activity</span><strong class="stat-value">{totalHabits}</strong><span class="stat-caption">Habit submissions</span></article>
        <article class="stat-card"><div class="stat-icon peach">✳</div><span class="stat-label">Challenge activity</span><strong class="stat-value">{totalChallenges}</strong><span class="stat-caption">Challenge submissions</span></article>
      </div>

      <section class="insight-panel">
        <div class="section-heading"><div><h2>Your week at a glance</h2><p class="muted">Each bar represents recorded activity submissions for that day.</p></div><span class="count-pill">7 days</span></div>
        <div class="activity-chart" role="img" aria-label="Daily activity count for the last seven days">
          {#each data.days as day (day.activityDate)}
            <div class="activity-chart-day">
              <div class="activity-chart-value">{day.totalActivityCount}</div>
              <div class="activity-chart-track"><div class="activity-chart-bar" style={`height: ${barHeight(day.totalActivityCount)}`}><span class="activity-chart-habit" style={`height: ${day.totalActivityCount ? day.habitActivityCount / day.totalActivityCount * 100 : 0}%`}></span></div></div>
              <span class="activity-chart-label">{dateLabel(day.activityDate, { weekday: 'short' })}</span>
              <span class="activity-chart-date">{dateLabel(day.activityDate)}</span>
            </div>
          {/each}
        </div>
        <div class="activity-chart-legend"><span><i class="legend-habit"></i> Habit submissions</span><span><i class="legend-challenge"></i> Challenge submissions</span></div>
      </section>

      <section class="insight-panel">
        <div class="section-heading"><div><h2>Daily breakdown</h2><p class="muted">Counts are submission events, not quantities or goal progress.</p></div></div>
        <div class="insight-daily-list">
          {#each [...data.days].reverse() as day (day.activityDate)}
            <article class="insight-daily-row">
              <div class="insight-daily-date"><strong>{dateLabel(day.activityDate, { weekday: 'short' })}</strong><span>{dateLabel(day.activityDate, { month: 'short', day: 'numeric', year: 'numeric' })}</span></div>
              <div class="insight-daily-metrics"><span><i class="legend-habit"></i> Habits <strong>{day.habitActivityCount}</strong></span><span><i class="legend-challenge"></i> Challenges <strong>{day.challengeActivityCount}</strong></span></div>
              <span class="insight-daily-total">{day.totalActivityCount} total</span>
            </article>
          {/each}
        </div>
      </section>

      <p class="insight-privacy-note">This summary is private to your employee account. It shows only your own activity submissions.</p>
    </section>
  </main>
</div>
