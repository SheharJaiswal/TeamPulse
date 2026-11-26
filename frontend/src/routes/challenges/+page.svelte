<script lang="ts">
  import { enhance } from '$app/forms';
  import type { ActionData, PageData } from './$types';

  export let data: PageData;
  export let form: ActionData;
  let isJoining = false;

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
              <p class="habit-description">{challenge.description || 'Take part in this organization challenge.'}</p>
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
              <article class="habit-card"><div class="habit-card-top"><span class="category-pill">Challenge response</span><span class="active-indicator"><i></i> {challenge.responseStatus}</span></div><h3>{challenge.name}</h3><p class="habit-description">{challenge.description || 'You are taking part in this organization challenge.'}</p><div class="habit-details"><div><span class="detail-label">STARTS</span><strong>{dateLabel(challenge.startDate)}</strong></div><div><span class="detail-label">ENDS</span><strong>{dateLabel(challenge.endDate)}</strong></div></div><div class="habit-footer"><span>{challenge.responseStatus === 'Joined' ? `Joined ${challenge.joinedAtUtc ? new Date(challenge.joinedAtUtc).toLocaleDateString() : ''}` : 'You declined this challenge.'}</span>{#if challenge.teamName}<span class="category-pill">Team: {challenge.teamName}</span>{:else if challenge.responseStatus === 'Joined'}<span class="muted">Team assignment pending HR finalization.</span>{/if}</div></article>
            {/each}
          </div>
        {/if}
      </section>
    </section>
  </main>
</div>
<style>
  .challenge-page{max-width:1240px}.challenge-card h3{margin-top:17px}.challenge-card .habit-footer{gap:12px;align-items:center}.challenge-card .primary-button{min-height:37px;padding:0 13px;font-size:10px;white-space:nowrap}.challenge-card .habit-footer form{margin:0}.response-actions{display:flex;gap:8px;flex-wrap:wrap}.response-actions form{margin:0}.secondary-button{min-height:37px;padding:0 13px;border:1px solid #dce5e0;border-radius:10px;background:#fff;color:#41524b;font-size:10px;font-weight:700;cursor:pointer}.my-challenges-section{margin-top:42px}
  @media(max-width:650px){.challenge-card .habit-footer{align-items:flex-start;flex-direction:column}.challenge-card .habit-footer form,.challenge-card .primary-button,.response-actions{width:100%}.response-actions form{flex:1}.response-actions button{width:100%}}
</style>
