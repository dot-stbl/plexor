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

  // ───────────── Popover menus (⋮ / dropdowns) ─────────────
  function closeAllMenus() {
    document.querySelectorAll('.menu:not([hidden])').forEach(function (m) {
      m.hidden = true;
      var t = m.parentNode && m.parentNode.querySelector('[data-menu-trigger]');
      if (t) t.setAttribute('aria-expanded', 'false');
    });
  }
  document.querySelectorAll('[data-menu-trigger]').forEach(function (trigger) {
    var menu = trigger.parentNode.querySelector('.menu');
    if (!menu) return;
    trigger.addEventListener('click', function (e) {
      e.stopPropagation();
      var willOpen = menu.hidden;
      closeAllMenus();
      menu.hidden = !willOpen;
      trigger.setAttribute('aria-expanded', String(willOpen));
    });
    menu.addEventListener('click', function () { menu.hidden = true; trigger.setAttribute('aria-expanded', 'false'); });
  });

  // ───────────── Dialogs ─────────────
  document.querySelectorAll('[data-dialog-open]').forEach(function (b) {
    b.addEventListener('click', function () {
      var d = document.querySelector(b.getAttribute('data-dialog-open'));
      if (d) d.hidden = false;
    });
  });
  document.querySelectorAll('.dialog-scrim').forEach(function (scrim) {
    scrim.addEventListener('click', function (e) { if (e.target === scrim) scrim.hidden = true; });
    scrim.querySelectorAll('[data-dialog-close]').forEach(function (b) {
      b.addEventListener('click', function () { scrim.hidden = true; });
    });
  });

  // ───────────── Toasts ─────────────
  function showToast(msg, kind) {
    var stack = document.querySelector('.toast-stack');
    if (!stack) { stack = document.createElement('div'); stack.className = 'toast-stack'; document.body.appendChild(stack); }
    var t = document.createElement('div');
    t.className = 'toast' + (kind ? ' ' + kind : '');
    t.innerHTML = '<span class="dot"></span><span class="toast-msg"></span><button class="toast-x icon-btn sm" aria-label="Закрыть"><svg viewBox="0 0 16 16" fill="none"><path d="M4 4l8 8M12 4l-8 8" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"/></svg></button>';
    t.querySelector('.toast-msg').textContent = msg;
    function dismiss() { t.classList.add('is-out'); setTimeout(function () { if (t.parentNode) t.remove(); }, 180); }
    t.querySelector('.toast-x').addEventListener('click', dismiss);
    stack.appendChild(t);
    setTimeout(dismiss, 3600);
  }
  window.plexorToast = showToast;
  document.querySelectorAll('[data-toast]').forEach(function (b) {
    b.addEventListener('click', function () { showToast(b.getAttribute('data-toast-msg') || 'Готово', b.getAttribute('data-toast')); });
  });

  // ───────────── Copy to clipboard ─────────────
  document.querySelectorAll('[data-copy]').forEach(function (b) {
    b.addEventListener('click', function () {
      var text = b.getAttribute('data-copy');
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).then(
          function () { showToast('Скопировано: ' + text, 'ok'); },
          function () { showToast('Не удалось скопировать', 'err'); }
        );
      } else { showToast('Скопировано: ' + text, 'ok'); }
    });
  });

  // ───────────── Combobox (searchable select) ─────────────
  document.querySelectorAll('[data-combo]').forEach(function (combo) {
    var btn = combo.querySelector('.combo-btn');
    var pop = combo.querySelector('.combo-pop');
    var search = combo.querySelector('.combo-search');
    var label = combo.querySelector('.combo-label');
    var opts = combo.querySelectorAll('.combo-opt');
    if (!btn || !pop) return;
    combo.addEventListener('click', function (e) { e.stopPropagation(); });
    btn.addEventListener('click', function () {
      var open = pop.hidden;
      pop.hidden = !open;
      if (open && search) { search.value = ''; opts.forEach(function (o) { o.hidden = false; }); search.focus(); }
    });
    if (search) search.addEventListener('input', function () {
      var q = search.value.toLowerCase();
      opts.forEach(function (o) { o.hidden = o.textContent.toLowerCase().indexOf(q) === -1; });
    });
    opts.forEach(function (o) {
      o.addEventListener('click', function () {
        opts.forEach(function (x) { x.classList.remove('is-selected'); });
        o.classList.add('is-selected');
        if (label) label.textContent = o.textContent.trim();
        pop.hidden = true;
      });
    });
  });

  // ───────────── Sortable tables ─────────────
  document.querySelectorAll('table[data-sortable]').forEach(function (table) {
    table.querySelectorAll('th[data-sort]').forEach(function (th) {
      var index = Array.prototype.indexOf.call(th.parentNode.children, th);
      th.addEventListener('click', function () {
        var tbody = table.tBodies[0]; if (!tbody) return;
        var asc = th.getAttribute('aria-sort') !== 'ascending';
        table.querySelectorAll('th[data-sort]').forEach(function (o) { o.removeAttribute('aria-sort'); });
        th.setAttribute('aria-sort', asc ? 'ascending' : 'descending');
        var type = th.getAttribute('data-sort');
        var rows = Array.prototype.slice.call(tbody.rows);
        rows.sort(function (a, b) {
          var av = (a.cells[index] ? a.cells[index].textContent : '').trim();
          var bv = (b.cells[index] ? b.cells[index].textContent : '').trim();
          var r = type === 'num'
            ? (parseFloat(av.replace(/[^\d.-]/g, '')) || 0) - (parseFloat(bv.replace(/[^\d.-]/g, '')) || 0)
            : av.localeCompare(bv, 'ru');
          return asc ? r : -r;
        });
        rows.forEach(function (r) { tbody.appendChild(r); });
      });
    });
  });

  // ───────────── Table row selection + bulk bar ─────────────
  document.querySelectorAll('[data-select-table]').forEach(function (wrap) {
    var all = wrap.querySelector('[data-select-all]');
    var rowBoxes = wrap.querySelectorAll('[data-select-row]');
    var bar = wrap.querySelector('.bulk-bar');
    var countEl = bar ? bar.querySelector('.bulk-count') : null;
    function sync() {
      var n = 0;
      rowBoxes.forEach(function (cb) {
        var tr = cb.closest('tr');
        if (cb.checked) { n++; if (tr) tr.classList.add('is-selected'); }
        else if (tr) tr.classList.remove('is-selected');
      });
      if (bar) bar.hidden = n === 0;
      if (countEl) countEl.textContent = 'Выбрано: ' + n;
      if (all) { all.checked = n > 0 && n === rowBoxes.length; all.indeterminate = n > 0 && n < rowBoxes.length; }
    }
    if (all) all.addEventListener('change', function () { rowBoxes.forEach(function (cb) { cb.checked = all.checked; }); sync(); });
    rowBoxes.forEach(function (cb) { cb.addEventListener('change', sync); });
    if (bar) {
      var clear = bar.querySelector('[data-bulk-clear]');
      if (clear) clear.addEventListener('click', function () { rowBoxes.forEach(function (cb) { cb.checked = false; }); if (all) all.checked = false; sync(); });
    }
    sync();
  });

  // ───────────── Command palette (⌘K / Ctrl+K) ─────────────
  var cmdk = document.querySelector('[data-cmdk]');
  if (cmdk) {
    var cmdkInput = cmdk.querySelector('.cmdk-input');
    var cmdkItems = cmdk.querySelectorAll('.cmdk-item');
    var cmdkEmpty = cmdk.querySelector('.cmdk-empty');
    function filterCmdk() {
      var q = (cmdkInput ? cmdkInput.value : '').toLowerCase();
      var visible = 0;
      cmdkItems.forEach(function (it) {
        var match = it.textContent.toLowerCase().indexOf(q) !== -1;
        it.hidden = !match; it.classList.remove('is-active');
        if (match) visible++;
      });
      if (cmdkEmpty) cmdkEmpty.hidden = visible !== 0;
      var first = cmdk.querySelector('.cmdk-item:not([hidden])');
      if (first) first.classList.add('is-active');
    }
    function openCmdk() { cmdk.hidden = false; if (cmdkInput) { cmdkInput.value = ''; filterCmdk(); cmdkInput.focus(); } }
    function closeCmdk() { cmdk.hidden = true; }
    if (cmdkInput) cmdkInput.addEventListener('input', filterCmdk);
    cmdk.addEventListener('click', function (e) { if (e.target === cmdk) closeCmdk(); });
    cmdkItems.forEach(function (it) {
      it.addEventListener('click', function () { closeCmdk(); showToast('Команда: ' + (it.getAttribute('data-cmd') || it.textContent.trim()), 'ok'); });
    });
    document.querySelectorAll('[data-cmdk-open]').forEach(function (b) { b.addEventListener('click', openCmdk); });
    window.plexorOpenCmdk = openCmdk;
    document.addEventListener('keydown', function (e) {
      if ((e.metaKey || e.ctrlKey) && (e.key === 'k' || e.key === 'K')) { e.preventDefault(); if (cmdk.hidden) openCmdk(); else closeCmdk(); }
    });
    if (cmdkInput) cmdkInput.addEventListener('keydown', function (e) {
      var items = Array.prototype.slice.call(cmdk.querySelectorAll('.cmdk-item:not([hidden])'));
      if (!items.length) return;
      var idx = items.findIndex(function (x) { return x.classList.contains('is-active'); });
      if (idx < 0) idx = 0;
      if (e.key === 'ArrowDown') { e.preventDefault(); items[idx].classList.remove('is-active'); items[(idx + 1) % items.length].classList.add('is-active'); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); items[idx].classList.remove('is-active'); items[(idx - 1 + items.length) % items.length].classList.add('is-active'); }
      else if (e.key === 'Enter') { e.preventDefault(); items[idx].click(); }
    });
  }

  // ───────────── Global click / Esc: close overlays ─────────────
  document.addEventListener('click', function () {
    closeAllMenus();
    document.querySelectorAll('.combo-pop:not([hidden])').forEach(function (p) { p.hidden = true; });
  });
  document.addEventListener('keydown', function (e) {
    if (e.key !== 'Escape') return;
    closeAllMenus();
    document.querySelectorAll('.combo-pop:not([hidden])').forEach(function (p) { p.hidden = true; });
    document.querySelectorAll('.dialog-scrim:not([hidden])').forEach(function (d) { d.hidden = true; });
    document.querySelectorAll('.drawer.is-open').forEach(function (d) { d.classList.remove('is-open'); });
    document.querySelectorAll('.drawer-scrim.is-open').forEach(function (s) { s.classList.remove('is-open'); });
    if (cmdk && !cmdk.hidden) cmdk.hidden = true;
  });
})();
