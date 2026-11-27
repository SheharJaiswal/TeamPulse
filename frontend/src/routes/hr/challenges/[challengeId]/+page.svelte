<script lang="ts">
  import { enhance } from '$app/forms';
  import type { ActionData, PageData } from './$types';

  export let data: PageData;
  export let form: ActionData;
  let submitting = false;

  function handleEnhance() {
    submitting = true;
    return async ({ update }: { update: () => Promise<void> }) => {
      try { await update(); } finally { submitting = false; }
    };
  }

  $: registration = data.registration;
  $: unassigned = registration.participants.filter(participant => !participant.teamId);
  $: allAssigned = registration.joinedCount > 0 && unassigned.length === 0;
  $: closed = Boolean(registration.registrationClosedAtUtc);
  $: finalized = Boolean(registration.teamsFinalizedAtUtc);
  $: message = form && 'message' in form ? form.message : undefined;
</script>

<svelte:head>
  <title>Manage challenge teams · TeamPulse</title>
  <meta name="description" content="Review challenge acknowledgements and organize challenge teams." />
</svelte:head>

<div class="app-shell">
  <aside class="sidebar">
    <a class="brand-lockup" href="/habits"><span class="brand-mark">✓</span><span>TeamPulse</span></a>
    <p class="nav-label">HR WORKSPACE</p>
    <a class="nav-link active" href="/hr/challenges" aria-current="page"><span class="nav-icon">✳</span> Challenges</a>
    <a class="nav-link" href="/hr/invitations"><span class="nav-icon">✉</span> Employee invitations</a>
    <div class="sidebar-profile"><div class="avatar">H</div><div class="profile-copy"><strong>HR workspace</strong><span>Team administration</span></div></div>
  </aside>
  <main class="main-content">
    <header class="topbar"><span class="breadcrumb"><a href="/hr/challenges">HR workspace / Challenges</a> / <strong>Registration & teams</strong></span></header>
    <section class="page-content detail-page">
      <a class="back-link" href="/hr/challenges">← Back to challenges</a>
      <div class="welcome-row"><div><p class="eyebrow">CHALLENGE MANAGEMENT</p><h1>{registration.challengeName}</h1><p class="muted">{registration.description || 'Review employee responses, close registration, and form challenge-specific teams.'}</p><div class="goal-summary"><strong>Employee action:</strong> {registration.goalInstructions}<span>Individual target: {registration.goalTarget} {registration.goalUnit}</span></div><p class="date-line">{registration.startDate} – {registration.endDate} · Registration deadline {registration.registrationDeadline}</p></div><span class:closed class="state-pill">{finalized ? 'Teams finalized' : closed ? 'Registration closed' : 'Registration open'}</span></div>

      {#if message}<div class:success={form && 'success' in form && form.success} class="alert" role="status">{message}</div>{/if}

      <section class="summary-grid">
        <article><span>Invited</span><strong>{registration.invitedCount}</strong></article>
        <article><span>Joined</span><strong>{registration.joinedCount}</strong></article>
        <article><span>Declined</span><strong>{registration.declinedCount}</strong></article>
        <article><span>Pending</span><strong>{registration.pendingCount}</strong></article>
      </section>

      <section class="panel">
        <div class="panel-heading"><div><h2>Registration</h2><p>Employees must Join or Decline by {registration.registrationDeadline}.</p></div></div>
        {#if !closed}
          <p class="note">Registration is open. After you close it, pending employees will not be participants and cannot be assigned to teams.</p>
          <form method="POST" action="?/close" use:enhance={handleEnhance}><button class="primary-button" type="submit" disabled={submitting}>Close registration</button></form>
        {:else}
          <p class="note success-note">Registration closed. Only employees who joined can be assigned to teams.</p>
        {/if}
      </section>

      <section class="panel">
        <div class="panel-heading"><div><h2>Joined employees</h2><p>Assign each joined employee to one challenge team.</p></div><span class="count-pill">{registration.participants.length} participants</span></div>
        {#if registration.participants.length === 0}
          <div class="empty-state"><h3>No joined employees</h3><p>Employees who joined before registration closed will appear here.</p></div>
        {:else}
          <div class="participant-list">
            {#each registration.participants as participant (participant.employeeId)}
              <div class="participant-row"><div class="avatar small">{participant.displayName.slice(0, 1).toUpperCase()}</div><div class="participant-copy"><strong>{participant.displayName}</strong><span>{participant.email}</span></div>{#if participant.teamName}<span class="assigned-pill">{participant.teamName}</span>{:else}<span class="pending-pill">Unassigned</span>{/if}</div>
            {/each}
          </div>
        {/if}
      </section>

      {#if closed}
        <section class="panel">
          <div class="panel-heading"><div><h2>Challenge teams</h2><p>Teams are created for this challenge only; company team membership is unchanged.</p></div><span class="count-pill">{registration.teams.length} teams</span></div>
          {#if registration.teams.length}
            <div class="team-grid">
              {#each registration.teams as team (team.id)}
                <article class="team-card">
                  {#if !finalized}
                    <form method="POST" action="?/renameTeam" use:enhance={handleEnhance} class="team-edit-form">
                      <input type="hidden" name="teamId" value={team.id} />
                      <label class="sr-only" for="team-name-{team.id}">Team name</label>
                      <input id="team-name-{team.id}" name="teamName" maxlength="100" value={team.name} required />
                      <button class="secondary-button" type="submit" disabled={submitting}>Save</button>
                    </form>
                    <form method="POST" action="?/deleteTeam" use:enhance={handleEnhance} on:submit={(event) => { if (!confirm('Delete this challenge team? Its members will become unassigned and can be placed in another team.')) event.preventDefault(); }}>
                      <input type="hidden" name="teamId" value={team.id} />
                      <button class="danger-button" type="submit" disabled={submitting}>Delete team</button>
                    </form>
                  {:else}
                    <h3>{team.name}</h3>
                  {/if}
                  <span class="count-pill">{team.memberCount} members</span>
                  <ul>{#each team.members as member (member.employeeId)}<li>{member.displayName}</li>{/each}</ul>
                </article>
              {/each}
            </div>
          {/if}

          {#if !finalized}
            <div class="create-team">
              <h3>Create a team</h3>
              {#if unassigned.length === 0}
                <p class="note">All joined employees are assigned. You can finalize teams.</p>
              {:else}
                <form method="POST" action="?/createTeam" use:enhance={handleEnhance}>
                  <div class="form-field"><label for="team-name">Team name</label><input id="team-name" name="teamName" maxlength="100" placeholder="e.g. Fit Warriors" required /></div>
                  <div class="form-field"><label for="team-members">Select unassigned joined employees</label><select id="team-members" name="employeeIds" multiple size={Math.min(8, unassigned.length)} required>{#each unassigned as participant (participant.employeeId)}<option value={participant.employeeId}>{participant.displayName} · {participant.email}</option>{/each}</select><small>Use Ctrl (Windows) or Cmd (Mac) to select multiple employees.</small></div>
                  <button class="primary-button" type="submit" disabled={submitting}>Create team and assign selected →</button>
                </form>
              {/if}
            </div>
            <div class="finalize-row"><span>{registration.joinedCount - unassigned.length} of {registration.joinedCount} joined employees assigned</span><form method="POST" action="?/finalize" use:enhance={handleEnhance}><button class="primary-button" type="submit" disabled={submitting || !allAssigned || registration.teams.length === 0}>Finalize teams</button></form></div>
          {:else}
            <p class="note success-note">Teams finalized on {new Date(registration.teamsFinalizedAtUtc!).toLocaleString()}. Team assignments are locked.</p>
          {/if}
        </section>
      {/if}
    </section>
  </main>
</div>

<style>
  .detail-page{max-width:1180px}.goal-summary{display:grid;gap:5px;margin:12px 0;padding:12px;border-radius:10px;background:#f4f8f5;color:#52645a;font-size:12px;line-height:1.5}.goal-summary span{font-weight:700;color:#2f7958}.date-line{margin:8px 0 0;color:#75817d;font-size:12px}.back-link{display:inline-block;margin-bottom:20px;color:#537267;text-decoration:none;font-size:13px;font-weight:700}.state-pill,.assigned-pill,.pending-pill{display:inline-flex;align-items:center;border-radius:999px;padding:8px 12px;background:#edf7f1;color:#2e7958;font-size:11px;font-weight:700;white-space:nowrap}.state-pill.closed,.pending-pill{background:#fff5e8;color:#98641f}.summary-grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:14px;margin:24px 0}.summary-grid article,.panel{border:1px solid #e4ebe7;border-radius:18px;background:#fff;padding:22px}.summary-grid article span{display:block;color:#75817d;font-size:12px}.summary-grid article strong{display:block;margin-top:8px;font-size:30px;color:#253a31}.panel{margin-bottom:20px}.panel-heading{display:flex;justify-content:space-between;align-items:center;gap:16px;margin-bottom:18px}.panel-heading h2{margin:0;font-size:21px}.panel-heading p,.note{margin:7px 0 0;color:#75817d;font-size:13px;line-height:1.6}.note{padding:12px 14px;border-radius:12px;background:#f5f8f6;margin:0 0 16px}.success-note{background:#eef8f2;color:#347856}.participant-list{display:grid;gap:10px}.participant-row{display:flex;align-items:center;gap:12px;padding:12px;border:1px solid #edf1ee;border-radius:12px}.participant-copy{display:grid;gap:3px;flex:1;min-width:0}.participant-copy strong{font-size:13px}.participant-copy span{font-size:11px;color:#7b8782;overflow-wrap:anywhere}.team-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px}.team-card{border:1px solid #e5ece7;border-radius:14px;padding:16px}.team-card h3{display:inline-block;margin:0 8px 12px 0;font-size:16px}.team-edit-form{display:flex;gap:8px;margin-bottom:8px}.team-edit-form input{min-width:0;flex:1;border:1px solid #dce5e0;border-radius:8px;padding:9px;font:inherit;font-size:13px}.secondary-button,.danger-button{border:1px solid #dce5e0;border-radius:8px;background:#fff;color:#345044;padding:8px 10px;font-size:11px;font-weight:700;cursor:pointer}.danger-button{border-color:#f0d4d1;color:#a33b32}.sr-only{position:absolute;width:1px;height:1px;padding:0;margin:-1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;border:0}.team-card ul{padding-left:18px;margin:12px 0 0;color:#64736c;font-size:12px;line-height:1.9}.create-team{margin-top:28px;padding-top:22px;border-top:1px solid #e9efeb}.create-team h3{margin:0 0 16px}.create-team form{display:grid;gap:16px;max-width:680px}.form-field{display:grid;gap:8px}.form-field label{font-size:12px;font-weight:700;color:#41534b}.form-field input,.form-field select{width:100%;border:1px solid #dce5e0;border-radius:10px;padding:12px;background:#fff;color:#263b32;font:inherit}.form-field select{min-height:140px}.form-field small{font-size:11px;color:#7b8782}.finalize-row{display:flex;align-items:center;justify-content:space-between;gap:16px;margin-top:24px;padding-top:18px;border-top:1px solid #e9efeb;color:#718079;font-size:12px}.primary-button{border:0;border-radius:10px;background:#2f7958;color:#fff;padding:12px 16px;font-size:12px;font-weight:700;cursor:pointer}.primary-button:disabled{opacity:.5;cursor:not-allowed}.alert{padding:13px 16px;margin:16px 0;border-radius:12px;background:#fff5e8;color:#98641f;font-size:13px}.alert.success{background:#eef8f2;color:#347856}.empty-state{padding:26px;text-align:center;color:#7a8781}.empty-state h3{margin:0 0 8px;color:#354b40}.empty-state p{margin:0;font-size:13px}@media(max-width:850px){.team-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:650px){.summary-grid{grid-template-columns:repeat(2,minmax(0,1fr))}.team-grid{grid-template-columns:1fr}.panel,.summary-grid article{padding:16px}.finalize-row{align-items:flex-start;flex-direction:column}.panel-heading{align-items:flex-start;flex-direction:column}}
</style>
