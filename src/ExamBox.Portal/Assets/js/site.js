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

// "Show password" checkboxes: <input type="checkbox" data-show-pw="InputId">
document.querySelectorAll('[data-show-pw]').forEach(function (c) {
  c.addEventListener('change', function () {
    var i = document.getElementById(c.dataset.showPw);
    if (i) i.type = c.checked ? 'text' : 'password';
  });
});
