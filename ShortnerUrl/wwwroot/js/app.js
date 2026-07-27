const API_BASE = '/api';
const THEME_COOKIE = 'theme';
let state = { links: [], currentStats: null };

document.addEventListener('DOMContentLoaded', async () => {
  initTheme();
  initEventListeners();
  await ensureApiKey();
  loadLinks();
});

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
  document.getElementById('copyResultBtn').addEventListener('click', () => copyText(document.getElementById('resultUrl').textContent));
  document.getElementById('qrResultBtn').addEventListener('click', showQrModal);
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

  if (!val) {
    input.className = '';
    feedback.textContent = '';
    feedback.className = 'validation-feedback';
    return;
  }

  if (isValidUrl(val)) {
    input.className = 'input-valid';
    feedback.textContent = 'Valid URL';
    feedback.className = 'validation-feedback valid';
  } else {
    input.className = 'input-invalid';
    feedback.textContent = 'Enter a valid absolute http/https URL';
    feedback.className = 'validation-feedback invalid';
  }
}

function validateAliasInput() {
  const input = document.getElementById('aliasInput');
  const feedback = document.getElementById('aliasFeedback');
  const val = input.value.trim();

  if (!val) {
    input.className = '';
    feedback.textContent = '';
    feedback.className = 'validation-feedback';
    return;
  }

  const valid = /^[a-zA-Z0-9-]{3,30}$/.test(val);
  if (valid) {
    input.className = 'input-valid';
    feedback.textContent = 'Valid alias';
    feedback.className = 'validation-feedback valid';
  } else {
    input.className = 'input-invalid';
    feedback.textContent = '3-30 chars, letters/digits/hyphens only';
    feedback.className = 'validation-feedback invalid';
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
    const res = await fetch(`${API_BASE}/list`, {
      headers: { 'X-Api-Key': apiKey() || '' }
    });
    if (!res.ok) { wrap.innerHTML = ''; empty.style.display = 'flex'; return; }

    const links = await res.json();
    state.links = links;
    if (!links || links.length === 0) {
      wrap.innerHTML = '';
      empty.style.display = 'flex';
      return;
    }

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
  links.forEach(link => {
    const tr = document.createElement('tr');
    const expired = link.isExpired;
    tr.innerHTML = `
      <td><a href="${escapeHtml(link.shortUrl)}" target="_blank" class="url-cell">${escapeHtml(link.shortCode)}</a></td>
      <td><span class="url-cell" title="${escapeHtml(link.longUrl)}">${escapeHtml(link.longUrl)}</span></td>
      <td>${link.clicks}</td>
      <td>${new Date(link.createdAt).toLocaleDateString()}</td>
      <td>${expired ? '<span class="expired-badge">Expired</span>' : (link.expiresAt ? '<span style="color:var(--c-warning)">Active</span>' : '<span style="color:var(--c-success)">Active</span>')}</td>
      <td>
        <div style="display:flex;gap:4px">
          <button class="btn btn-sm btn-secondary btn-icon" onclick="copyText('${escapeHtml(link.shortUrl)}')" title="Copy" aria-label="Copy short URL"><i class="fas fa-copy"></i></button>
          <button class="btn btn-sm btn-secondary btn-icon" onclick="showStats('${escapeHtml(link.shortCode)}')" title="Stats" aria-label="View stats"><i class="fas fa-chart-bar"></i></button>
          <button class="btn btn-sm btn-secondary btn-icon" onclick="showQrFor('${escapeHtml(link.shortCode)}')" title="QR" aria-label="QR code"><i class="fas fa-qrcode"></i></button>
          <button class="btn btn-sm btn-danger btn-icon" onclick="deleteLink('${escapeHtml(link.shortCode)}')" title="Delete" aria-label="Delete"><i class="fas fa-trash"></i></button>
        </div>
      </td>
    `;
    tbody.appendChild(tr);
  });
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
        <div class="stat-card"><div class="stat-value">${stats.totalClicks}</div><div class="stat-label">Total Clicks</div></div>
        <div class="stat-card"><div class="stat-value">${stats.clicksOverTime ? stats.clicksOverTime.length : 0}</div><div class="stat-label">Days Active (30d)</div></div>
        <div class="stat-card"><div class="stat-value">${stats.topReferrers ? stats.topReferrers.length : 0}</div><div class="stat-label">Referrers</div></div>
      </div>
      <div class="chart-wrap">
        <canvas id="clicksChart"></canvas>
      </div>
      <h4 style="margin-bottom:8px">Top Referrers</h4>
      ${stats.topReferrers && stats.topReferrers.length > 0
        ? `<ul class="referrer-list">${stats.topReferrers.map(r => `<li><span>${escapeHtml(r.referrer) || '(direct)'}</span><span class="referrer-count">${r.count}</span></li>`).join('')}</ul>`
        : '<p style="color:var(--c-text-2);font-size:0.85rem">No referrer data yet.</p>'
      }
    `;

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
        borderRadius: 4
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: true,
      plugins: {
        legend: { display: false },
        tooltip: { backgroundColor: isDark ? '#1a1a2e' : '#fff', titleColor: textColor, bodyColor: textColor }
      },
      scales: {
        x: { ticks: { color: textColor, maxRotation: 45 }, grid: { color: isDark ? '#2d2d4a' : '#e2e4e9' } },
        y: { beginAtZero: true, ticks: { color: textColor, stepSize: 1 }, grid: { color: isDark ? '#2d2d4a' : '#e2e4e9' } }
      }
    }
  });
}

function showQrFor(code) {
  const overlay = document.getElementById('modalOverlay');
  document.getElementById('modalTitle').textContent = 'QR Code';
  document.getElementById('modalBody').innerHTML = `
    <p style="color:var(--c-text-2);margin-bottom:12px;text-align:center">Scan to visit: <strong>${escapeHtml(code)}</strong></p>
    <img src="${API_BASE}/urls/${encodeURIComponent(code)}/qr" alt="QR Code for ${escapeHtml(code)}" class="qr-img" />
  `;
  overlay.classList.add('open');
}

function showQrModal() {
  const code = state.links.find(l => l.shortUrl === document.getElementById('resultUrl').textContent);
  if (code) showQrFor(code.shortCode);
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

function copyText(text) {
  navigator.clipboard.writeText(text)
    .then(() => showToast('Copied!', 'success'))
    .catch(() => showToast('Failed to copy.', 'error'));
}

function closeModal() {
  document.getElementById('modalOverlay').classList.remove('open');
}

function showToast(msg, type) {
  const existing = document.querySelector('.toast');
  if (existing) existing.remove();

  const t = document.createElement('div');
  t.className = `toast toast-${type}`;
  t.textContent = msg;
  document.body.appendChild(t);
  setTimeout(() => { t.style.opacity = '0'; t.style.transition = 'opacity 0.3s'; setTimeout(() => t.remove(), 300); }, 3500);
}

function escapeHtml(s) {
  const d = document.createElement('div');
  d.textContent = s;
  return d.innerHTML;
}
