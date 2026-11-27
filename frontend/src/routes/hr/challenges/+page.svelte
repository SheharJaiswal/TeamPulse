<script lang="ts">
  import { enhance } from '$app/forms';
  import type { ActionData, PageData } from './$types';
  import { toLocalDateInputValue } from '$lib/validation.js';

  export let data: PageData;
  export let form: ActionData;
  let isSubmitting = false;
  function handleEnhance() {
    isSubmitting = true;
    return async ({ update }: { update: () => Promise<void> }) => {
      try { await update(); } finally { isSubmitting = false; }
    };
  }
  $: message = form && 'message' in form ? form.message : undefined;
</script>

<svelte:head>
  <title>Challenges · TeamPulse</title>
  <meta name="description" content="Create and manage organization challenges." />
</svelte:head>

<div class="app-shell">
  <aside class="sidebar">
    <a class="brand-lockup" href="/habits"><span class="brand-mark">✓</span><span>TeamPulse</span></a>
    <p class="nav-label">HR WORKSPACE</p>
    <a class="nav-link" href="/hr/employees/lifecycle"><span class="nav-icon">↻</span> Employee lifecycle</a>
    <a class="nav-link" href="/challenges"><span class="nav-icon">✳</span> Employee challenges</a>
    <a class="nav-link active" href="/hr/challenges" aria-current="page"><span class="nav-icon">＋</span> Manage challenges</a>
    <a class="nav-link" href="/hr/invitations"><span class="nav-icon">✉</span> Employee invitations</a>
    <a class="nav-link" href="/habits"><span class="nav-icon">◷</span> My habits</a>
    <nav class="mobile-workspace-nav" aria-label="Workspace">
      <a class="mobile-workspace-link" href="/hr/employees/lifecycle">Lifecycle</a>
      <a class="mobile-workspace-link" href="/challenges">Employee challenges</a>
      <a class="mobile-workspace-link active" href="/hr/challenges">Manage</a>
      <a class="mobile-workspace-link" href="/hr/invitations">Invitations</a>
      <a class="mobile-workspace-link" href="/habits">My habits</a>
    </nav>
    <div class="sidebar-note"><span class="note-icon">✳</span><strong>Grow together.</strong><p>Bring people together around shared wellbeing goals.</p></div>
    <div class="sidebar-profile">
      <div class="avatar">{data.employee.displayName.slice(0, 1).toUpperCase()}</div>
      <div class="profile-copy"><strong>{data.employee.displayName}</strong><span>HR administrator</span></div>
      <form method="POST" action="/logout"><button class="icon-button" aria-label="Sign out" title="Sign out">↗</button></form>
    </div>
  </aside>
  <main class="main-content">
    <header class="topbar"><span class="breadcrumb">HR workspace / <strong>Challenges</strong></span><div class="topbar-right"><span class="status-dot"></span><span>HR administration</span><div class="avatar small">{data.employee.displayName.slice(0, 1).toUpperCase()}</div></div></header>
    <section class="page-content hr-page">
      <div class="welcome-row"><div><p class="eyebrow">TEAM WELLBEING</p><h1>Challenges</h1><p class="muted">Create organization challenges and keep shared goals visible.</p></div><span class="hr-access-pill"><span></span> HR access</span></div>
      {#if message}<div class="alert" role="alert">{message}</div>{/if}
      {#if form && 'success' in form && form.success}<div class="alert success" role="status">Challenge created successfully.</div>{/if}
      <section class="create-section challenge-create">
        <div class="create-copy"><p class="eyebrow">START SOMETHING TOGETHER</p><h2>Create a challenge</h2><p class="muted">Set the action, measurable target, and challenge dates.</p><div class="create-tip"><span>✦</span> Example: walk 5,000 steps daily for 7 days (35,000 steps total).</div></div>
        <form class="create-form" method="POST" action="?/create" use:enhance={handleEnhance} aria-busy={isSubmitting}>
          <div class="form-field full"><label for="challenge-name">Challenge name</label><input id="challenge-name" name="name" maxlength="120" placeholder="e.g. 7-Day Walking Challenge" required /></div>
          <div class="form-field full"><label for="challenge-description">Description <span>Optional</span></label><textarea id="challenge-description" name="description" maxlength="1000" rows="4" placeholder="Describe the challenge goal and how to take part."></textarea></div>
          <div class="form-field full"><label for="challenge-action">Action to take</label><textarea id="challenge-action" name="goalInstructions" maxlength="500" rows="3" placeholder="e.g. Walk every day and record your steps." required></textarea><small>Tell employees exactly what they should do to make progress.</small></div>
          <div class="form-field"><label for="challenge-target">Goal target</label><input id="challenge-target" name="goalTarget" type="number" min="0.01" max="1000000" step="0.01" placeholder="e.g. 35000" required /></div>
          <div class="form-field"><label for="challenge-unit">Unit</label><input id="challenge-unit" name="goalUnit" maxlength="30" placeholder="e.g. steps" required /></div>
          <div class="form-field"><label for="challenge-start">Start date</label><input id="challenge-start" name="startDate" type="date" value={toLocalDateInputValue()} required /></div>
          <div class="form-field"><label for="challenge-end">End date</label><input id="challenge-end" name="endDate" type="date" value={toLocalDateInputValue()} required /></div>
          <div class="form-field"><label for="challenge-registration-deadline">Registration deadline</label><input id="challenge-registration-deadline" name="registrationDeadline" type="date" value={toLocalDateInputValue()} required /><small>Employees must Join or Decline by this date. HR closes registration before forming teams.</small></div>
          <div class="form-actions full"><button class="primary-button" type="submit" disabled={isSubmitting}>{isSubmitting ? 'Creating…' : 'Create challenge →'}</button></div>
        </form>
      </section>
      <div class="section-heading"><div><h2>Organization challenges</h2><p class="muted">Challenges created for your organization.</p></div><span class="count-pill">{data.challenges.length} total</span></div>
      {#if data.challenges.length === 0}
        <section class="empty-state"><div class="empty-illustration">✳</div><h3>No challenges yet</h3><p>Create your first challenge using the form above.</p></section>
      {:else}
        <div class="habit-grid">
          {#each data.challenges as challenge (challenge.id)}
            <article class="habit-card"><div class="habit-card-top"><span class="category-pill">Organization challenge</span></div><h3>{challenge.name}</h3><p class="habit-description">{challenge.description || 'No description provided.'}</p><div class="challenge-goal"><strong>Action:</strong> {challenge.goalInstructions}<span>Target: {challenge.goalTarget} {challenge.goalUnit}</span></div><div class="habit-details"><div><span class="detail-label">STARTS</span><strong>{challenge.startDate}</strong></div><div><span class="detail-label">ENDS</span><strong>{challenge.endDate}</strong></div></div><div class="habit-footer"><span>Registration: {challenge.registrationClosedAtUtc ? 'Closed' : 'Open'} · Deadline {challenge.registrationDeadline}</span><a class="primary-button" href={`/hr/challenges/${challenge.id}`}>Manage registration & teams →</a></div></article>
          {/each}
        </div>
      {/if}
    </section>
  </main>
</div>
<style>
  .hr-page{max-width:1240px}.hr-access-pill{display:inline-flex;align-items:center;gap:8px;padding:9px 13px;border:1px solid #d9eee5;border-radius:999px;color:#247653;background:#f2faf6;font-size:12px;font-weight:700;white-space:nowrap}.hr-access-pill span{width:7px;height:7px;border-radius:50%;background:#39a878;display:inline-block}.challenge-create{margin-bottom:30px}.challenge-goal{display:grid;gap:5px;margin:12px 0 16px;padding:12px;border-radius:10px;background:#f4f8f5;color:#52645a;font-size:12px;line-height:1.5}.challenge-goal strong{color:#2f4b3d}.challenge-goal span{font-weight:700;color:#2f7958}.create-form small{display:block;margin-top:7px;color:#75817d;font-size:11px;line-height:1.5}.habit-footer .primary-button{display:inline-flex;align-items:center;text-decoration:none;white-space:nowrap}
  @media(max-width:650px){.welcome-row{align-items:flex-start}}
</style>
