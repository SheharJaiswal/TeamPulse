<script lang="ts">
  import { enhance } from '$app/forms';
  import type { ActionData, PageData } from './$types';

  export let data: PageData;
  export let form: ActionData;

  $: invitation = form && 'invitation' in form ? form.invitation : undefined;
  $: message = form && 'message' in form ? form.message : undefined;
  $: actionLabel = form && 'action' in form
    ? form.action === 'create' ? 'Invitation created.' :
      form.action === 'resend' ? 'A new invitation email has been queued.' :
      form.action === 'cancel' ? 'Invitation cancelled.' :
      'Invitation details loaded.'
    : '';

  function dateTime(value: string): string {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short'
    }).format(new Date(value));
  }
</script>

<svelte:head>
  <title>Employee invitations · TeamPulse</title>
  <meta name="description" content="Invite employees and manage their account invitations." />
</svelte:head>

<div class="app-shell">
  <aside class="sidebar">
    <a class="brand-lockup" href="/habits">
      <span class="brand-mark">✓</span><span>TeamPulse</span>
    </a>
    <p class="nav-label">HR WORKSPACE</p>
    <a class="nav-link active" href="/hr/invitations" aria-current="page">
      <span class="nav-icon">✉</span> Employee invitations
    </a>
    <a class="nav-link" href="/habits"><span class="nav-icon">◷</span> My habits</a>
    <a class="nav-link" href="/activities"><span class="nav-icon">✓</span> Activity history</a>
    <nav class="mobile-workspace-nav" aria-label="Workspace">
      <a class="mobile-workspace-link" href="/habits">My habits</a>
      <a class="mobile-workspace-link" href="/activities">Activity history</a>
      <a class="mobile-workspace-link active" href="/hr/invitations" aria-current="page">Invitations</a>
    </nav>
    <div class="sidebar-note">
      <span class="note-icon">✳</span>
      <strong>Make room to grow.</strong>
      <p>Bring your people into a healthier, more connected workplace.</p>
    </div>
    <div class="sidebar-profile">
      <div class="avatar">{data.employee.displayName.slice(0, 1).toUpperCase()}</div>
      <div class="profile-copy">
        <strong>{data.employee.displayName}</strong>
        <span>HR administrator</span>
      </div>
      <form method="POST" action="/logout">
        <button class="icon-button" aria-label="Sign out" title="Sign out">↗</button>
      </form>
    </div>
  </aside>

  <main class="main-content">
    <header class="topbar">
      <span class="breadcrumb">HR workspace / <strong>Employee invitations</strong></span>
      <div class="topbar-right">
        <span class="status-dot"></span><span>HR administration</span>
        <div class="avatar small">{data.employee.displayName.slice(0, 1).toUpperCase()}</div>
      </div>
    </header>

    <section class="page-content hr-page">
      <div class="welcome-row">
        <div>
          <p class="eyebrow">PEOPLE & ACCESS</p>
          <h1>Employee invitations</h1>
          <p class="muted">Invite a teammate to TeamPulse and help them get started.</p>
        </div>
        <span class="hr-access-pill"><span></span> HR access</span>
      </div>

      {#if message}
        <div class="alert" role="alert">{message}</div>
      {/if}
      {#if invitation}
        <div class="alert success" role="status">{actionLabel}</div>
        <section class="invitation-result">
          <div class="result-heading">
            <div>
              <p class="eyebrow">INVITATION DETAILS</p>
              <h2>{invitation.displayName}</h2>
              <p class="muted">{invitation.email}</p>
            </div>
            <span class:status-pending={invitation.status.toLowerCase() === 'pending'} class="invitation-status">
              {invitation.status}
            </span>
          </div>
          <div class="result-grid">
            <div><span>INVITATION ID</span><strong class="id-value">{invitation.invitationId}</strong></div>
            <div><span>ORGANIZATION</span><strong>{invitation.organizationId}</strong></div>
            <div><span>TEAM</span><strong>{invitation.teamId}</strong></div>
            <div><span>EXPIRES</span><strong>{dateTime(invitation.expiresAtUtc)}</strong></div>
            <div><span>DELIVERY GENERATION</span><strong>{invitation.deliveryGeneration}</strong></div>
          </div>
          {#if invitation.status.toLowerCase() === 'pending'}
            <div class="invitation-actions">
              <form method="POST" action="?/resend" use:enhance>
                <input type="hidden" name="invitationId" value={invitation.invitationId} />
                <button class="primary-button" type="submit">Resend invitation</button>
              </form>
              <form method="POST" action="?/cancel" use:enhance>
                <input type="hidden" name="invitationId" value={invitation.invitationId} />
                <button class="danger-button" type="submit">Cancel invitation</button>
              </form>
            </div>
          {/if}
        </section>
      {/if}

      <div class="hr-content-grid">
        <section class="hr-panel">
          <div class="panel-heading">
            <div class="panel-icon">＋</div>
            <div><h2>Invite an employee</h2><p class="muted">An activation link will be sent to their work email.</p></div>
          </div>
          <form method="POST" action="?/create" use:enhance class="hr-form">
            <div class="form-field full">
              <label for="invite-name">Employee name</label>
              <input id="invite-name" name="displayName" maxlength="150" placeholder="e.g. Aditi Sharma" required />
            </div>
            <div class="form-field full">
              <label for="invite-email">Work email</label>
              <input id="invite-email" name="email" type="email" maxlength="320" placeholder="aditi@company.com" required />
            </div>
            <div class="form-field full">
              <label for="invite-organization">Organization ID</label>
              <input id="invite-organization" name="organizationId" maxlength="100" placeholder="Organization identifier" required />
            </div>
            <div class="form-field full">
              <label for="invite-team">Team ID</label>
              <input id="invite-team" name="teamId" maxlength="100" placeholder="Team identifier" required />
            </div>
            <button class="primary-button full-width" type="submit">Create invitation <span aria-hidden="true">→</span></button>
          </form>
          <p class="form-footnote">The employee will create their password using the secure, time-limited activation link. New accounts receive the Employee role.</p>
        </section>

        <section class="hr-panel lookup-panel">
          <div class="panel-heading">
            <div class="panel-icon violet">⌕</div>
            <div><h2>Find an invitation</h2><p class="muted">Look up a specific invitation by its ID.</p></div>
          </div>
          <form method="POST" action="?/inspect" use:enhance class="hr-form">
            <div class="form-field full">
              <label for="lookup-id">Invitation ID</label>
              <input id="lookup-id" name="invitationId" placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" required />
            </div>
            <button class="secondary-button full-width" type="submit">Load invitation</button>
          </form>
          <div class="lookup-note">
            <span>ⓘ</span>
            <p>Use the invitation ID returned when an invitation is created. The current API supports direct lookup; a searchable invitation directory will require a dedicated list endpoint.</p>
          </div>
        </section>
      </div>
    </section>
  </main>
</div>

<style>
  .hr-page { max-width: 1240px; }
  .hr-access-pill { display: inline-flex; align-items: center; gap: 8px; padding: 9px 13px; border: 1px solid #d9eee5; border-radius: 999px; color: #247653; background: #f2faf6; font-size: 12px; font-weight: 700; white-space: nowrap; }
  .hr-access-pill span, .invitation-status::before { width: 7px; height: 7px; border-radius: 50%; background: #39a878; display: inline-block; }
  .hr-content-grid { display: grid; grid-template-columns: minmax(0, 1.15fr) minmax(280px, .85fr); gap: 22px; align-items: start; margin-top: 28px; }
  .hr-panel, .invitation-result { background: white; border: 1px solid var(--border, #e9ecef); border-radius: 18px; padding: 26px; box-shadow: 0 8px 28px rgba(28, 45, 39, .035); }
  .panel-heading { display: flex; align-items: center; gap: 14px; margin-bottom: 25px; }
  .panel-heading h2, .result-heading h2 { margin: 0 0 5px; font-size: 19px; }
  .panel-heading p, .result-heading p { margin: 0; font-size: 13px; }
  .panel-icon { width: 42px; height: 42px; display: grid; place-items: center; border-radius: 13px; background: #eaf7f1; color: #26825b; font-size: 24px; }
  .panel-icon.violet { background: #f1efff; color: #7165bb; }
  .hr-form { display: grid; grid-template-columns: 1fr; gap: 17px; }
  .hr-form .form-field { display: grid; gap: 7px; }
  .hr-form label { font-size: 12px; font-weight: 700; color: #46534d; }
  .hr-form input { width: 100%; min-height: 45px; border: 1px solid #dfe6e2; border-radius: 9px; padding: 10px 12px; background: #fff; color: #26332d; font: inherit; font-size: 13px; box-sizing: border-box; }
  .hr-form input:focus { outline: 3px solid rgba(57, 168, 120, .14); border-color: #55a982; }
  .full-width { width: 100%; justify-content: center; }
  .secondary-button, .danger-button { display: inline-flex; align-items: center; justify-content: center; min-height: 43px; border-radius: 9px; padding: 0 16px; border: 1px solid #dce5e0; background: white; color: #34483d; font: inherit; font-size: 13px; font-weight: 700; cursor: pointer; }
  .danger-button { border-color: #f0d7d5; color: #a43d38; background: #fffafa; }
  .form-footnote { margin: 17px 0 0; color: #7b8781; font-size: 11px; line-height: 1.6; }
  .lookup-note { display: flex; gap: 10px; margin-top: 26px; padding: 14px; border-radius: 11px; background: #f7f8fb; color: #65716b; font-size: 11px; line-height: 1.6; }
  .lookup-note span { color: #8175c3; font-size: 15px; }
  .lookup-note p { margin: 0; }
  .invitation-result { margin-top: 22px; }
  .result-heading { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; }
  .invitation-status { display: inline-flex; align-items: center; gap: 7px; text-transform: capitalize; padding: 7px 10px; border-radius: 999px; background: #f2f4f3; color: #64716a; font-size: 11px; font-weight: 700; }
  .invitation-status.status-pending { background: #fff7e9; color: #9a6b1d; }
  .invitation-status.status-pending::before { background: #d9a33f; }
  .result-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 19px; margin-top: 24px; }
  .result-grid div { min-width: 0; display: grid; gap: 6px; }
  .result-grid span { color: #89948e; font-size: 9px; letter-spacing: .08em; font-weight: 800; }
  .result-grid strong { color: #34443b; font-size: 12px; overflow-wrap: anywhere; }
  .result-grid .id-value { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 10px; }
  .invitation-actions { display: flex; gap: 10px; margin-top: 24px; padding-top: 19px; border-top: 1px solid #edf0ee; }
  @media (max-width: 850px) { .hr-content-grid { grid-template-columns: 1fr; } }
  @media (max-width: 600px) { .hr-panel, .invitation-result { padding: 19px; } .result-grid { grid-template-columns: 1fr; } .invitation-actions { flex-direction: column; } .invitation-actions form, .invitation-actions button { width: 100%; } .welcome-row { align-items: flex-start; } }
</style>
