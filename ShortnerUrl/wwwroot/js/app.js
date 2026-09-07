const API_BASE = '/api';
const THEME_COOKIE = 'theme';
let state = { links: [], currentStats: null };
let qrObjectUrl = null;

document.addEventListener('DOMContentLoaded', async () => {
  injectMeshBackground();
  initTheme();
  initEventListeners();
  initRippleEffect();
  await ensureApiKey();
  loadLinks();
});

/* ── Gradient Mesh Background ── */
function injectMeshBackground() {
  const mesh = document.createElement('div');
  mesh.className = 'mesh-bg';
  mesh.innerHTML = '<div class="mesh-blob"></div><div class="mesh-blob"></div><div class="mesh-blob"></div>';
  document.body.prepend(mesh);
}

/* ── Button Ripple Effect ── */
function initRippleEffect() {
  document.addEventListener('mousedown', e => {
    const btn = e.target.closest('.btn');
    if (!btn) return;
    const rect = btn.getBoundingClientRect();
    const x = ((e.clientX - rect.left) / rect.width * 100).toFixed(0);
    const y = ((e.clientY - rect.top) / rect.height * 100).toFixed(0);
    btn.style.setProperty('--ripple-x', x + '%');
    btn.style.setProperty('--ripple-y', y + '%');
  });
}

/* ── Theme ── */
function initTheme() {
  const saved = getCookie(THEME_COOKIE) || 'light';
  applyTheme(saved);
  document.getElementById('themeToggle').addEventListener('click', () => {
    const next = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
    applyTheme(next);
    setCookie(THEME_COOKIE, next, 365);
  });
}

function applyTheme(theme) {
  document.documentElement.setAttribute('data-theme', theme);
  const icon = document.querySelector('#themeToggle i');
  icon.className = theme === 'dark' ? 'fas fa-sun' : 'fas fa-moon';
}

function getCookie(name) {
  const match = document.cookie.match(new RegExp(`(^| )${name}=([^;]+)`));
  return match ? match[2] : null;
}

function setCookie(name, value, days) {
  const d = new Date();
  d.setTime(d.getTime() + days * 864e5);
  document.cookie = `${name}=${value};path=/;expires=${d.toUTCString()};SameSite=Lax`;
}

async function ensureApiKey() {
  let key = getCookie('apiKey');
  if (key) return key;
  try {
    const res = await fetch(`${API_BASE}/keys/create`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ workspaceName: 'Default' })
    });
    if (res.ok) {
      const data = await res.json();
      key = data.plainTextKey;
      setCookie('apiKey', key, 365);
    }
  } catch {}
  return key;
}

function apiKey() {
  return getCookie('apiKey') || '';
}

function initEventListeners() {
  document.getElementById('shortenBtn').addEventListener('click', shortenUrl);
  document.getElementById('urlInput').addEventListener('input', validateUrlInput);
  document.getElementById('urlInput').addEventListener('keypress', e => { if (e.key === 'Enter') shortenUrl(); });
  document.getElementById('advancedToggle').addEventListener('click', toggleAdvanced);
  document.getElementById('advancedContent').querySelector('input').addEventListener('input', validateAliasInput);
  document.getElementById('copyResultBtn').addEventListener('click', e => copyFromButton(e.currentTarget, document.getElementById('resultUrl').textContent));
  document.getElementById('qrResultBtn').addEventListener('click', showQrModal);
  document.getElementById('qrGenerateBtn').addEventListener('click', generateQrCode);
  document.getElementById('qrUrlInput').addEventListener('input', validateQrUrlInput);
  document.getElementById('modalClose').addEventListener('click', closeModal);
  document.getElementById('modalOverlay').addEventListener('click', e => { if (e.target === e.currentTarget) closeModal(); });
  document.addEventListener('keydown', e => { if (e.key === 'Escape') closeModal(); });
}

function toggleAdvanced() {
  const btn = document.getElementById('advancedToggle');
  const content = document.getElementById('advancedContent');
  btn.classList.toggle('open');
  content.classList.toggle('open');
}

/* ── Particle Explosion ── */
function createParticleExplosion(originEl) {
  let canvas = document.getElementById('particleCanvas');
  if (!canvas) {
    canvas = document.createElement('canvas');
    canvas.id = 'particleCanvas';
    document.body.appendChild(canvas);
  }
  canvas.width = window.innerWidth;
  canvas.height = window.innerHeight;
  const ctx = canvas.getContext('2d');
  const rect = originEl.getBoundingClientRect();
  const cx = rect.left + rect.width / 2;
  const cy = rect.top + rect.height / 2;
  const colors = ['#6c5ce7', '#00b894', '#fdcb6e', '#e17055', '#74b9ff', '#a29bfe', '#55efc4', '#fd79a8'];
  const particles = [];

  for (let i = 0; i < 40; i++) {
    const angle = (Math.PI * 2 * i) / 40 + (Math.random() - 0.5) * 0.5;
    const speed = 3 + Math.random() * 6;
    const size = 3 + Math.random() * 5;
    particles.push({
      x: cx, y: cy,
      vx: Math.cos(angle) * speed,
      vy: Math.sin(angle) * speed,
      size,
      color: colors[Math.floor(Math.random() * colors.length)],
      life: 1,
      decay: 0.015 + Math.random() * 0.015,
      gravity: 0.08 + Math.random() * 0.04,
      shape: Math.random() > 0.5 ? 'circle' : 'rect',
      rotation: Math.random() * Math.PI * 2,
      rotSpeed: (Math.random() - 0.5) * 0.2
    });
  }

  function animate() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    let alive = false;
    for (const p of particles) {
      if (p.life <= 0) continue;
      alive = true;
      p.x += p.vx;
      p.y += p.vy;
      p.vy += p.gravity;
      p.vx *= 0.98;
      p.life -= p.decay;
      p.rotation += p.rotSpeed;
      ctx.save();
      ctx.translate(p.x, p.y);
      ctx.rotate(p.rotation);
      ctx.globalAlpha = Math.max(0, p.life);
      ctx.fillStyle = p.color;
      if (p.shape === 'circle') {
        ctx.beginPath();
        ctx.arc(0, 0, p.size, 0, Math.PI * 2);
        ctx.fill();
      } else {
        ctx.fillRect(-p.size / 2, -p.size / 2, p.size, p.size);
      }
      ctx.restore();
    }
    if (alive) requestAnimationFrame(animate);
    else ctx.clearRect(0, 0, canvas.width, canvas.height);
  }
  requestAnimationFrame(animate);
}

/* ── Animated Counter (Odometer) ── */
function animateCounter(el, target) {
  const duration = 600;
  const start = performance.now();
  const from = 0;
  function tick(now) {
    const t = Math.min((now - start) / duration, 1);
    const eased = 1 - Math.pow(1 - t, 3);
    const val = Math.round(from + (target - from) * eased);
    el.textContent = val.toLocaleString();
    if (t < 1) requestAnimationFrame(tick);
  }
  requestAnimationFrame(tick);
}

/* ── Copy Micro-interaction ── */
function copyFromButton(btn, text) {
  navigator.clipboard.writeText(text).then(() => {
    const origHtml = btn.innerHTML;
    btn.classList.add('btn-copy-success');
    btn.innerHTML = '<i class="fas fa-check"></i>';
    showToast('Copied!', 'success');
    setTimeout(() => {
      btn.classList.remove('btn-copy-success');
      btn.innerHTML = origHtml;
    }, 1500);
  }).catch(() => showToast('Failed to copy.', 'error'));
}

function copyText(text) {
  navigator.clipboard.writeText(text)
    .then(() => showToast('Copied!', 'success'))
    .catch(() => showToast('Failed to copy.', 'error'));
}

/* ── Shorten URL ── */
async function shortenUrl() {
  const input = document.getElementById('urlInput');
  const url = input.value.trim();
  const alias = document.getElementById('aliasInput').value.trim() || null;
  const expires = document.getElementById('expiresInput').value || null;

  if (!url) return showToast('Please enter a URL.', 'error');
  if (!isValidUrl(url)) return showToast('Please enter a valid http or https URL.', 'error');

  const btn = document.getElementById('shortenBtn');
  btn.disabled = true;
  btn.innerHTML = '<i class="fas fa-spinner fa-pulse"></i> Shortening...';

  try {
    const body = { url };
    if (alias) body.customAlias = alias;
    if (expires) body.expiresAt = new Date(expires).toISOString();

    const res = await fetch(`${API_BASE}/shorten`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Api-Key': apiKey() || '' },
      body: JSON.stringify(body)
    });

    if (res.status === 429) throw new Error('Rate limit exceeded. Please wait a moment.');
    if (!res.ok) {
      const err = await res.json().catch(() => ({ detail: res.statusText }));
      throw new Error(err.detail || err.title || 'Request failed');
    }

    const data = await res.json();
    const resultUrl = document.getElementById('resultUrl');
    resultUrl.textContent = data.shortUrl;
    resultUrl.href = data.shortUrl;
    document.getElementById('resultCard').classList.add('show');

    // Particle explosion from the button
    createParticleExplosion(btn);
    btn.classList.add('btn-pulse');
    setTimeout(() => btn.classList.remove('btn-pulse'), 600);

    showToast('URL shortened successfully!', 'success');
    loadLinks();
    document.getElementById('urlInput').value = '';
    document.getElementById('aliasInput').value = '';
    document.getElementById('expiresInput').value = '';
    document.getElementById('advancedContent').classList.remove('open');
    document.getElementById('advancedToggle').classList.remove('open');
  } catch (err) {
    showToast(err.message, 'error');
  } finally {
    btn.disabled = false;
    btn.innerHTML = '<i class="fas fa-bolt"></i> Shorten URL';
  }
}

function validateUrlInput() {
  const input = document.getElementById('urlInput');
  const feedback = document.getElementById('urlFeedback');
  const val = input.value.trim();
  if (!val) { input.className = ''; feedback.textContent = ''; feedback.className = 'validation-feedback'; return; }
  if (isValidUrl(val)) {
    input.className = 'input-valid'; feedback.textContent = 'Valid URL'; feedback.className = 'validation-feedback valid';
  } else {
    input.className = 'input-invalid'; feedback.textContent = 'Enter a valid absolute http/https URL'; feedback.className = 'validation-feedback invalid';
  }
}

function validateAliasInput() {
  const input = document.getElementById('aliasInput');
  const feedback = document.getElementById('aliasFeedback');
  const val = input.value.trim();
  if (!val) { input.className = ''; feedback.textContent = ''; feedback.className = 'validation-feedback'; return; }
  if (/^[a-zA-Z0-9-]{3,30}$/.test(val)) {
    input.className = 'input-valid'; feedback.textContent = 'Valid alias'; feedback.className = 'validation-feedback valid';
  } else {
    input.className = 'input-invalid'; feedback.textContent = '3-30 chars, letters/digits/hyphens only'; feedback.className = 'validation-feedback invalid';
  }
}

function validateQrUrlInput() {
  const input = document.getElementById('qrUrlInput');
  const feedback = document.getElementById('qrUrlFeedback');
  const val = input.value.trim();
  if (!val) { input.className = ''; feedback.textContent = ''; feedback.className = 'validation-feedback'; return; }
  if (isValidUrl(val)) {
    input.className = 'input-valid'; feedback.textContent = 'Valid URL'; feedback.className = 'validation-feedback valid';
  } else {
    input.className = 'input-invalid'; feedback.textContent = 'Enter a valid absolute http/https URL'; feedback.className = 'validation-feedback invalid';
  }
}

async function generateQrCode() {
  const input = document.getElementById('qrUrlInput');
  const url = input.value.trim();
  const fgColor = document.getElementById('qrFgColor')?.value || null;
  const bgColor = document.getElementById('qrBgColor')?.value || null;

  if (!url) return showToast('Please enter a URL.', 'error');
  if (!isValidUrl(url)) return showToast('Please enter a valid http or https URL.', 'error');

  const btn = document.getElementById('qrGenerateBtn');
  btn.disabled = true;
  btn.innerHTML = '<i class="fas fa-spinner fa-pulse"></i> Generating...';

  try {
    const body = { url };
    if (fgColor) body.foregroundColor = fgColor;
    if (bgColor) body.backgroundColor = bgColor;

    const res = await fetch(`${API_BASE}/qr`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Api-Key': apiKey() || '' },
      body: JSON.stringify(body)
    });

    if (res.status === 401) throw new Error('API key missing — refresh the page.');
    if (!res.ok) {
      const err = await res.json().catch(() => ({ detail: res.statusText }));
      throw new Error(err.error || err.detail || 'Request failed');
    }

    const blob = await res.blob();
    qrObjectUrl = URL.createObjectURL(blob);

    const overlay = document.getElementById('modalOverlay');
    document.getElementById('modalTitle').textContent = 'QR Code';
    document.getElementById('modalBody').innerHTML = `
      <p style="color:var(--c-text-2);margin-bottom:12px;text-align:center;word-break:break-all">${escapeHtml(url)}</p>
      <img src="${qrObjectUrl}" alt="QR Code" class="qr-img" />
      <div style="text-align:center;margin-top:16px">
        <a class="btn btn-sm btn-primary" href="${qrObjectUrl}" download="qr-code.png"><i class="fas fa-download"></i> Download PNG</a>
      </div>
    `;
    overlay.classList.add('open');
  } catch (err) {
    showToast(err.message, 'error');
  } finally {
    btn.disabled = false;
    btn.innerHTML = '<i class="fas fa-qrcode"></i> Generate QR Code';
  }
}

function isValidUrl(s) {
  try { const u = new URL(s); return u.protocol === 'http:' || u.protocol === 'https:'; }
  catch { return false; }
}

async function loadLinks() {
  const wrap = document.getElementById('linksWrap');
  const empty = document.getElementById('emptyState');

  wrap.innerHTML = '<div class="skeleton skeleton-lg"></div><div class="skeleton"></div><div class="skeleton skeleton-sm"></div>';

  try {
    const res = await fetch(`${API_BASE}/list`, { headers: { 'X-Api-Key': apiKey() || '' } });
    if (!res.ok) { wrap.innerHTML = ''; empty.style.display = 'flex'; return; }

    const links = await res.json();
    state.links = links;
    if (!links || links.length === 0) { wrap.innerHTML = ''; empty.style.display = 'flex'; return; }

    empty.style.display = 'none';
    renderLinksTable(links);
  } catch {
    wrap.innerHTML = '';
    empty.style.display = 'flex';
  }
}

function renderLinksTable(links) {
  const wrap = document.getElementById('linksWrap');
  wrap.innerHTML = `
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Short URL</th>
            <th>Destination</th>
            <th>Clicks</th>
            <th>Created</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody id="linksBody"></tbody>
      </table>
    </div>
  `;

  const tbody = document.getElementById('linksBody');
  links.forEach((link, idx) => {
    const tr = document.createElement('tr');
    tr.className = 'row-enter';
    tr.style.animationDelay = (idx * 0.05) + 's';

    const expired = link.isExpired;
    const inactive = !link.isActive;
    const statusHtml = inactive
      ? '<span class="inactive-badge">Inactive</span>'
      : expired
        ? '<span class="expired-badge">Expired</span>'
        : '<span class="active-badge">Active</span>';

    const toggleIcon = link.isActive ? 'fa-toggle-on' : 'fa-toggle-off';
    const toggleTitle = link.isActive ? 'Deactivate' : 'Activate';

    tr.innerHTML = `
      <td><a href="${escapeHtml(link.shortUrl)}" target="_blank" class="url-cell">${escapeHtml(link.shortCode)}</a></td>
      <td><span class="url-cell" title="${escapeHtml(link.longUrl)}">${escapeHtml(link.longUrl)}</span></td>
      <td class="click-count" data-count="${link.clicks}">${link.clicks}</td>
      <td>${new Date(link.createdAt).toLocaleDateString()}</td>
      <td>${statusHtml}</td>
      <td>
        <div style="display:flex;gap:4px">
          <button class="btn btn-sm btn-secondary btn-icon copy-btn" data-url="${escapeHtml(link.shortUrl)}" title="Copy" aria-label="Copy short URL"><i class="fas fa-copy"></i></button>
          <button class="btn btn-sm btn-secondary btn-icon" onclick="showStats('${escapeHtml(link.shortCode)}')" title="Stats" aria-label="View stats"><i class="fas fa-chart-bar"></i></button>
          <button class="btn btn-sm btn-secondary btn-icon" onclick="showQrFor('${escapeHtml(link.shortCode)}')" title="QR" aria-label="QR code"><i class="fas fa-qrcode"></i></button>
          <button class="btn btn-sm btn-secondary btn-icon" onclick="toggleLink('${escapeHtml(link.shortCode)}', ${!link.isActive})" title="${toggleTitle}" aria-label="${toggleTitle}"><i class="fas ${toggleIcon}"></i></button>
          <button class="btn btn-sm btn-secondary btn-icon" onclick="showEditModal('${escapeHtml(link.shortCode)}')" title="Edit" aria-label="Edit"><i class="fas fa-pen"></i></button>
          <button class="btn btn-sm btn-danger btn-icon" onclick="deleteLink('${escapeHtml(link.shortCode)}')" title="Delete" aria-label="Delete"><i class="fas fa-trash"></i></button>
        </div>
      </td>
    `;
    tbody.appendChild(tr);
  });

  // Attach copy handlers with micro-interaction
  document.querySelectorAll('.copy-btn').forEach(btn => {
    btn.addEventListener('click', () => copyFromButton(btn, btn.dataset.url));
  });

  // Animate click counters
  document.querySelectorAll('.click-count').forEach(el => {
    const count = parseInt(el.dataset.count, 10);
    if (count > 0) animateCounter(el, count);
  });
}

async function toggleLink(code, activate) {
  try {
    const res = await fetch(`${API_BASE}/urls/${encodeURIComponent(code)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', 'X-Api-Key': apiKey() || '' },
      body: JSON.stringify({ isActive: activate })
    });
    if (!res.ok) throw new Error('Update failed');
    showToast(activate ? 'Link activated.' : 'Link deactivated.', 'success');
    loadLinks();
  } catch {
    showToast('Failed to update link.', 'error');
  }
}

function showEditModal(code) {
  const link = state.links.find(l => l.shortCode === code);
  if (!link) return;

  const overlay = document.getElementById('modalOverlay');
  document.getElementById('modalTitle').textContent = `Edit: ${code}`;
  document.getElementById('modalBody').innerHTML = `
    <div class="input-group">
      <label for="editUrlInput">Destination URL</label>
      <input type="url" id="editUrlInput" value="${escapeHtml(link.longUrl)}" />
    </div>
    <div class="input-group">
      <label for="editExpiresInput">Expires at</label>
      <input type="datetime-local" id="editExpiresInput" value="${link.expiresAt ? new Date(link.expiresAt).toISOString().slice(0, 16) : ''}" />
    </div>
    <button class="btn btn-primary btn-full" onclick="saveEdit('${escapeHtml(code)}')"><i class="fas fa-save"></i> Save Changes</button>
  `;
  overlay.classList.add('open');
}

async function saveEdit(code) {
  const url = document.getElementById('editUrlInput').value.trim();
  const expires = document.getElementById('editExpiresInput').value;
  if (url && !isValidUrl(url)) return showToast('Please enter a valid URL.', 'error');

  const body = {};
  if (url) body.url = url;
  if (expires) body.expiresAt = new Date(expires).toISOString();

  try {
    const res = await fetch(`${API_BASE}/urls/${encodeURIComponent(code)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', 'X-Api-Key': apiKey() || '' },
      body: JSON.stringify(body)
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({ detail: 'Update failed' }));
      throw new Error(err.detail || err.error || 'Update failed');
    }
    showToast('Link updated!', 'success');
    closeModal();
    loadLinks();
  } catch (err) {
    showToast(err.message, 'error');
  }
}

async function showStats(code) {
  const overlay = document.getElementById('modalOverlay');
  const title = document.getElementById('modalTitle');
  const body = document.getElementById('modalBody');

  title.textContent = 'Loading stats...';
  body.innerHTML = '<div class="skeleton skeleton-lg"></div><div class="skeleton"></div><div class="skeleton skeleton-sm"></div>';
  overlay.classList.add('open');

  try {
    const res = await fetch(`${API_BASE}/urls/${encodeURIComponent(code)}/stats`, {
      headers: { 'X-Api-Key': apiKey() || '' }
    });
    if (!res.ok) { body.innerHTML = '<p class="empty-state">Failed to load stats.</p>'; return; }

    const stats = await res.json();
    state.currentStats = stats;
    title.textContent = `Stats: ${stats.shortCode}`;

    body.innerHTML = `
      <div class="stats-grid">
        <div class="stat-card"><div class="stat-value" data-animate="${stats.totalClicks}">0</div><div class="stat-label">Total Clicks</div></div>
        <div class="stat-card"><div class="stat-value" data-animate="${stats.clicksOverTime ? stats.clicksOverTime.length : 0}">0</div><div class="stat-label">Days Active (30d)</div></div>
        <div class="stat-card"><div class="stat-value" data-animate="${stats.topReferrers ? stats.topReferrers.length : 0}">0</div><div class="stat-label">Referrers</div></div>
      </div>
      <div class="chart-wrap">
        <canvas id="clicksChart"></canvas>
      </div>
      <h4 style="margin-bottom:8px">Top Referrers</h4>
      ${stats.topReferrers && stats.topReferrers.length > 0
        ? `<ul class="referrer-list">${stats.topReferrers.map(r => `<li><span>${escapeHtml(r.referrer) || '(direct)'}</span><span class="referrer-count">${r.count}</span></li>`).join('')}</ul>`
        : '<p style="color:var(--c-text-2);font-size:0.85rem">No referrer data yet.</p>'
      }
      ${stats.topBrowsers && stats.topBrowsers.length > 0
        ? `<h4 style="margin:16px 0 8px">Top Browsers</h4>
           <ul class="referrer-list">${stats.topBrowsers.map(b => `<li><span>${escapeHtml(b.browser)}</span><span class="referrer-count">${b.count}</span></li>`).join('')}</ul>`
        : ''
      }
    `;

    // Animate stat counters
    document.querySelectorAll('.stat-value[data-animate]').forEach(el => {
      animateCounter(el, parseInt(el.dataset.animate, 10));
    });

    renderChart(stats.clicksOverTime || []);
  } catch {
    body.innerHTML = '<p class="empty-state">Failed to load stats.</p>';
  }
}

function renderChart(data) {
  const canvas = document.getElementById('clicksChart');
  if (!canvas) return;

  const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
  const textColor = isDark ? '#9ca3af' : '#6b7280';

  new Chart(canvas, {
    type: 'bar',
    data: {
      labels: data.map(d => d.date),
      datasets: [{
        label: 'Clicks',
        data: data.map(d => d.count),
        backgroundColor: 'rgba(108,92,231,0.6)',
        borderColor: '#6c5ce7',
        borderWidth: 2,
        borderRadius: 6
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: true,
      animation: { duration: 800, easing: 'easeOutQuart' },
      plugins: {
        legend: { display: false },
        tooltip: {
          backgroundColor: isDark ? '#1a1a2e' : '#fff',
          titleColor: textColor,
          bodyColor: textColor,
          borderColor: isDark ? '#2d2d4a' : '#e2e4e9',
          borderWidth: 1,
          cornerRadius: 8,
          padding: 10
        }
      },
      scales: {
        x: { ticks: { color: textColor, maxRotation: 45 }, grid: { color: isDark ? '#2d2d4a' : '#e2e4e9' } },
        y: { beginAtZero: true, ticks: { color: textColor, stepSize: 1 }, grid: { color: isDark ? '#2d2d4a' : '#e2e4e9' } }
      }
    }
  });
}

async function showQrFor(code) {
  const overlay = document.getElementById('modalOverlay');
  document.getElementById('modalTitle').textContent = 'QR Code';
  document.getElementById('modalBody').innerHTML = '<div class="skeleton skeleton-lg"></div>';
  overlay.classList.add('open');

  try {
    const res = await fetch(`${API_BASE}/urls/${encodeURIComponent(code)}/qr`, {
      headers: { 'X-Api-Key': apiKey() || '' }
    });
    if (!res.ok) throw new Error('Failed to load QR code');

    const blob = await res.blob();
    if (qrObjectUrl) URL.revokeObjectURL(qrObjectUrl);
    qrObjectUrl = URL.createObjectURL(blob);

    document.getElementById('modalBody').innerHTML = `
      <p style="color:var(--c-text-2);margin-bottom:12px;text-align:center">Scan to visit: <strong>${escapeHtml(code)}</strong></p>
      <img src="${qrObjectUrl}" alt="QR Code for ${escapeHtml(code)}" class="qr-img" />
      <div style="text-align:center;margin-top:16px">
        <a class="btn btn-sm btn-primary" href="${qrObjectUrl}" download="qr-${escapeHtml(code)}.png"><i class="fas fa-download"></i> Download PNG</a>
      </div>
    `;
  } catch (err) {
    document.getElementById('modalBody').innerHTML = '<p class="empty-state">Failed to load QR code.</p>';
    showToast(err.message, 'error');
  }
}

function showQrModal() {
  const resultUrl = document.getElementById('resultUrl').textContent;
  if (!resultUrl) return;
  const parts = resultUrl.replace(/\/+$/, '').split('/');
  const code = parts[parts.length - 1];
  if (code) showQrFor(code);
}

async function deleteLink(code) {
  if (!confirm(`Delete short URL "${code}"? This cannot be undone.`)) return;
  try {
    const res = await fetch(`${API_BASE}/${encodeURIComponent(code)}`, {
      method: 'DELETE',
      headers: { 'X-Api-Key': apiKey() || '' }
    });
    if (!res.ok) throw new Error('Delete failed');
    showToast('Link deleted.', 'success');
    loadLinks();
  } catch {
    showToast('Failed to delete link.', 'error');
  }
}

function closeModal() {
  const overlay = document.getElementById('modalOverlay');
  const modal = overlay.querySelector('.modal');
  if (modal) {
    modal.style.animation = 'modalOut 0.2s ease forwards';
    setTimeout(() => {
      overlay.classList.remove('open');
      modal.style.animation = '';
      if (qrObjectUrl) { URL.revokeObjectURL(qrObjectUrl); qrObjectUrl = null; }
    }, 200);
  } else {
    overlay.classList.remove('open');
  }
}

// Inject modal-out keyframe dynamically
(() => {
  const style = document.createElement('style');
  style.textContent = '@keyframes modalOut{to{opacity:0;transform:scale(0.95) translateY(8px)}}';
  document.head.appendChild(style);
})();

function showToast(msg, type) {
  const existing = document.querySelector('.toast');
  if (existing) existing.remove();

  const icons = { success: 'fa-check-circle', error: 'fa-exclamation-circle', info: 'fa-info-circle' };
  const t = document.createElement('div');
  t.className = `toast toast-${type}`;
  t.innerHTML = `<i class="fas ${icons[type] || icons.info}"></i> ${escapeHtml(msg)}`;
  document.body.appendChild(t);
  setTimeout(() => {
    t.style.opacity = '0';
    t.style.transform = 'translateX(-50%) translateY(10px)';
    t.style.transition = 'all 0.3s ease';
    setTimeout(() => t.remove(), 300);
  }, 3500);
}

function escapeHtml(s) {
  const d = document.createElement('div');
  d.textContent = s;
  return d.innerHTML;
}
