/* Plexor console — interactivity
 * Tab switching, wizard steps, drawer, sidebar context
 */

(function () {
  // ───────────── Theme (light / dark, persisted) ─────────────
  // Applies the saved preference on every screen; a [data-theme-toggle]
  // control (topnav) flips and persists it. Default follows the OS.
  (function initTheme() {
    var KEY = 'plexor-theme';
    var root = document.documentElement;

    function apply(theme) {
      if (theme === 'dark') root.setAttribute('data-theme', 'dark');
      else root.removeAttribute('data-theme');
    }

    var saved = null;
    try { saved = localStorage.getItem(KEY); } catch (e) {}
    var prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    apply(saved || (prefersDark ? 'dark' : 'light'));

    document.querySelectorAll('[data-theme-toggle]').forEach(function (btn) {
      function sync() {
        var isDark = root.getAttribute('data-theme') === 'dark';
        btn.setAttribute('aria-pressed', String(isDark));
        var label = btn.querySelector('[data-theme-label]');
        if (label) label.textContent = isDark ? 'Тёмная' : 'Светлая';
      }
      sync();
      btn.addEventListener('click', function () {
        var next = root.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        apply(next);
        try { localStorage.setItem(KEY, next); } catch (e) {}
        sync();
      });
    });
  })();

  // ───────────── Tabs (page-level: switch .tab-panel.is-active) ─────────────
  document.querySelectorAll('[data-tabs]').forEach(function (root) {
    var tabBar = root.querySelector('.tabs');
    if (!tabBar) return;
    var tabs = tabBar.querySelectorAll('.tab');
    var panels = root.querySelectorAll('.tab-panel');

    tabs.forEach(function (tab, i) {
      tab.addEventListener('click', function () {
        tabs.forEach(function (t) { t.classList.remove('is-active'); t.setAttribute('aria-selected', 'false'); });
        panels.forEach(function (p) { p.classList.remove('is-active'); });
        tab.classList.add('is-active');
        tab.setAttribute('aria-selected', 'true');
        if (panels[i]) panels[i].classList.add('is-active');
      });
    });
  });

  // ───────────── Wizard steps ─────────────
  var wizard = document.querySelector('[data-wizard]');
  if (wizard) {
    var steps = wizard.querySelectorAll('.wizard-step');
    var panes = wizard.querySelectorAll('.wizard-pane');
    var prev = wizard.querySelector('[data-wizard-prev]');
    var next = wizard.querySelector('[data-wizard-next]');
    var submit = wizard.querySelector('[data-wizard-submit]');
    var current = 0;

    function update() {
      steps.forEach(function (s, i) {
        s.classList.remove('is-active', 'is-done');
        if (i < current) s.classList.add('is-done');
        if (i === current) s.classList.add('is-active');
      });
      panes.forEach(function (p, i) {
        p.classList.toggle('is-active', i === current);
      });

      if (prev) prev.disabled = current === 0;
      if (next) next.style.display = current === panes.length - 1 ? 'none' : '';
      if (submit) submit.style.display = current === panes.length - 1 ? '' : 'none';
    }

    function go(delta) {
      var target = Math.min(Math.max(current + delta, 0), panes.length - 1);
      current = target;
      update();
      wizard.dispatchEvent(new CustomEvent('wizard:step', { detail: { step: current } }));
    }

    if (prev) prev.addEventListener('click', function () { go(-1); });
    if (next) next.addEventListener('click', function () { go(+1); });
    if (submit) submit.addEventListener('click', function () {
      // Pretend to submit — in real product, would dispatch API call
      var pane = panes[current];
      pane.innerHTML =
        '<div class="empty-state" style="padding: 64px 24px;">' +
        '<div style="font-family: var(--font-mono); font-size: 14px; color: var(--fg); margin-bottom: 8px;">' +
        '<span class="pill ok" style="margin-right: 8px;"><span class="dot"></span>VM created</span> ' +
        'vm-prod-' + Math.floor(100 + Math.random() * 900) + '</div>' +
        '<div style="color: var(--muted); margin-bottom: 16px;">Provisioning in zone eu-central-1-a. This usually takes 30–60 seconds.</div>' +
        '<a href="01-vm-list.html" class="btn primary">К списку ВМ</a>' +
        '</div>';
    });

    // Step click — allow jumping to a completed step
    steps.forEach(function (s, i) {
      s.addEventListener('click', function () {
        if (i <= current || s.classList.contains('is-done')) {
          current = i;
          update();
        }
      });
    });

    // Selectable cards inside wizard (radio-style)
    wizard.querySelectorAll('[data-card-group]').forEach(function (group) {
      group.querySelectorAll('.wizard-image-card, .wizard-type-card').forEach(function (card) {
        card.addEventListener('click', function () {
          group.querySelectorAll('.wizard-image-card, .wizard-type-card').forEach(function (c) {
            c.classList.remove('is-selected');
            c.setAttribute('aria-pressed', 'false');
          });
          card.classList.add('is-selected');
          card.setAttribute('aria-pressed', 'true');
        });
      });
    });

    update();
  }

  // ───────────── Drawer ─────────────
  document.querySelectorAll('[data-drawer-open]').forEach(function (trigger) {
    trigger.addEventListener('click', function () {
      var sel = trigger.getAttribute('data-drawer-open');
      var drawer = document.querySelector(sel);
      var scrim = document.querySelector('[data-drawer-scrim]');
      if (!drawer) return;
      drawer.classList.add('is-open');
      if (scrim) scrim.classList.add('is-open');
    });
  });
  document.querySelectorAll('[data-drawer-close]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var drawer = btn.closest('.drawer') || document.querySelector('.drawer.is-open');
      var scrim = document.querySelector('.drawer-scrim.is-open');
      if (drawer) drawer.classList.remove('is-open');
      if (scrim) scrim.classList.remove('is-open');
    });
  });
  document.querySelectorAll('[data-drawer-scrim]').forEach(function (scrim) {
    scrim.addEventListener('click', function () {
      var drawer = document.querySelector('.drawer.is-open');
      if (drawer) drawer.classList.remove('is-open');
      scrim.classList.remove('is-open');
    });
  });

  // ───────────── Filter chips (single-select within group) ─────────────
  document.querySelectorAll('.toolbar-group').forEach(function (group) {
    var chips = group.querySelectorAll('.chip');
    chips.forEach(function (chip) {
      chip.addEventListener('click', function () {
        chips.forEach(function (c) {
          c.classList.remove('is-on');
          c.setAttribute('aria-pressed', 'false');
        });
        chip.classList.add('is-on');
        chip.setAttribute('aria-pressed', 'true');
      });
    });
  });

  // Cluster switcher (period selector)
  document.querySelectorAll('.cluster-switcher').forEach(function (group) {
    var pills = group.querySelectorAll('.cs-pill');
    pills.forEach(function (pill) {
      pill.addEventListener('click', function () {
        pills.forEach(function (p) { p.setAttribute('aria-pressed', 'false'); });
        pill.setAttribute('aria-pressed', 'true');
      });
    });
  });

  // Button groups (toggle aria-pressed within group)
  document.querySelectorAll('.btn-group').forEach(function (group) {
    var btns = group.querySelectorAll('.btn');
    btns.forEach(function (btn) {
      btn.addEventListener('click', function () {
        btns.forEach(function (b) {
          b.classList.remove('is-active');
          b.setAttribute('aria-pressed', 'false');
        });
        btn.classList.add('is-active');
        btn.setAttribute('aria-pressed', 'true');
      });
    });
  });

  // Pagination page buttons (visual select only)
  document.querySelectorAll('.pg-controls').forEach(function (group) {
    var pages = group.querySelectorAll('.pg-page');
    pages.forEach(function (p) {
      p.addEventListener('click', function (e) {
        e.preventDefault();
        if (p.classList.contains('pg-page')) {
          pages.forEach(function (x) { x.classList.remove('is-active'); });
          p.classList.add('is-active');
        }
      });
    });
  });

  // ───────────── Sidebar context: mark active by data-active on body ─────────────
  var page = document.body.getAttribute('data-section');
  if (page) {
    document.querySelectorAll('.sidebar-link').forEach(function (link) {
      if (link.getAttribute('data-section') === page) link.classList.add('is-active');
    });
  }

  // ───────────── Search input macro (topnav) ─────────────
  document.querySelectorAll('[data-search]').forEach(function (input) {
    input.addEventListener('keydown', function (e) {
      if (e.key === '/' && document.activeElement !== input) {
        e.preventDefault();
        input.focus();
      }
    });
  });
})();
