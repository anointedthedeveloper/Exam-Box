// Render UTC timestamps in the viewer's local time.
document.querySelectorAll('time[datetime]').forEach(function (t) {
  var d = new Date(t.getAttribute('datetime'));
  if (isNaN(d)) return;
  t.textContent = t.dataset.fmt === 'date' ? d.toLocaleDateString() : d.toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
});
// Friendly in-page confirm dialog (returns a Promise<boolean>).
window.exConfirm = function (opts) {
  var m = document.getElementById('confirmModal');
  if (!m) return Promise.resolve(window.confirm(opts.text || 'Are you sure?'));
  document.getElementById('modalTitle').textContent = opts.title || 'Are you sure?';
  document.getElementById('modalText').textContent = opts.text || '';
  var yes = document.getElementById('modalYes'), no = document.getElementById('modalNo');
  yes.textContent = opts.yes || 'Yes, continue'; no.textContent = opts.no || 'Go back';
  m.classList.add('show'); m.setAttribute('aria-hidden', 'false'); yes.focus();
  return new Promise(function (res) {
    function done(v) {
      m.classList.remove('show'); m.setAttribute('aria-hidden', 'true');
      yes.onclick = no.onclick = null; document.removeEventListener('keydown', key); res(v);
    }
    function key(e) { if (e.key === 'Escape') done(false); }
    yes.onclick = function () { done(true); }; no.onclick = function () { done(false); };
    m.onclick = function (e) { if (e.target === m) done(false); };
    document.addEventListener('keydown', key);
  });
};
document.querySelectorAll('form[data-confirm]').forEach(function (f) {
  f.addEventListener('submit', function (e) {
    if (f.dataset.ok) return;
    e.preventDefault();
    window.exConfirm({ title: f.dataset.confirmTitle || 'Start this exam?', text: f.dataset.confirm, yes: f.dataset.confirmYes || 'Yes, start' }).then(function (ok) {
      if (ok) { f.dataset.ok = '1'; if (f.requestSubmit) f.requestSubmit(); else f.submit(); }
    });
  });
});
document.querySelectorAll('[data-copy]').forEach(function (b) {
  b.addEventListener('click', function () {
    navigator.clipboard.writeText(b.dataset.copy).then(function () { b.textContent = 'Copied'; });
  });
});

// Eye button inside password fields: <button class="eye" data-show-pw="InputId">
document.querySelectorAll('[data-show-pw]').forEach(function (b) {
  b.addEventListener('click', function () {
    var i = document.getElementById(b.dataset.showPw);
    if (!i) return;
    var show = i.type === 'password';
    i.type = show ? 'text' : 'password';
    b.classList.toggle('on', show);
    i.focus();
  });
});

// Loaders: button spinner (+ optional full-screen overlay) while a form submits, top progress bar on navigation.
function busy(on) { document.body.classList.toggle('navigating', on); }
document.querySelectorAll('form[data-loading]').forEach(function (f) {
  f.addEventListener('submit', function (e) {
    setTimeout(function () {            // after every other handler (e.g. confirm dialogs) had its say
      if (e.defaultPrevented) return;
      var btn = f.querySelector('button:not([type=button])');
      if (btn) btn.classList.add('is-loading');
      if (f.dataset.overlay !== undefined) {
        document.getElementById('overlayMsg').textContent = f.dataset.loading;
        document.getElementById('overlay').classList.add('show');
      }
      busy(true);
    }, 0);
  });
});
document.addEventListener('click', function (e) {
  var a = e.target.closest && e.target.closest('a[href]');
  if (a && a.origin === location.origin && !a.target && !e.ctrlKey && !e.metaKey && a.getAttribute('href')[0] !== '#') busy(true);
});
window.addEventListener('pageshow', function () {   // coming back via the Back button
  busy(false);
  document.getElementById('overlay') && document.getElementById('overlay').classList.remove('show');
  document.querySelectorAll('.is-loading').forEach(function (b) { b.classList.remove('is-loading'); });
});

// Staggered entrance for card grids.
document.querySelectorAll('.stats>*,.exam-grid>*').forEach(function (el, i) { el.style.animationDelay = (i % 9) * 70 + 'ms'; });

// Score ring on the result page: fill and count up.
if (!window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
  document.querySelectorAll('.ring').forEach(function (r) {
    var target = parseFloat(getComputedStyle(r).getPropertyValue('--p')) || 0, inner = r.firstElementChild, t0 = null;
    function step(ts) {
      t0 = t0 || ts;
      var k = Math.min(1, (ts - t0) / 1300), e = 1 - Math.pow(1 - k, 3);
      r.style.setProperty('--p', (target * e).toFixed(2));
      if (inner) inner.textContent = (Math.round(target * e * 10) / 10) + '%';
      if (k < 1) requestAnimationFrame(step);
    }
    r.style.setProperty('--p', '0');
    requestAnimationFrame(step);
  });
}

// Caps Lock hint under password fields that have a #caps element.
(function () {
  var caps = document.getElementById('caps'), pw = document.getElementById('Password');
  if (!caps || !pw) return;
  function check(e) { caps.classList.toggle('show', !!(e.getModifierState && e.getModifierState('CapsLock'))); }
  pw.addEventListener('keydown', check); pw.addEventListener('keyup', check);
  pw.addEventListener('blur', function () { caps.classList.remove('show'); });
})();


// Live countdowns ("Opens in 1d 2h 5m"); reloads the page the moment an exam opens.
(function () {
  var els = document.querySelectorAll('[data-countdown]');
  if (!els.length) return;
  function tick() {
    var reload = false;
    els.forEach(function (el) {
      var s = Math.round((new Date(el.dataset.countdown) - Date.now()) / 1000);
      if (s <= 0) { el.textContent = 'Opening now…'; reload = true; return; }
      var d = Math.floor(s / 86400), h = Math.floor(s % 86400 / 3600), m = Math.floor(s % 3600 / 60), sec = s % 60;
      el.textContent = (d ? d + 'd ' : '') + (d || h ? h + 'h ' : '') + m + 'm' + (d ? '' : ' ' + sec + 's');
    });
    if (reload) setTimeout(function () { location.reload(); }, 1200);
  }
  tick(); setInterval(tick, 1000);
})();

// Cards glide in as they scroll into view.
(function () {
  if (!('IntersectionObserver' in window) || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
  var io = new IntersectionObserver(function (entries) {
    entries.forEach(function (e) { if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); } });
  }, { rootMargin: '0px 0px -8% 0px' });
  document.querySelectorAll('.q,.feedback').forEach(function (el) { el.classList.add('reveal'); io.observe(el); });
})();

// Confetti once when a result shows a pass.
(function () {
  var hero = document.querySelector('.res-hero.pass');
  if (!hero || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
  var key = 'confetti:' + location.pathname;
  try { if (sessionStorage.getItem(key)) return; sessionStorage.setItem(key, '1'); } catch (e) { }
  var box = document.createElement('div'); box.className = 'confetti'; document.body.appendChild(box);
  var colors = ['#0b5ff0', '#3d8bff', '#12a06f', '#ffd166', '#ff7a90', '#b794ff'];
  for (var i = 0; i < 90; i++) {
    var p = document.createElement('i');
    p.style.left = Math.random() * 100 + '%';
    p.style.background = colors[i % colors.length];
    p.style.animationDelay = Math.random() * 0.8 + 's';
    p.style.animationDuration = 2.2 + Math.random() * 1.8 + 's';
    p.style.setProperty('--dx', (Math.random() * 160 - 80) + 'px');
    p.style.transform = 'rotate(' + Math.random() * 360 + 'deg)';
    box.appendChild(p);
  }
  setTimeout(function () { box.remove(); }, 5200);
})();

// Greeting by the student's own clock.
(function () {
  var g = document.getElementById('greetWord'), d = document.getElementById('today');
  if (g) { var h = new Date().getHours(); g.textContent = h < 12 ? 'Good morning' : h < 17 ? 'Good afternoon' : 'Good evening'; }
  if (d) d.textContent = new Date().toLocaleDateString([], { weekday: 'long', day: 'numeric', month: 'long' });
})();

// Numbers count up once.
(function () {
  var reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  document.querySelectorAll('[data-count]').forEach(function (el) {
    var target = parseInt(el.dataset.count, 10) || 0;
    if (reduce || target === 0) { el.textContent = target; return; }
    var t0 = null;
    function step(ts) { t0 = t0 || ts; var k = Math.min(1, (ts - t0) / 900); el.textContent = Math.round(target * (1 - Math.pow(1 - k, 3))); if (k < 1) requestAnimationFrame(step); }
    requestAnimationFrame(step);
  });
})();

// Sliding highlight behind the active navigation item.
(function () {
  var nav = document.getElementById('mainNav'), pill = document.getElementById('navPill');
  if (!nav || !pill) return;
  function place(el, instant) {
    if (!el) { pill.style.opacity = 0; return; }
    if (instant) pill.style.transition = 'none';
    pill.style.opacity = 1; pill.style.width = el.offsetWidth + 'px'; pill.style.transform = 'translateX(' + el.offsetLeft + 'px)';
    if (instant) { pill.offsetWidth; pill.style.transition = ''; }
  }
  var active = nav.querySelector('a.active');
  place(active, true);
  nav.querySelectorAll('a').forEach(function (a) {
    a.addEventListener('mouseenter', function () { place(a); });
    a.addEventListener('focus', function () { place(a); });
  });
  nav.addEventListener('mouseleave', function () { place(active); });
  window.addEventListener('resize', function () { place(active, true); });
})();

// Cards get a soft spotlight that follows the pointer.
document.querySelectorAll('.exam-card,.panel,.stat,.mini,.exam-row').forEach(function (c) {
  c.addEventListener('pointermove', function (e) {
    var r = c.getBoundingClientRect();
    c.style.setProperty('--mx', (e.clientX - r.left) + 'px'); c.style.setProperty('--my', (e.clientY - r.top) + 'px');
  });
});
