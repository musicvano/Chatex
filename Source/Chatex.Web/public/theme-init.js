// Apply the saved theme before the first paint so dark mode doesn't flash white.
// A file rather than an inline script, so the Content-Security-Policy can stay script-src 'self'.
(function () {
  try {
    var t = localStorage.getItem('chatex.theme') || 'system';
    var dark =
      t === 'dark' || (t === 'system' && matchMedia('(prefers-color-scheme: dark)').matches);
    document.documentElement.classList.toggle('dark', dark);
  } catch (e) {}
})();
