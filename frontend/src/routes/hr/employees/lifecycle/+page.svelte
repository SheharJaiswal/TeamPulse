<script lang="ts">
  import { enhance } from '$app/forms';
  import type { ActionData, PageData } from './$types';

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
  $: result = form && 'result' in form ? form.result : undefined;
</script>

<svelte:head>
  <title>Employee lifecycle · TeamPulse</title>
  <meta name="description" content="HR employee activation and deactivation controls." />
</svelte:head>

<div class="app-shell">
  <aside class="sidebar">
    <a class="brand-lockup" href="/habits"><span class="brand-mark">✓</span><span>TeamPulse</span></a>
    <p class="nav-label">HR WORKSPACE</p>
    <a class="nav-link active" href="/hr/employees/lifecycle" aria-current="page"><span class="nav-icon">↻</span> Employee lifecycle</a>
    <a class="nav-link" href="/hr/challenges"><span class="nav-icon">✳</span> Challenges</a>
    <a class="nav-link" href="/hr/invitations"><span class="nav-icon">✉</span> Employee invitations</a>
    <a class="nav-link" href="/habits"><span class="nav-icon">◷</span> My habits</a>
    <nav class="mobile-workspace-nav" aria-label="Workspace">
      <a class="mobile-workspace-link active" href="/hr/employees/lifecycle">Lifecycle</a>
      <a class="mobile-workspace-link" href="/hr/challenges">Challenges</a>
      <a class="mobile-workspace-link" href="/hr/invitations">Invitations</a>
    </nav>
    <div class="sidebar-note"><span class="note-icon">✳</span><strong>People first.</strong><p>Employee access changes are recorded with an audit reason.</p></div>
    <div class="sidebar-profile">
      <div class="avatar">{data.employee.displayName.slice(0, 1).toUpperCase()}</div>
      <div class="profile-copy"><strong>{data.employee.displayName}</strong><span>HR administrator</span></div>
      <form method="POST" action="/logout"><button class="icon-button" aria-label="Sign out" title="Sign out">↗</button></form>
    </div>
  </aside>
  <main class="main-content">
    <header class="topbar"><span class="breadcrumb">HR workspace / <strong>Employee lifecycle</strong></span><div class="topbar-right"><span class="status-dot"></span><span>HR administration</span><div class="avatar small">{data.employee.displayName.slice(0, 1).toUpperCase()}</div></div></header>
    <section class="page-content hr-page">
      <div class="welcome-row"><div><p class="eyebrow">EMPLOYEE ACCESS</p><h1>Employee lifecycle</h1><p class="muted">Deactivate or reactivate an employee account with an auditable reason.</p></div><span class="hr-access-pill"><span></span> HR access</span></div>
      {#if message}<div class="alert" role="alert">{message}</div>{/if}
      {#if result}
        <div class="alert success" role="status">
          Employee status: <strong>{result.isActive ? 'Active' : 'Inactive'}</strong> · {result.outcome.replaceAll('InDesiredState', ' (already in desired state)').replaceAll('transitioned', 'transitioned').replaceAll('replayed', 'replayed')}.
          Lifecycle version {result.lifecycleVersion}.
        </div>
      {/if}
      <section class="create-section challenge-create">
        <div class="create-copy"><p class="eyebrow">HR CONTROL</p><h2>Change employee status</h2><p class="muted">Use the employee ID from the employee's profile or HR record. Status changes are audited and published to downstream services.</p><div class="create-tip"><span>ⓘ</span> Deactivation blocks new login. Existing tokens may remain usable in downstream services until expiry while propagation is pending.</div></div>
        <form class="create-form" method="POST" use:enhance={handleEnhance} aria-busy={isSubmitting}>
          <div class="form-field full"><label for="lifecycle-employee-id">Employee ID</label><input id="lifecycle-employee-id" name="employeeId" value={result?.employeeId ?? ''} placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" autocomplete="off" required /></div>
          <div class="form-field full"><label for="lifecycle-reason">Reason code</label><input id="lifecycle-reason" name="reasonCode" maxlength="100" placeholder="Enter the HR-approved reason code" required /><p class="muted reason-help">The API requires a reason code. A controlled reason-code catalogue has not been defined yet.</p></div>
          <div class="form-field full"><label for="lifecycle-note">Note <span>Optional</span></label><textarea id="lifecycle-note" name="note" maxlength="1000" rows="3" placeholder="Optional context for the audit record."></textarea></div>
          <div class="form-actions full">
            <button class="primary-button" formaction="?/deactivate" type="submit" disabled={isSubmitting}>{isSubmitting ? 'Saving…' : 'Deactivate employee'}</button>
            <button class="secondary-button" formaction="?/reactivate" type="submit" disabled={isSubmitting}>{isSubmitting ? 'Saving…' : 'Reactivate employee'}</button>
          </div>
        </form>
      </section>
      <p class="form-footnote">HR is the authority for employee activation and deactivation. Reactivation does not restore previous sessions. Verify the employee ID and reason before submitting.</p>
    </section>
  </main>
</div>
<style>
  .hr-page{max-width:1240px}.hr-access-pill{display:inline-flex;align-items:center;gap:8px;padding:9px 13px;border:1px solid #d9eee5;border-radius:999px;color:#247653;background:#f2faf6;font-size:12px;font-weight:700;white-space:nowrap}.hr-access-pill span{width:7px;height:7px;border-radius:50%;background:#39a878;display:inline-block}.challenge-create{margin-bottom:20px}.reason-help{margin:7px 0 0;font-size:11px}.form-actions{display:flex;flex-wrap:wrap;gap:12px}.secondary-button{display:inline-flex;justify-content:center;align-items:center;min-height:46px;padding:0 20px;border:1px solid #dfe7e3;border-radius:10px;background:#fff;color:var(--green);font-weight:700}.secondary-button:hover{background:#f4f8f5}
  @media(max-width:650px){.welcome-row{align-items:flex-start}.form-actions button{width:100%}}
</style>
