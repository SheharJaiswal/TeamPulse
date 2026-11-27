<script lang="ts">
  import { enhance } from '$app/forms';
  import { onMount } from 'svelte';
  import { parseUtcTimestamp, toLocalDateInputValue } from '$lib/validation.js';
  import type { ActionData, PageData } from './$types';

  export let data: PageData;
  export let form: ActionData;
  let isJoining = false;
  let localTodayDate = localTodayDate;

  onMount(() => {
    // Date-only challenge values stay calendar dates; activity input defaults
    // follow the employee's browser time zone rather than the server's UTC day.
    localTodayDate = toLocalDateInputValue();
  });

  function handleEnhance() {
    isJoining = true;
    return async ({ update }: { update: () => Promise<void> }) => {
      try {
        await update();
      } finally {
        isJoining = false;
      }
    };
  }

  $: formMessage = form && 'message' in form ? form.message : undefined;
  $: successMessage = form && 'success' in form && form.success
    ? ('message' in form ? form.message : 'You have joined the challenge.')
    : undefined;

  function joinedDateLabel(value: string): string {
    const date = parseUtcTimestamp(value);
    return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(date);
  }

  function dateLabel(value: string): string {
    const [year, month, day] = value.split('-').map(Number);
    return new Intl.DateTimeFormat('en', { month: 'short', day: 'numeric', year: 'numeric' })
      .format(new Date(year, month - 1, day));
  }
</script>

<svelte:head>
  <title>Challenges · TeamPulse</title>
  <meta name="description" content="Discover and join organization wellbeing challenges." />
</svelte:head>

<div class="app-shell">
  <aside class="sidebar">
    <a class="brand-lockup" href="/habits"><span class="brand-mark">✓</span><span>TeamPulse</span></a>
    <p class="nav-label">WORKSPACE</p>
    <a class="nav-link" href="/habits"><span class="nav-icon">◷</span> My habits</a>
    <a class="nav-link" href="/activities"><span class="nav-icon">✓</span> Activity history</a>
    <a class="nav-link active" href="/challenges" aria-current="page"><span class="nav-icon">✳</span> Challenges</a>
    {#if data.employee.role === 'HrAdmin'}
      <a class="nav-link" href="/hr/challenges"><span class="nav-icon">＋</span> Manage challenges</a>
      <a class="nav-link" href="/hr/invitations"><span class="nav-icon">✉</span> Employee invitations</a>
    {/if}
    <nav class="mobile-workspace-nav" aria-label="Workspace">
      <a class="mobile-workspace-link" href="/habits">My habits</a>
      <a class="mobile-workspace-link" href="/activities">Activity history</a>
      <a class="mobile-workspace-link active" href="/challenges" aria-current="page">Challenges</a>
      {#if data.employee.role === 'HrAdmin'}
        <a class="mobile-workspace-link" href="/hr/challenges">Manage</a>
      {/if}
    </nav>
    <div class="sidebar-note"><span class="note-icon">✳</span><strong>Grow together.</strong><p>Take part in shared wellbeing goals with your team.</p></div>
    <div class="sidebar-profile">
      <div class="avatar">{data.employee.displayName.slice(0, 1).toUpperCase()}</div>
      <div class="profile-copy"><strong>{data.employee.displayName}</strong><span>{data.employee.role}</span></div>
      <form method="POST" action="/logout"><button class="icon-button" aria-label="Sign out" title="Sign out">↗</button></form>
    </div>
  </aside>

  <main class="main-content">
    <header class="topbar"><span class="breadcrumb">Workspace / <strong>Challenges</strong></span><div class="topbar-right"><span class="status-dot"></span><span>Team wellbeing</span><div class="avatar small">{data.employee.displayName.slice(0, 1).toUpperCase()}</div></div></header>
    <section class="page-content challenge-page">
      <div class="welcome-row"><div><p class="eyebrow">TEAM WELLBEING</p><h1>Challenges</h1><p class="muted">Join a shared goal and take small steps together.</p></div><span class="count-pill">{data.myChallenges.length} responses</span></div>

      {#if formMessage}<div class:success={Boolean(successMessage)} class="alert" role={successMessage ? 'status' : 'alert'}>{formMessage}</div>{/if}

      <div class="section-heading"><div><h2>Available challenges</h2><p class="muted">Challenges open to employees in your organization.</p></div><span class="count-pill">{data.availableChallenges.length} available</span></div>
      {#if data.availableChallenges.length === 0}
        <section class="empty-state"><div class="empty-illustration">✳</div><h3>You're all caught up</h3><p>There are no open challenges to join right now. Check back when HR publishes another challenge.</p></section>
      {:else}
        <div class="habit-grid">
          {#each data.availableChallenges as challenge (challenge.id)}
            <article class="habit-card challenge-card">
              <div class="habit-card-top"><span class="category-pill">Organization challenge</span><span class="active-indicator"><i></i> Open to join</span></div>
              <h3>{challenge.name}</h3>
              <p class="habit-description">{challenge.description || 'Take part in this organization challenge.'}</p><div class="challenge-goal"><strong>Action:</strong> {challenge.goalInstructions}<span>Goal: {challenge.goalTarget} {challenge.goalUnit}</span></div>
              <div class="habit-details"><div><span class="detail-label">STARTS</span><strong>{dateLabel(challenge.startDate)}</strong></div><div><span class="detail-label">ENDS</span><strong>{dateLabel(challenge.endDate)}</strong></div></div>
              <div class="habit-footer"><span>Respond by {dateLabel(challenge.registrationDeadline)}.</span><div class="response-actions"><form method="POST" action="?/join" use:enhance={handleEnhance} aria-busy={isJoining}><input type="hidden" name="challengeId" value={challenge.id} /><button class="primary-button" type="submit" disabled={isJoining}>{isJoining ? 'Saving…' : 'Join challenge'}</button></form><form method="POST" action="?/decline" use:enhance={handleEnhance} aria-busy={isJoining}><input type="hidden" name="challengeId" value={challenge.id} /><button class="secondary-button" type="submit" disabled={isJoining}>Decline</button></form></div></div>
            </article>
          {/each}
        </div>
      {/if}

      <section class="my-challenges-section">
        <div class="section-heading"><div><h2>My responses</h2><p class="muted">Your acknowledgement and assigned challenge team.</p></div><span class="count-pill">{data.myChallenges.length} responses</span></div>
        {#if data.myChallenges.length === 0}
          <section class="empty-state"><div class="empty-illustration">✓</div><h3>Your challenge journey starts here</h3><p>Your Join or Decline acknowledgement will appear here.</p></section>
        {:else}
          <div class="habit-grid">
            {#each data.myChallenges as challenge (challenge.id)}
              <article class="habit-card"><div class="habit-card-top"><span class="category-pill">Challenge response</span><span class="active-indicator"><i></i> {challenge.responseStatus}</span></div><h3>{challenge.name}</h3><p class="habit-description">{challenge.description || 'You are taking part in this organization challenge.'}</p><div class="challenge-goal"><strong>Action:</strong> {challenge.goalInstructions}<span>Goal: {challenge.goalTarget} {challenge.goalUnit}</span></div><div class="habit-details"><div><span class="detail-label">STARTS</span><strong>{dateLabel(challenge.startDate)}</strong></div><div><span class="detail-label">ENDS</span><strong>{dateLabel(challenge.endDate)}</strong></div></div>{#if challenge.responseStatus === 'Joined'}
                <section class="challenge-progress" aria-label="Challenge progress">
                  <div class="progress-heading"><strong>Your progress</strong><span>{challenge.progressAvailable && challenge.progress ? `${challenge.progress.totalQuantity} / ${challenge.goalTarget} ${challenge.goalUnit}` : 'Progress unavailable'}</span></div>
                  {#if challenge.progressAvailable && challenge.progress}
                    <div class="progress-track" role="progressbar" aria-valuemin="0" aria-valuemax={challenge.goalTarget} aria-valuenow={Math.min(challenge.goalTarget, challenge.progress.totalQuantity)}><div class="progress-fill" style={`width: ${Math.min(100, challenge.progress.totalQuantity / challenge.goalTarget * 100)}%`}></div></div>
                    {#if challenge.progress.totalQuantity >= challenge.goalTarget}
                      <p class="progress-status complete">✓ Challenge complete — goal reached!</p>
                    {:else}
                      <p class="progress-status">{Math.max(0, challenge.goalTarget - challenge.progress.totalQuantity)} {challenge.goalUnit} left to complete.</p>
                    {/if}
                    {#if challenge.progress.entries.length}
                      <details class="activity-history"><summary>Recent activity ({challenge.progress.entries.length})</summary><ul>{#each challenge.progress.entries.slice(0, 4) as entry (entry.id)}<li>{entry.activityDate}: +{entry.quantity} {challenge.goalUnit}</li>{/each}</ul></details>
                    {/if}
                    {#if challenge.progress.totalQuantity < challenge.goalTarget && localTodayDate >= challenge.startDate && localTodayDate <= challenge.endDate}
                      <form class="log-activity-form" method="POST" action="?/logActivity" use:enhance={handleEnhance} aria-busy={isJoining}>
                        <input type="hidden" name="challengeId" value={challenge.id} />
                        <input type="hidden" name="idempotencyKey" value={challenge.activityKey} />
                        <label>Activity date<input type="date" name="activityDate" value={localTodayDate} min={challenge.startDate} max={localTodayDate < challenge.endDate ? localTodayDate : challenge.endDate} required /></label>
                        <label>Amount ({challenge.goalUnit})<input type="number" name="quantity" min="0.01" max="1000000" step="0.01" placeholder="e.g. 5000" required /></label>
                        <button class="primary-button" type="submit" disabled={isJoining}>{isJoining ? 'Saving…' : 'Log activity'}</button>
                      </form>
                    {:else if challenge.progress.totalQuantity < challenge.goalTarget}
                      <p class="progress-status">Activity can be logged from {challenge.startDate} through {challenge.endDate}.</p>
                    {/if}
                  {:else}
                    <p class="progress-status">Progress could not be loaded. You can still submit activity; the total will refresh when tracking is available.</p>
                    {#if localTodayDate >= challenge.startDate && localTodayDate <= challenge.endDate}
                      <form class="log-activity-form" method="POST" action="?/logActivity" use:enhance={handleEnhance} aria-busy={isJoining}>
                        <input type="hidden" name="challengeId" value={challenge.id} />
                        <input type="hidden" name="idempotencyKey" value={challenge.activityKey} />
                        <label>Activity date<input type="date" name="activityDate" value={localTodayDate} min={challenge.startDate} max={localTodayDate < challenge.endDate ? localTodayDate : challenge.endDate} required /></label>
                        <label>Amount ({challenge.goalUnit})<input type="number" name="quantity" min="0.01" max="1000000" step="0.01" placeholder="e.g. 5000" required /></label>
                        <button class="primary-button" type="submit" disabled={isJoining}>{isJoining ? 'Saving…' : 'Log activity'}</button>
                      </form>
                    {:else}
                      <p class="progress-status">Activity can be logged from {challenge.startDate} through {challenge.endDate}.</p>
                    {/if}
                  {/if}
                </section>
              {/if}
              <div class="habit-footer"><span>{challenge.responseStatus === 'Joined' ? `Joined ${challenge.joinedAtUtc ? joinedDateLabel(challenge.joinedAtUtc) : ''}` : 'You declined this challenge.'}</span>{#if challenge.teamName}<span class="category-pill">Team: {challenge.teamName}</span>{:else if challenge.responseStatus === 'Joined'}<span class="muted">Team assignment pending HR finalization.</span>{/if}</div></article>
            {/each}
          </div>
        {/if}
      </section>
    </section>
  </main>
</div>
<style>
  .challenge-page{max-width:1240px}.challenge-card h3{margin-top:17px}.challenge-card .habit-footer{gap:12px;align-items:center}.challenge-card .primary-button{min-height:37px;padding:0 13px;font-size:10px;white-space:nowrap}.challenge-card .habit-footer form{margin:0}.response-actions{display:flex;gap:8px;flex-wrap:wrap}.response-actions form{margin:0}.secondary-button{min-height:37px;padding:0 13px;border:1px solid #dce5e0;border-radius:10px;background:#fff;color:#41524b;font-size:10px;font-weight:700;cursor:pointer}.my-challenges-section{margin-top:42px}.challenge-goal{display:grid;gap:5px;margin:12px 0 16px;padding:12px;border-radius:10px;background:#f4f8f5;color:#52645a;font-size:12px;line-height:1.5}.challenge-goal strong{color:#2f4b3d}.challenge-goal span{font-weight:700;color:#2f7958}.challenge-progress{margin:12px 0 16px;padding:14px;border:1px solid #e5ece7;border-radius:12px;background:#fbfdfb}.progress-heading{display:flex;justify-content:space-between;gap:10px;font-size:12px;color:#334b3e}.progress-heading span{font-variant-numeric:tabular-nums;color:#5c6d64}.progress-track{height:9px;margin:10px 0;border-radius:999px;background:#e5ece7;overflow:hidden}.progress-fill{height:100%;border-radius:999px;background:#38936b;transition:width .2s}.progress-status{margin:6px 0 0;color:#65756c;font-size:11px}.progress-status.complete{color:#23764f;font-weight:800}.log-activity-form{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-top:14px;padding-top:12px;border-top:1px solid #e6ede8}.log-activity-form label{display:grid;gap:6px;color:#52645a;font-size:11px;font-weight:700}.log-activity-form input{width:100%;min-width:0;border:1px solid #dce5e0;border-radius:8px;padding:9px;background:white;color:#263b32;font:inherit;font-size:12px}.log-activity-form button{grid-column:1/-1}.activity-history{margin-top:10px;color:#66766d;font-size:11px}.activity-history summary{cursor:pointer;font-weight:700}.activity-history ul{padding-left:18px;line-height:1.8}@media(max-width:650px){.log-activity-form{grid-template-columns:1fr}}
  @media(max-width:650px){.challenge-card .habit-footer{align-items:flex-start;flex-direction:column}.challenge-card .habit-footer form,.challenge-card .primary-button,.response-actions{width:100%}.response-actions form{flex:1}.response-actions button{width:100%}}
</style>
