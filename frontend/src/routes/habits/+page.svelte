<script lang="ts">
  import { enhance } from '$app/forms';
  import type { ActionData, PageData } from './$types';
  import { toLocalDateInputValue } from '$lib/validation.js';

  export let data: PageData;
  export let form: ActionData;

  let editingHabitId: string | null = null;
  let isSubmitting = false;

  function handleEnhance() {
    isSubmitting = true;
    return async ({ update }: { update: () => Promise<void> }) => {
      try {
        await update();
      } finally {
        isSubmitting = false;
      }
    };
  }

  $: activeHabits = data.habits.filter((habit) => habit.status === 1);
  $: archivedHabits = data.habits.filter((habit) => habit.status === 2);
  $: formMessage = form && 'message' in form ? form.message : undefined;
  $: formSuccessMessage =
    form && 'success' in form && form.success
      ? form.action === 'create'
        ? 'Habit created successfully.'
        : form.action === 'update'
          ? 'Habit updated successfully.'
          : 'Habit archived successfully.'
      : undefined;

  function dateLabel(value: string): string {
    const [year, month, day] = value.split('-').map(Number);
    return new Intl.DateTimeFormat('en', { month: 'short', day: 'numeric', year: 'numeric' })
      .format(new Date(year, month - 1, day));
  }
</script>

<svelte:head>
  <title>My habits · TeamPulse</title>
  <meta name="description" content="Create and manage your personal wellbeing habits." />
</svelte:head>

<div class="app-shell">
  <aside class="sidebar">
    <a class="brand-lockup" href="/habits">
      <span class="brand-mark">✓</span>
      <span>TeamPulse</span>
    </a>

    <p class="nav-label">WORKSPACE</p>
    <a class="nav-link active" href="/habits" aria-current="page">
      <span class="nav-icon">◷</span>
      My habits
    </a>
    <a class="nav-link" href="/activities">
      <span class="nav-icon">✓</span>
      Activity history
    </a>
    <a class="nav-link" href="/challenges"><span class="nav-icon">✳</span> Challenges</a>
    {#if data.employee.role === 'HrAdmin'}
      <a class="nav-link" href="/hr/challenges"><span class="nav-icon">＋</span> Manage challenges</a>
      <a class="nav-link" href="/hr/invitations">
        <span class="nav-icon">✉</span>
        Employee invitations
      </a>
    {/if}
    <nav class="mobile-workspace-nav" aria-label="Workspace">
      <a class="mobile-workspace-link active" href="/habits" aria-current="page">My habits</a>
      <a class="mobile-workspace-link" href="/activities">Activity history</a>
      <a class="mobile-workspace-link" href="/challenges">Challenges</a>
      {#if data.employee.role === 'HrAdmin'}
        <a class="mobile-workspace-link" href="/hr/challenges">Manage</a>
        <a class="mobile-workspace-link" href="/hr/invitations">Invitations</a>
      {/if}
    </nav>
    <div class="sidebar-note">
      <span class="note-icon">✳</span>
      <strong>Progress, not perfection.</strong>
      <p>Small consistent actions add up to meaningful change.</p>
    </div>

    <div class="sidebar-profile">
      <div class="avatar">{data.employee.displayName.slice(0, 1).toUpperCase()}</div>
      <div class="profile-copy">
        <strong>{data.employee.displayName}</strong>
        <span>{data.employee.role}</span>
      </div>
      <form method="POST" action="/logout">
        <button class="icon-button" aria-label="Sign out" title="Sign out">↗</button>
      </form>
    </div>
  </aside>

  <main class="main-content">
    <header class="topbar">
      <div><span class="breadcrumb">Workspace / <strong>My habits</strong></span></div>
      <div class="topbar-right">
        <a class="text-link" href="/activities">Activity history</a>
        <span class="status-dot"></span>
        <span>Personal space</span>
        <div class="avatar small">{data.employee.displayName.slice(0, 1).toUpperCase()}</div>
      </div>
    </header>

    <section class="page-content">
      <div class="welcome-row">
        <div>
          <p class="eyebrow">YOUR WELLBEING JOURNEY</p>
          <h1>Good to see you, {data.employee.displayName.split(' ')[0]}.</h1>
          <p class="muted">Build a rhythm that works for you. One habit at a time.</p>
        </div>
        <a class="text-link" href="#create-habit">＋ Create a habit</a>
      </div>

      {#if formMessage}
        <div class="alert" role="alert">{formMessage}</div>
      {/if}
      {#if formSuccessMessage}
        <div class="alert success" role="status">{formSuccessMessage}</div>
      {/if}

      <div class="stats-grid">
        <article class="stat-card">
          <div class="stat-icon mint">◷</div>
          <span class="stat-label">Active habits</span>
          <strong class="stat-value">{activeHabits.length}</strong>
          <span class="stat-caption">Habits in your routine</span>
        </article>
        <article class="stat-card">
          <div class="stat-icon lavender">✓</div>
          <span class="stat-label">Archived habits</span>
          <strong class="stat-value">{archivedHabits.length}</strong>
          <span class="stat-caption">Past routines you've kept</span>
        </article>
        <article class="stat-card encouragement">
          <div class="stat-icon peach">✳</div>
          <span class="stat-label">A gentle reminder</span>
          <strong class="encouragement-title">Consistency beats intensity.</strong>
          <span class="stat-caption">Choose a pace you can sustain.</span>
        </article>
      </div>

      <div class="section-heading">
        <div>
          <h2>Your active habits</h2>
          <p class="muted">Your personal routines, all in one place.</p>
        </div>
        <span class="count-pill">{activeHabits.length} active</span>
      </div>

      {#if activeHabits.length === 0}
        <section class="empty-state">
          <div class="empty-illustration">✳</div>
          <h3>Start with one small thing</h3>
          <p>You don't need a perfect plan. Add a habit that makes your day a little better.</p>
          <a class="primary-button" href="#create-habit">Create your first habit <span aria-hidden="true">→</span></a>
        </section>
      {:else}
        <div class="habit-grid">
          {#each activeHabits as habit (habit.id)}
            <article class="habit-card">
              <div class="habit-card-top">
                <span class="category-pill">{habit.category || 'Personal'}</span>
                <span class="active-indicator"><i></i> Active</span>
              </div>
              {#if editingHabitId === habit.id}
                <form class="edit-form" method="POST" action="?/update" use:enhance={handleEnhance} aria-busy={isSubmitting}>
                  <input type="hidden" name="habitId" value={habit.id} />
                  <div class="form-field full">
                    <label for={`edit-name-${habit.id}`}>Habit name</label>
                    <input id={`edit-name-${habit.id}`} name="name" maxlength="100" value={habit.name} required />
                  </div>
                  <div class="form-field full">
                    <label for={`edit-description-${habit.id}`}>Description</label>
                    <textarea id={`edit-description-${habit.id}`} name="description" maxlength="500" rows="2">{habit.description || ''}</textarea>
                  </div>
                  <div class="form-field">
                    <label for={`edit-category-${habit.id}`}>Category</label>
                    <input id={`edit-category-${habit.id}`} name="category" maxlength="50" value={habit.category || ''} />
                  </div>
                  <div class="form-field">
                    <label for={`edit-frequency-${habit.id}`}>Frequency</label>
                    <select id={`edit-frequency-${habit.id}`} name="frequency">
                      <option value="1" selected={habit.frequency === 1}>Daily</option>
                      <option value="2" selected={habit.frequency === 2}>Weekly</option>
                    </select>
                  </div>
                  <div class="form-field">
                    <label for={`edit-target-${habit.id}`}>Target count</label>
                    <input id={`edit-target-${habit.id}`} name="targetCount" type="number" min="1" max="1000" value={habit.targetCount} required />
                  </div>
                  <div class="form-field">
                    <label for={`edit-start-${habit.id}`}>Start date</label>
                    <input id={`edit-start-${habit.id}`} name="startDate" type="date" value={habit.startDate} required />
                  </div>
                  <div class="form-field full">
                    <label for={`edit-end-${habit.id}`}>End date</label>
                    <input id={`edit-end-${habit.id}`} name="endDate" type="date" value={habit.endDate || ''} />
                  </div>
                  <div class="edit-actions full">
                    <button class="primary-button" type="submit" disabled={isSubmitting}>{isSubmitting ? 'Saving…' : 'Save changes'}</button>
                    <button class="subtle-button" type="button" onclick={() => editingHabitId = null}>Cancel</button>
                  </div>
                </form>
              {:else}
                <h3>{habit.name}</h3>
                <p class="habit-description">{habit.description || 'A small step towards a healthier routine.'}</p>
                <div class="habit-details">
                  <div>
                    <span class="detail-label">FREQUENCY</span>
                    <strong>{habit.frequency === 1 ? 'Daily' : 'Weekly'}</strong>
                  </div>
                  <div>
                    <span class="detail-label">TARGET</span>
                    <strong>{habit.targetCount} {habit.frequency === 1 ? 'times / day' : 'times / week'}</strong>
                  </div>
                </div>
                <div class="habit-footer">
                  <span>Started {dateLabel(habit.startDate)}</span>
                  <div class="card-actions">
                    <a class="subtle-button" href={`/activities?habitId=${habit.id}`}>Log activity</a>
                    <button class="subtle-button" type="button" onclick={() => editingHabitId = habit.id}>Edit</button>
                    <form method="POST" action="?/archive" use:enhance={handleEnhance} aria-busy={isSubmitting}>
                      <input type="hidden" name="habitId" value={habit.id} />
                      <button class="subtle-button" type="submit" disabled={isSubmitting}>{isSubmitting ? 'Working…' : 'Archive'}</button>
                    </form>
                  </div>
                </div>
              {/if}
            </article>
          {/each}
        </div>
      {/if}

      <section class="create-section" id="create-habit">
        <div class="create-copy">
          <p class="eyebrow">MAKE IT YOURS</p>
          <h2>Create a new habit</h2>
          <p class="muted">Keep it simple, make it personal, and choose a target that feels achievable.</p>
          <div class="create-tip"><span>✦</span> A good habit starts small enough to repeat.</div>
        </div>

        <form class="create-form" method="POST" action="?/create" use:enhance={handleEnhance} aria-busy={isSubmitting}>
          <div class="form-field full">
            <label for="habit-name">Habit name</label>
            <input id="habit-name" name="name" maxlength="100" placeholder="e.g. Take a mindful walk" required />
          </div>
          <div class="form-field full">
            <label for="habit-description">Description <span>Optional</span></label>
            <textarea id="habit-description" name="description" maxlength="500" rows="2" placeholder="What does this habit mean to you?"></textarea>
          </div>
          <div class="form-field">
            <label for="habit-category">Category <span>Optional</span></label>
            <input id="habit-category" name="category" maxlength="50" placeholder="e.g. Mindfulness" />
          </div>
          <div class="form-field">
            <label for="habit-frequency">Frequency</label>
            <select id="habit-frequency" name="frequency">
              <option value="1">Daily</option>
              <option value="2">Weekly</option>
            </select>
          </div>
          <div class="form-field">
            <label for="habit-target">Target count</label>
            <input id="habit-target" name="targetCount" type="number" min="1" max="1000" value="1" required />
          </div>
          <div class="form-field">
            <label for="habit-start">Start date</label>
            <input id="habit-start" name="startDate" type="date" value={toLocalDateInputValue()} required />
          </div>
          <div class="form-field full">
            <label for="habit-end">End date <span>Optional</span></label>
            <input id="habit-end" name="endDate" type="date" />
          </div>
          <div class="form-actions full">
            <button class="primary-button" type="submit" disabled={isSubmitting}>{isSubmitting ? 'Saving…' : 'Save habit →'}</button>
          </div>
        </form>
      </section>

      {#if archivedHabits.length > 0}
        <section class="archived-section">
          <div class="section-heading">
            <div>
              <h2>Past habits</h2>
              <p class="muted">Archived routines stay here for your reference.</p>
            </div>
            <span class="count-pill">{archivedHabits.length} archived</span>
          </div>
          <div class="archived-list">
            {#each archivedHabits as habit (habit.id)}
              <div class="archived-item">
                <div class="archived-check">✓</div>
                <div class="archived-copy">
                  <strong>{habit.name}</strong>
                  <span>{habit.category || 'Personal'} · {habit.frequency === 1 ? 'Daily' : 'Weekly'}</span>
                </div>
                <span class="archived-date">Archived {habit.archivedAtUtc ? new Date(habit.archivedAtUtc).toLocaleDateString() : ''}</span>
              </div>
            {/each}
          </div>
        </section>
      {/if}
    </section>
  </main>
</div>
