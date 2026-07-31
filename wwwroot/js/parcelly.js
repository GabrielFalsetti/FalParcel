window.parcelly = {
  get: (key) => localStorage.getItem(key),
  set: (key, value) => localStorage.setItem(key, value),
  remove: (key) => localStorage.removeItem(key),
  downloadText: (filename, content, mime) => {
    const blob = new Blob([content], { type: mime || 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  },
  downloadBytes: (filename, base64, mime) => {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    const blob = new Blob([bytes], { type: mime || 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  },
  // iOS: type=tel abre o teclado telefônico GRANDE (0-9).
  // Evite inputmode=numeric em campos com vírgula — o Safari pode cair no teclado ABC/123.
  forceNumberPad: (selectors, withPattern = true) => {
    const list = Array.isArray(selectors) ? selectors : [selectors];
    for (const selector of list) {
      document.querySelectorAll(selector).forEach((el) => {
        const apply = () => {
          if (el.type !== 'tel') el.type = 'tel';
          el.setAttribute('type', 'tel');
          el.removeAttribute('inputmode');
          try { el.inputMode = ''; } catch (_) { /* ignore */ }
          if (withPattern) el.setAttribute('pattern', '[0-9]*');
          else el.removeAttribute('pattern');
        };
        apply();
        if (el.dataset.padBound === '1') return;
        el.dataset.padBound = '1';
        el.addEventListener('touchstart', apply, { passive: true });
        el.addEventListener('focus', apply);
      });
    }
  }
};
