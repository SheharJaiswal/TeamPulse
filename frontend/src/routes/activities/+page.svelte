<script lang="ts">
  import { enhance } from '$app/forms';
  import type { ActionData, PageData } from './$types';

  export let data: PageData;
  export let form: ActionData;

  $: activeHabits = data.habits.filter((habit) => habit.status === 1);
  $: habitNames = new Map(data.habits.map((habit) => [habit.id, habit.name]));
  $: formMessage = form && 'message' in form ? form.message : undefined;

  function dateLabel(value: string): string {
    const [year, month, day] = value.split('-').map(Number);
    return new Intl.DateTimeFormat('en', { month: 'short', day: 'numeric', year: 'numeric' })
      .format(new Date(year, month - 1, day));
  }
</script>

<svelte:head>
  <title>Activity history · TeamPulse</title>
  <meta name="description" content="Record your habit activity and review your personal history." />
</svelte:head>

<div class="app-shell">
  <aside class="sidebar">
    <a class="brand-lockup" href="/habits"><span class="brand-mark">✓</span><span>TeamPulse</span></a>
    <p class="nav-label">WORKSPACE</p>
    <a class="nav-link" href="/habits"><span class="nav-icon">◷</span> My habits</a>
    <a class="nav-link active" href="/activities"><span class="nav-icon">✓</span> Activity history</a>
    <div class="sidebar-note">
      <span class="note-icon">✳</span>
      <strong>Progress, not perfection.</strong>
      <p>Every small action is worth recording.</p>
    </div>
    <div class="sidebar-profile">
      <div class="profile-copy"><strong>Personal workspace</strong><span>Private activity history</span></div>
      <form method="POST" action="/logout"><button class="icon-button" aria-label="Sign out" title="Sign out">↗</button></form>
    </div>
  </aside>

  <main class="main-content">
    <header class="topbar"><span class="breadcrumb">Workspace / <strong>Activity history</strong></span><a class="text-link" href="/habits">Manage habits</a></header>
    <section class="page-content">
      <div class="welcome-row">
        <div><p class="eyebrow">YOUR DAILY MOMENTUM</p><h1>Activity history</h1><p class="muted">Record what you did and keep a personal timeline of your effort.</p></div>
      </div>

      {#if formMessage}<div class="alert" role="alert">{formMessage}</div>{/if}
      {#if form && 'success' in form && form.success}<div class="alert" role="status">Activity recorded successfully.</div>{/if}

      <section class="create-section">
        <div class="create-copy">
          <p class="eyebrow">LOG A MOMENT</p>
          <h2>Record activity</h2>
          <p class="muted">Add a self-reported count for one of your personal habits.</p>
          <div class="create-tip"><span>✦</span> Multiple entries on the same day are supported.</div>
        </div>
        {#if activeHabits.length === 0}
          <div class="empty-state"><h3>No active habits yet</h3><p>Create a habit before recording activity.</p><a class="primary-button" href="/habits#create-habit">Create a habit →</a></div>
        {:else}
          <form class="create-form" method="POST" action="?/submit" use:enhance>
            <div class="form-field full">
              <label for="activity-habit">Habit</label>
              <select id="activity-habit" name="habitId" required>
                {#each activeHabits as habit (habit.id)}<option value={habit.id}>{habit.name}</option>{/each}
              </select>
            </div>
            <div class="form-field">
              <label for="activity-date">Activity date</label>
              <input id="activity-date" name="activityDate" type="date" value={new Date().toISOString().slice(0, 10)} required />
            </div>
            <div class="form-field">
              <label for="activity-quantity">Quantity</label>
              <input id="activity-quantity" name="quantity" type="number" min="1" max="1000" value="1" required />
            </div>
            <div class="form-actions full"><button class="primary-button" type="submit">Record activity →</button></div>
          </form>
        {/if}
      </section>

      <div class="section-heading">
        <div><h2>Your activity timeline</h2><p class="muted">Only your own submissions are shown here.</p></div>
        <span class="count-pill">{data.activities.length} entries</span>
      </div>
      {#if data.activities.length === 0}
        <section class="empty-state"><div class="empty-illustration">✓</div><h3>Your story starts here</h3><p>Once you record an activity, it will appear in this timeline.</p></section>
      {:else}
        <div class="archived-list">
          {#each data.activities as activity (activity.id)}
            <article class="archived-item">
              <div class="archived-check">✓</div>
              <div class="archived-copy"><strong>{habitNames.get(activity.habitId) || 'Habit activity'}</strong><span>{dateLabel(activity.activityDate)} · Quantity {activity.quantity}</span></div>
              <span class="archived-date">Recorded {new Date(activity.submittedAtUtc).toLocaleString()}</span>
            </article>
          {/each}
        </div>
      {/if}
    </section>
  </main>
</div>
