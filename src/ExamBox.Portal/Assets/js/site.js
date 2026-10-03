// Render UTC timestamps in the viewer's local time.
document.querySelectorAll('time[datetime]').forEach(function (t) {
  var d = new Date(t.getAttribute('datetime'));
  if (isNaN(d)) return;
  t.textContent = t.dataset.fmt === 'date' ? d.toLocaleDateString() : d.toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
});
document.querySelectorAll('form[data-confirm]').forEach(function (f) {
  f.addEventListener('submit', function (e) { if (!confirm(f.dataset.confirm)) e.preventDefault(); });
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
