<script lang="ts">
  import { enhance } from '$app/forms';
  import type { ActionData, PageData } from './$types';

  export let data: PageData;
  export let form: ActionData;
</script>

<svelte:head>
  <title>Activate account · TeamPulse</title>
  <meta name="description" content="Set your password and activate your TeamPulse account." />
</svelte:head>

<main class="auth-shell">
  <section class="auth-brand">
    <a class="brand-lockup" href="/" aria-label="TeamPulse home">
      <span class="brand-mark">✓</span>
      <span>TeamPulse</span>
    </a>
    <div class="brand-message">
      <p class="eyebrow">WELCOME TO TEAMPULSE</p>
      <h1>A healthier rhythm starts with one small step.</h1>
      <p>Your organization has invited you to build healthy routines and make progress together.</p>
    </div>
    <p class="brand-footnote">A healthier rhythm, together.</p>
  </section>

  <section class="auth-panel">
    <div class="auth-card">
      <div class="mobile-brand brand-lockup">
        <span class="brand-mark">✓</span>
        <span>TeamPulse</span>
      </div>
      <p class="eyebrow">ACCOUNT SETUP</p>
      <h2>Activate your account</h2>
      <p class="muted">Create a password to finish setting up your TeamPulse account.</p>

      {#if form?.success}
        <div class="success-alert" role="status">
          <strong>You're all set.</strong>
          <p>{form.message}</p>
          <a class="primary-button full-width button-link" href="/login">Continue to sign in <span aria-hidden="true">→</span></a>
        </div>
      {:else}
        {#if form?.message}
          <div class="alert" role="alert">{form.message}</div>
        {/if}
        {#if !data.token}
          <div class="alert" role="alert">This activation link is incomplete. Ask your HR administrator to resend the invitation.</div>
        {/if}

        <form method="POST" use:enhance class="auth-form">
          <input type="hidden" name="token" value={data.token} />
          <label for="password">Create password</label>
          <input
            id="password"
            name="password"
            type="password"
            autocomplete="new-password"
            minlength="12"
            maxlength="256"
            placeholder="At least 12 characters"
            required
            disabled={!data.token}
          />
          <label for="confirmPassword">Confirm password</label>
          <input
            id="confirmPassword"
            name="confirmPassword"
            type="password"
            autocomplete="new-password"
            minlength="12"
            maxlength="256"
            placeholder="Enter your password again"
            required
            disabled={!data.token}
          />
          <p class="password-hint">Use 12–256 characters. Your password is securely hashed before it is stored.</p>
          <button class="primary-button full-width" type="submit" disabled={!data.token}>
            Activate account <span aria-hidden="true">→</span>
          </button>
        </form>
      {/if}
    </div>
    <footer class="auth-footer">© 2026 TeamPulse · Your wellbeing, your pace.</footer>
  </section>
</main>

<style>
  .success-alert {
    margin-top: 1.5rem;
    padding: 1.25rem;
    border: 1px solid #b7e4ca;
    border-radius: 14px;
    background: #f0fbf4;
    color: #155b35;
  }
  .success-alert p { margin: 0.5rem 0 1.25rem; }
  .button-link {
    display: flex;
    justify-content: center;
    align-items: center;
    gap: 0.5rem;
    text-decoration: none;
  }
  .password-hint {
    margin: -0.25rem 0 0;
    color: #697586;
    font-size: 0.82rem;
    line-height: 1.5;
  }
</style>
