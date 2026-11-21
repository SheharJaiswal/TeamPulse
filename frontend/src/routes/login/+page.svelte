<script lang="ts">
  import { enhance } from '$app/forms';
  import type { ActionData } from './$types';

  export let form: ActionData;
</script>

<svelte:head>
  <title>Sign in · TeamPulse</title>
  <meta name="description" content="Sign in to your TeamPulse wellness workspace." />
</svelte:head>

<main class="auth-shell">
  <section class="auth-brand">
    <a class="brand-lockup" href="/" aria-label="TeamPulse home">
      <span class="brand-mark">✓</span>
      <span>TeamPulse</span>
    </a>
    <div class="brand-message">
      <p class="eyebrow">SMALL STEPS. STRONGER TEAMS.</p>
      <h1>Wellbeing works better when it becomes a habit.</h1>
      <p>Build healthy routines, stay consistent, and make progress at your own pace.</p>
    </div>
    <p class="brand-footnote">A healthier rhythm, together.</p>
  </section>

  <section class="auth-panel">
    <div class="auth-card">
      <div class="mobile-brand brand-lockup">
        <span class="brand-mark">✓</span>
        <span>TeamPulse</span>
      </div>
      <p class="eyebrow">YOUR WORKSPACE</p>
      <h2>Welcome back</h2>
      <p class="muted">Sign in with your TeamPulse account to continue.</p>

      {#if form?.message}
        <div class="alert" role="alert">{form.message}</div>
      {/if}

      <form method="POST" use:enhance class="auth-form">
        <label for="email">Work email</label>
        <input id="email" name="email" type="email" autocomplete="username" placeholder="you@company.com" required />
        <label for="password">Password</label>
        <input id="password" name="password" type="password" autocomplete="current-password" placeholder="Enter your password" required />
        <button class="primary-button full-width" type="submit">Sign in <span aria-hidden="true">→</span></button>
      </form>
      <p class="auth-help">Access is provided by your organization's HR administrator.</p>
    </div>
    <footer class="auth-footer">© 2026 TeamPulse · Your wellbeing, your pace.</footer>
  </section>
</main>
