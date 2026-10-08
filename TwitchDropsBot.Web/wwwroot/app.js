// TwitchDropsBot web UI: plain ES module, no build step. Talks to the bot's /api and listens to /api/events.

const NOTE_TEXT = {
  NoLiveChannel: 'No live channel',
  Completed: 'All drops earned',
  NotEnoughTime: 'Not enough time left to finish',
  NoWatchableDrops: 'Nothing watchable (subscriber-only or no timed drops)',
  NothingLeftOnChannel: 'Nothing left to earn on the live channel',
  WatchedNotClaimed: 'Watched, waiting for the claim',
  CategoryNotFound: 'Game category not found on Twitch',
};

const IDLE_TEXT = {
  cycleFinished: 'Finished a drop, picking the next one',
  nothingToWatch: 'Nothing to watch right now',
  streamOffline: 'The stream went offline',
  dropSessionChanged: 'The drop session changed',
  switching: 'Switching campaigns',
  error: 'Something went wrong',
};

const FILTERS = [
  ['all', 'All'],
  ['favourites', 'Favourites'],
  ['queued', 'Queued'],
  ['progress', 'In progress'],
  ['unclaimed', 'Unclaimed'],
  ['done', 'Done'],
  ['linked', 'Linked / no link'],
  ['ending', 'Ending soon'],
];

const LOG_LEVELS = [
  ['info', 'Info'],
  ['all', 'Everything'],
  ['problems', 'Problems'],
];

const TABS = [
  ['now', 'Now', 'now'],
  ['queue', 'Queue', 'queue'],
  ['campaigns', 'Campaigns', 'grid'],
  ['activity', 'Activity', 'activity'],
];

const KEY = { account: 'tdb.account', tab: 'tdb.tab', right: 'tdb.right', theme: 'tdb.theme', filter: 'tdb.filter', sort: 'tdb.sort', logLevel: 'tdb.logLevel' };

const store = {
  session: null,
  state: null,
  skew: 0,
  connected: false,
  everConnected: false,
  accountId: localStorage.getItem(KEY.account),
  tab: localStorage.getItem(KEY.tab) || 'now',
  right: localStorage.getItem(KEY.right) || 'campaigns',
  filter: localStorage.getItem(KEY.filter) || 'all',
  sort: localStorage.getItem(KEY.sort) || 'ending',
  search: '',
  campaigns: new Map(),
  logs: new Map(),
  logLevel: localStorage.getItem(KEY.logLevel) || 'info',
  logFollow: true,
  sheet: null,
  dragging: false,
};

const desktop = matchMedia('(min-width: 960px)');
const $ = (selector, root = document) => root.querySelector(selector);
const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];

// ---------------------------------------------------------------- templating

const ESCAPES = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
const escapeHtml = (value) => String(value).replace(/[&<>"']/g, (c) => ESCAPES[c]);

class Raw {
  constructor(text) { this.text = text; }
  toString() { return this.text; }
}

const raw = (text) => new Raw(text);

function fragment(value) {
  if (value instanceof Raw) return value.text;
  if (Array.isArray(value)) return value.map(fragment).join('');
  if (value === null || value === undefined || value === false) return '';
  return escapeHtml(value);
}

// Interpolated values are escaped unless they are Raw (built by html`` itself)
function html(strings, ...values) {
  let out = strings[0];
  values.forEach((value, i) => { out += fragment(value) + strings[i + 1]; });
  return raw(out);
}

const icon = (name, cls = '') => raw(`<svg class="icon ${cls}" aria-hidden="true"><use href="#i-${name}"/></svg>`);

// Only https images and links from the API are used
const safeUrl = (url) => (typeof url === 'string' && /^https:\/\//i.test(url) ? url : null);

function initials(name) {
  const words = String(name || '?').replace(/[^\p{L}\p{N} ]/gu, ' ').split(/\s+/).filter(Boolean);
  if (words.length === 0) return '?';
  if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
  return (words[0][0] + words[1][0]).toUpperCase();
}

function art(url, name, size = '') {
  const src = safeUrl(url);
  return html`<span class="art ${size}" data-initials="${initials(name)}">${src ? html`<img src="${src}" alt="" loading="lazy" decoding="async" referrerpolicy="no-referrer">` : ''}</span>`;
}

function rewardImage(url) {
  const src = safeUrl(url);
  return html`<span class="reward">${src ? html`<img src="${src}" alt="" loading="lazy" referrerpolicy="no-referrer">` : icon('gift')}</span>`;
}

function pct(current, required) {
  if (!required) return 0;
  return Math.max(0, Math.min(100, Math.floor((current / required) * 100)));
}

function meter(current, required, size = '') {
  const value = pct(current, required);
  return html`<div class="meter ${size} ${value >= 100 ? 'done' : ''}" role="progressbar" aria-valuemin="0" aria-valuemax="${required || 0}" aria-valuenow="${Math.min(current || 0, required || 0)}"><i data-pct="${value}"></i></div>`;
}

// Inline style attributes are blocked by the page's CSP, so widths are set from script
function applyMeters(root) {
  for (const bar of $$('[data-pct]', root)) bar.style.width = `${bar.dataset.pct}%`;
}

function setHtml(element, content) {
  element.innerHTML = fragment(content);
  applyMeters(element);
}

// ---------------------------------------------------------------- time

const now = () => Date.now() + store.skew;
const clockFormat = new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' });
const dayFormat = new Intl.DateTimeFormat(undefined, { weekday: 'short', hour: 'numeric', minute: '2-digit' });
const dateFormat = new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });

function minutesText(minutes) {
  minutes = Math.max(0, Math.round(minutes));
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest ? `${hours} h ${rest} min` : `${hours} h`;
}

function spanText(ms) {
  const minutes = Math.round(ms / 60000);
  if (minutes < 1) return 'less than a minute';
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return minutes % 60 ? `${hours}h ${minutes % 60}m` : `${hours}h`;
  const days = Math.floor(hours / 24);
  return hours % 24 ? `${days}d ${hours % 24}h` : `${days}d`;
}

function untilText(iso) {
  if (!iso) return 'no end date';
  const ms = new Date(iso) - now();
  return ms <= 0 ? 'ended' : `in ${spanText(ms)}`;
}

function endsText(iso) {
  if (!iso) return 'no end date';
  const ms = new Date(iso) - now();
  return ms <= 0 ? 'ended' : `ends in ${spanText(ms)}`;
}

function agoText(iso) {
  const ms = now() - new Date(iso);
  if (ms < 60000) return 'just now';
  return `${spanText(ms)} ago`;
}

function countdownText(iso) {
  const seconds = Math.max(0, Math.round((new Date(iso) - now()) / 1000));
  if (seconds === 0) return 'any moment';
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  return h ? `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}` : `${m}:${String(s).padStart(2, '0')}`;
}

function whenText(iso) {
  const date = new Date(iso);
  const ms = date - now();
  if (Math.abs(ms) < 20 * 3600e3 && new Date(now()).getDate() === date.getDate()) return clockFormat.format(date);
  if (Math.abs(ms) < 6 * 86400e3) return dayFormat.format(date);
  return dateFormat.format(date);
}

const live = (kind, iso, text) => html`<span data-${kind}="${iso}">${text}</span>`;

function tickClocks() {
  for (const el of $$('[data-countdown]')) el.textContent = countdownText(el.dataset.countdown);
  for (const el of $$('[data-until]')) el.textContent = untilText(el.dataset.until);
  for (const el of $$('[data-ends]')) el.textContent = endsText(el.dataset.ends);
  for (const el of $$('[data-ago]')) el.textContent = agoText(el.dataset.ago);
}

// ---------------------------------------------------------------- api

class ApiError extends Error {
  constructor(status, message) { super(message); this.status = status; }
}

async function api(path, { method = 'GET', body, auth = true } = {}) {
  const headers = { Accept: 'application/json' };
  if (method !== 'GET') {
    headers['Content-Type'] = 'application/json';
    headers['X-TDB-Request'] = '1';
  }

  let response;
  try {
    response = await fetch(path, { method, headers, credentials: 'same-origin', body: body === undefined ? undefined : JSON.stringify(body) });
  } catch {
    throw new ApiError(0, 'Can\'t reach the bot. Check the connection.');
  }

  if (response.status === 401 && auth) {
    signedOut('Your session ended. Sign in again.');
    throw new ApiError(401, 'Signed out');
  }

  const text = await response.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch { data = null; }

  if (!response.ok) throw new ApiError(response.status, data?.error || `The bot answered ${response.status}.`);
  return data;
}

const enc = encodeURIComponent;

// ---------------------------------------------------------------- live state

let events = null;
let reconnectTimer = null;

function connect() {
  clearTimeout(reconnectTimer);
  events?.close();
  events = new EventSource('/api/events');

  events.addEventListener('state', (event) => {
    const message = JSON.parse(event.data);
    store.skew = new Date(message.serverTime) - Date.now();
    setConnected(true);
    setState(message.state);
  });

  events.onerror = () => {
    setConnected(false);
    if (events.readyState === EventSource.CLOSED) {
      reconnectTimer = setTimeout(checkAndReconnect, 4000);
    }
  };
}

async function checkAndReconnect() {
  try {
    const session = await api('/api/session', { auth: false });
    if (!session.authenticated) {
      signedOut();
      return;
    }
  } catch {
    reconnectTimer = setTimeout(checkAndReconnect, 6000);
    return;
  }
  connect();
}

function setConnected(value) {
  if (store.connected === value) return;
  store.connected = value;
  if (value) store.everConnected = true;
  const banner = $('#conn');
  if (banner) banner.hidden = value || !store.everConnected;
}

function setState(state) {
  store.state = state;
  if (!state.accounts.some((a) => a.id === store.accountId)) store.accountId = state.accounts[0]?.id ?? null;
  renderAll();
}

const accounts = () => store.state?.accounts ?? [];
const accountById = (id) => accounts().find((a) => a.id === id);
const currentAccount = () => accountById(store.accountId);
const accountSlot = (account) => (accounts().indexOf(account) % 4) + 1;
const queuePosition = (account, campaignId) => {
  const index = account?.queue.findIndex((q) => q.campaignId === campaignId) ?? -1;
  return index >= 0 ? index + 1 : null;
};

function statusKind(account) {
  if (account.status === 'watching') return 'watching';
  if (account.status === 'seeking') return 'seeking';
  if (account.lastError && account.idleReason === 'error') return 'error';
  return 'waiting';
}

// ---------------------------------------------------------------- shell

const app = $('#app');

function renderShell() {
  app.dataset.view = 'main';
  setHtml(app, html`
    <header class="topbar" id="topbar">
      <div class="brand"><span class="brand-mark">${icon('logo')}</span><span class="brand-name">Drops</span></div>
      <nav class="accounts" id="accounts" role="tablist" aria-label="Accounts"></nav>
      <div class="top-actions">
        <button class="btn ghost square" data-action="app-menu" aria-label="Menu">${icon('more')}</button>
      </div>
    </header>
    <div class="conn" id="conn" hidden>${icon('alert', 'sm')}<span>Lost the connection to the bot. Reconnecting…</span></div>
    <div class="layout">
      <div class="col-left">
        <section class="view" id="view-now" data-tab="now" aria-label="Now"></section>
        <section class="view" id="view-queue" data-tab="queue" aria-label="Queue"></section>
      </div>
      <div class="col-right">
        <div class="panel-switch" role="tablist" aria-label="Panel">
          <button role="tab" data-right="campaigns">${icon('grid', 'sm')} Campaigns</button>
          <button role="tab" data-right="activity">${icon('activity', 'sm')} Activity</button>
        </div>
        <section class="view" id="view-campaigns" data-tab="campaigns" aria-label="Campaigns"></section>
        <section class="view" id="view-activity" data-tab="activity" aria-label="Activity"></section>
      </div>
    </div>
    <nav class="tabbar" id="tabbar" role="tablist" aria-label="Views">
      ${TABS.map(([id, label, glyph]) => html`<button class="tab" role="tab" data-tab-button="${id}">${icon(glyph)}<span>${label}</span><span class="count" hidden></span></button>`)}
    </nav>`);
  renderCampaignsFrame();
  renderActivityFrame();
}

function renderAll() {
  if (app.dataset.view !== 'main') return;
  renderAccounts();
  renderTabs();
  renderNow();
  if (!store.dragging) renderQueue();
  renderCampaignList();
  if (activityVisible()) pollLogs();
  if (campaignsVisible()) ensureCampaigns();
  if (store.sheet) renderSheet();
}

let lastAccountsKey = '';
function renderAccounts() {
  const key = JSON.stringify([store.accountId, accounts().map((a) => [a.id, a.login, statusKind(a)])]);
  if (key === lastAccountsKey) return;
  lastAccountsKey = key;
  setHtml($('#accounts'), accounts().map((account) => html`
    <button class="acct" role="tab" aria-selected="${account.id === store.accountId}" data-account="${account.id}">
      <span class="avatar" data-slot="${accountSlot(account)}">${account.login[0]}<span class="pip ${statusKind(account)}"></span></span>
      <span>${account.login}</span>
    </button>`));
}

function renderTabs() {
  for (const button of $$('[data-tab-button]')) {
    button.setAttribute('aria-selected', String(button.dataset.tabButton === store.tab));
  }
  for (const button of $$('[data-right]')) {
    button.setAttribute('aria-selected', String(button.dataset.right === store.right));
  }
  for (const view of $$('.view')) {
    view.classList.toggle('active', view.dataset.tab === store.tab);
    view.classList.toggle('right-active', view.dataset.tab === store.right);
  }
  const count = $('[data-tab-button="queue"] .count');
  const queued = currentAccount()?.queue.length ?? 0;
  if (count) {
    count.hidden = queued === 0;
    count.textContent = queued;
  }
}

const campaignsVisible = () => (desktop.matches ? store.right === 'campaigns' : store.tab === 'campaigns');
const activityVisible = () => (desktop.matches ? store.right === 'activity' : store.tab === 'activity');

function selectTab(tab) {
  store.tab = tab;
  localStorage.setItem(KEY.tab, tab);
  if (tab === 'campaigns' || tab === 'activity') {
    store.right = tab;
    localStorage.setItem(KEY.right, tab);
  }
  renderTabs();
  if (!desktop.matches) window.scrollTo({ top: 0 });
  if (campaignsVisible()) ensureCampaigns();
  if (activityVisible()) {
    renderActivity(true);
    pollLogs();
  }
}

function selectRight(panel) {
  store.right = panel;
  localStorage.setItem(KEY.right, panel);
  renderTabs();
  if (campaignsVisible()) ensureCampaigns();
  if (activityVisible()) {
    renderActivity(true);
    pollLogs();
  }
}

function selectAccount(id) {
  if (id === store.accountId) return;
  store.accountId = id;
  localStorage.setItem(KEY.account, id);
  lastNowKey = '';
  lastQueueKey = '';
  lastListKey = '';
  renderAll();
  renderActivity(true);
}

// ---------------------------------------------------------------- now

let lastNowKey = '';
function renderNow() {
  const account = currentAccount();
  const key = JSON.stringify([account, accounts().map((a) => [a.id, a.status, a.watching?.minutesWatched, a.nextCheckAt])]);
  if (key === lastNowKey) return;
  lastNowKey = key;

  const view = $('#view-now');
  if (!account) {
    setHtml(view, html`<div class="card empty">${icon('info')}<h3>No accounts yet</h3><p>The bot hasn't started any Twitch account. Its log will say why.</p></div>`);
    return;
  }

  const others = accounts().filter((a) => a.id !== account.id);
  const upNext = account.queue.filter((q) => q.campaignId !== account.watching?.campaignId).slice(0, 3);

  setHtml(view, html`
    <div class="section-head"><h2>${account.login}</h2>${account.watching ? html`<span class="hint">${live('ends', account.watching.campaignEndsAt, endsText(account.watching.campaignEndsAt))}</span>` : ''}</div>
    ${hero(account)}
    <div class="section-head">
      <h2>Up next</h2>
      <button class="link-btn" data-goto="queue">${account.queue.length ? 'Edit queue' : 'Queue a campaign'}</button>
    </div>
    ${upNext.length
      ? html`<ol class="card list">${upNext.map((item, i) => queueRow(account, item, i, false))}</ol>`
      : html`<div class="callout info">${icon('info', 'sm')}<div class="grow">Nothing queued. The bot follows its normal order: ${account.onlyFavourites ? 'favourite games only' : 'favourite games first'}, soonest-ending first. <button class="link-btn" data-goto="campaigns">Pick a campaign</button></div></div>`}
    ${others.length ? html`
      <div class="section-head"><h2>${others.length === 1 ? 'Other account' : 'Other accounts'}</h2></div>
      <div class="mini-list">${others.map(miniAccount)}</div>` : ''}`);
}

function hero(account) {
  const error = account.lastError
    ? html`<div class="callout bad error-line">${icon('alert', 'sm')}<div class="grow"><strong>Last error</strong> ${live('ago', account.lastError.at, agoText(account.lastError.at))}: ${account.lastError.message}</div></div>`
    : '';

  if (account.status === 'watching' && account.watching) return heroWatching(account, error);

  if (account.status === 'seeking') {
    const checking = account.checking;
    return html`<article class="card hero">
      <span class="status busy">${icon('loader', 'spin')}Looking</span>
      <h3 class="hero-title">Looking for a campaign</h3>
      <p class="hero-sub">${checking ? html`Checking <strong>${checking.gameName || 'a game'}</strong><span class="sep">·</span>${checking.campaignName}` : 'Fetching campaigns and claiming finished drops.'}</p>
      ${error}
    </article>`;
  }

  if (account.status === 'starting') {
    return html`<article class="card hero">
      <span class="status busy">${icon('loader', 'spin')}Starting</span>
      <h3 class="hero-title">Starting up</h3>
      <p class="hero-sub">The bot is signing in and fetching campaigns.</p>
    </article>`;
  }

  const head = account.queue.find((q) => q.state === 'queued');
  return html`<article class="card hero">
    <span class="status ${account.idleReason === 'error' ? 'bad' : 'warn'}">${icon(account.idleReason === 'error' ? 'alert' : 'clock')}Waiting</span>
    <h3 class="hero-title">${IDLE_TEXT[account.idleReason] || 'Waiting'}</h3>
    ${account.nextCheckAt ? html`<p class="hero-sub">Next check in</p><div class="waiting-figure num">${live('countdown', account.nextCheckAt, countdownText(account.nextCheckAt))}</div>` : ''}
    ${head ? html`<p class="hero-sub queue-hint">First in the queue: <strong>${head.campaignName}</strong> (${head.gameName || 'unknown game'})</p>` : ''}
    ${error}
    <div class="hero-actions">
      <button class="btn primary" data-action="switch">${icon('refresh', 'sm')}Check now</button>
    </div>
  </article>`;
}

function heroWatching(account, error) {
  const w = account.watching;
  const current = w.minutesWatched ?? 0;
  const required = w.minutesRequired ?? 0;
  const left = Math.max(0, required - current);
  const eta = new Date(now() + left * 60000).toISOString();
  const channelUrl = w.channelLogin ? `https://www.twitch.tv/${encodeURIComponent(w.channelLogin)}` : null;
  const head = account.queue.find((q) => q.state === 'queued' && q.campaignId !== w.campaignId);

  return html`<article class="card hero">
    <div class="hero-top">
      ${art(w.boxArtUrl, w.gameName, 'lg')}
      <div class="hero-main">
        <span class="status good"><span class="dot live"></span>Watching</span>
        <h3 class="hero-title">${w.campaignName}</h3>
        <p class="hero-sub">${w.gameName || 'Unknown game'}${w.channelName ? html`<span class="sep">·</span>${channelUrl ? html`<a href="${channelUrl}" target="_blank" rel="noopener noreferrer">${w.channelName}</a>` : w.channelName}` : ''}</p>
      </div>
    </div>
    <div class="earning">
      ${rewardImage(w.dropImageUrl)}
      <div><div class="label">Now earning</div><div class="name">${w.dropName || 'Next drop'}</div></div>
    </div>
    <div class="figure"><span class="value num">${current}</span><span class="unit">/ ${required} min</span><span class="pct num">${pct(current, required)}%</span></div>
    ${meter(current, required, 'lg')}
    <div class="facts">
      <span><strong>${minutesText(left)}</strong> left</span>
      <span>Done around <strong>${clockFormat.format(new Date(eta))}</strong></span>
    </div>
    ${tierList(w.tiers)}
    ${error}
    <div class="hero-actions">
      ${head ? html`<button class="btn grow" data-action="switch">${icon('skip', 'sm')}<span class="clip">Switch to ${head.campaignName}</span></button>` : ''}
      <button class="btn ${head ? 'square' : ''}" data-open="${w.campaignId}" aria-label="Campaign details">${icon('info', 'sm')}${head ? '' : 'Campaign details'}</button>
    </div>
  </article>`;
}

function tierList(tiers, { all = false, sheet = false } = {}) {
  if (!tiers || tiers.length <= 1 && !sheet) return '';
  const claimed = tiers.filter((t) => t.claimed);
  const rest = tiers.filter((t) => !t.claimed);
  const collapse = !all && claimed.length > 2;
  const shownRest = all ? rest : rest.slice(0, 4);
  const shown = collapse ? shownRest : (all ? tiers : [...claimed, ...shownRest]);
  const hidden = rest.length - shownRest.length;

  return html`<ol class="tiers ${sheet ? 'sheet-tiers' : ''}">
    ${collapse ? html`<li class="tier claimed"><span class="tier-mark">${icon('check-circle', 'sm')}</span><span class="tier-name">${claimed.length} drops claimed</span><span class="tier-num"></span></li>` : ''}
    ${shown.map((t) => html`<li class="tier ${t.claimed ? 'claimed' : t.active ? 'active' : ''}">
      <span class="tier-mark">${t.claimed ? icon('check-circle', 'sm') : t.active ? raw('<span class="now"></span>') : raw('<span class="ring"></span>')}</span>
      <span class="tier-name">${t.name}</span>
      <span class="tier-num num">${t.claimed ? 'claimed' : t.current == null ? `${minutesText(t.required)}` : `${Math.min(t.current, t.required)} / ${t.required} min`}</span>
      ${t.current == null && !t.claimed ? '' : meter(t.claimed ? t.required : t.current, t.required, 'sm')}
    </li>`)}
    ${hidden > 0 ? html`<li class="tier-more">+${hidden} more ${hidden === 1 ? 'drop' : 'drops'} later in this campaign</li>` : ''}
  </ol>`;
}

function miniAccount(account) {
  const w = account.watching;
  let line;
  if (account.status === 'watching' && w) line = html`Watching <strong>${w.campaignName}</strong> · ${w.minutesWatched ?? 0}/${w.minutesRequired ?? 0} min`;
  else if (account.status === 'seeking') line = 'Looking for a campaign';
  else if (account.status === 'starting') line = 'Starting up';
  else line = html`${IDLE_TEXT[account.idleReason] || 'Waiting'}${account.nextCheckAt ? html` · next check ${live('countdown', account.nextCheckAt, countdownText(account.nextCheckAt))}` : ''}`;

  return html`<button class="card mini" data-account="${account.id}">
    <span class="avatar lg" data-slot="${accountSlot(account)}">${account.login[0]}<span class="pip ${statusKind(account)}"></span></span>
    <span>
      <span class="mini-name">${account.login}</span>
      <span class="mini-sub">${line}</span>
      ${account.status === 'watching' && w ? meter(w.minutesWatched ?? 0, w.minutesRequired ?? 0, 'sm') : ''}
    </span>
    <span class="chev">${icon('chevron')}</span>
  </button>`;
}

// ---------------------------------------------------------------- queue

function queueState(account, item, index) {
  switch (item.state) {
    case 'watching': return { cls: 'good', glyph: 'radio', text: 'Watching now' };
    case 'done': return { cls: 'good', glyph: 'check-circle', text: 'Done, all drops earned' };
    case 'waitingClaim': return item.needsLink
      ? { cls: 'bad', glyph: 'link', text: 'Watched · link your account to claim' }
      : { cls: 'busy', glyph: 'clock', text: 'Watched · claim pending' };
    case 'ended': return { cls: 'bad', glyph: 'clock', text: 'Campaign ended' };
    case 'unavailable': return { cls: 'bad', glyph: 'alert', text: 'No longer offered to this account' };
    default:
      if (item.note && item.note.kind !== 'Completed') {
        return { cls: 'warn', glyph: 'alert', text: html`${NOTE_TEXT[item.note.kind] || 'Skipped'} · checked ${live('ago', item.note.at, agoText(item.note.at))}` };
      }
      return index === 0 || account.queue.slice(0, index).every((q) => q.state !== 'queued')
        ? { cls: 'busy', glyph: 'play', text: 'Next up' }
        : { cls: '', glyph: 'clock', text: 'Waiting for its turn' };
  }
}

function queueRow(account, item, index, editable) {
  const state = queueState(account, item, index);
  const p = item.progress;
  return html`<li class="row ${editable ? 'q-item' : 'clickable'}" data-id="${item.campaignId}">
    ${editable ? html`<button class="grip" data-drag aria-label="Reorder ${item.campaignName}. Drag, or use the arrow keys.">${icon('grip', 'sm')}</button>` : html`<span class="q-index">${index + 1}</span>`}
    <button class="row-open" data-open="${item.campaignId}" aria-label="${item.campaignName}, details">
      ${art(item.boxArtUrl, item.gameName)}
      <div class="row-main">
        <div class="row-title">${item.campaignName || 'Unknown campaign'}</div>
        <div class="row-sub">${item.gameName || 'Unknown game'} · ${item.endsAt ? live('ends', item.endsAt, endsText(item.endsAt)) : 'no end date'}</div>
        <div class="row-state ${state.cls}">${icon(state.glyph)}<span>${state.text}</span></div>
        ${p && p.total && item.state !== 'done' ? html`${meter(p.watched, p.required, 'sm')}<div class="row-progress"><span>${p.claimed} of ${p.total} ${dropsWord(p.total)}</span><span class="num">${p.watched}/${p.required} min</span></div>` : ''}
      </div>
    </button>
    ${editable ? html`<div class="row-side"><button class="btn ghost square sm" data-menu="queue" data-id="${item.campaignId}" aria-label="Actions for ${item.campaignName}">${icon('more')}</button></div>` : ''}
  </li>`;
}

let lastQueueKey = '';
function renderQueue() {
  const account = currentAccount();
  const view = $('#view-queue');
  const key = JSON.stringify(account ? [account.id, account.queue, account.onlyFavourites] : null);
  if (key === lastQueueKey) return;
  lastQueueKey = key;

  if (!account) {
    setHtml(view, '');
    return;
  }

  const finished = account.queue.filter((q) => q.state === 'done' || q.state === 'ended' || q.state === 'unavailable');
  setHtml(view, html`
    <div class="section-head"><h2>Priority queue</h2><span class="hint">${account.queue.length ? `${account.queue.length} queued` : ''}</span></div>
    <p class="help">Watched first, top to bottom. When nothing here can be watched, the bot falls back to its normal order${account.onlyFavourites ? ' (favourites only)' : ''}.</p>
    ${account.queue.length
      ? html`<ol class="card list" id="queue-list">${account.queue.map((item, i) => queueRow(account, item, i, true))}</ol>`
      : html`<div class="card empty">${icon('queue')}<h3>Nothing queued</h3><p>Queue a campaign to watch it before anything else. Queued campaigns are farmed even if their game isn't a favourite.</p><button class="btn primary" data-goto="campaigns">${icon('plus', 'sm')}Browse campaigns</button></div>`}
    ${account.queue.length ? html`<div class="queue-foot">
      <button class="btn" data-goto="campaigns">${icon('plus', 'sm')}Add campaigns</button>
      ${finished.length ? html`<button class="btn ghost" data-action="clear-finished">${icon('x', 'sm')}Clear ${finished.length} finished</button>` : ''}
    </div>` : ''}`);
}

// Drag to reorder (mouse and touch); arrow keys on the handle do the same
function startDrag(event, handle) {
  const item = handle.closest('.q-item');
  const list = item.parentElement;
  const items = [...list.children];
  const from = items.indexOf(item);
  const rects = items.map((el) => el.getBoundingClientRect());
  const startY = event.clientY;
  const height = rects[from].height;
  let to = from;

  store.dragging = true;
  item.classList.add('dragging');
  items.forEach((el) => el !== item && el.classList.add('shifting'));
  handle.setPointerCapture(event.pointerId);

  const move = (e) => {
    const dy = e.clientY - startY;
    item.style.transform = `translateY(${dy}px)`;
    const centre = rects[from].top + height / 2 + dy;
    to = rects.filter((r, i) => i !== from && centre > r.top + r.height / 2).length;
    items.forEach((el, i) => {
      if (el === item) return;
      let shift = 0;
      if (from < to && i > from && i <= to) shift = -height;
      if (from > to && i >= to && i < from) shift = height;
      el.style.transform = shift ? `translateY(${shift}px)` : '';
    });
  };

  const finish = async () => {
    handle.removeEventListener('pointermove', move);
    handle.removeEventListener('pointerup', finish);
    handle.removeEventListener('pointercancel', finish);
    items.forEach((el) => el.classList.remove('dragging', 'shifting'));
    items.forEach((el) => { el.style.transform = ''; });
    store.dragging = false;

    if (to === from) return;

    // Show the new order at once; the pushed state confirms it, or a failure puts the old one back
    const ids = items.map((el) => el.dataset.id);
    const [moved] = ids.splice(from, 1);
    ids.splice(to, 0, moved);
    for (const id of ids) list.append(items.find((el) => el.dataset.id === id));
    if (!(await reorder(currentAccount(), ids))) {
      lastQueueKey = '';
      renderQueue();
    }
  };

  handle.addEventListener('pointermove', move);
  handle.addEventListener('pointerup', finish);
  handle.addEventListener('pointercancel', finish);
}

async function reorder(account, ids) {
  try {
    await api(`/api/accounts/${enc(account.id)}/queue`, { method: 'PUT', body: { campaignIds: ids } });
    return true;
  } catch (e) {
    toast(e.message, { error: true });
    return false;
  }
}

async function moveInQueue(account, campaignId, delta, absolute = null) {
  const ids = account.queue.map((q) => q.campaignId);
  const from = ids.indexOf(campaignId);
  if (from < 0) return;
  const to = absolute ?? Math.max(0, Math.min(ids.length - 1, from + delta));
  if (to === from) return;
  ids.splice(from, 1);
  ids.splice(to, 0, campaignId);
  await reorder(account, ids);
}

async function addToQueue(accountIds, campaignId, { position = 'bottom', switchNow = false, name = 'Campaign' } = {}) {
  const done = [];
  for (const id of accountIds) {
    const account = accountById(id);
    try {
      await api(`/api/accounts/${enc(id)}/queue`, { method: 'POST', body: { campaignId, position, switchNow } });
      done.push(account?.login ?? id);
    } catch (e) {
      toast(`${account?.login ?? id}: ${e.message}`, { error: true });
    }
  }
  if (done.length === 0) return false;

  const who = done.join(' and ');
  if (switchNow) toast(`Switching ${who} to ${name}…`);
  else if (position === 'top') toast(`${name} is next up for ${who}`);
  else {
    toast(`Added ${name} to ${who}'s queue`, {
      action: done.length === 1 ? { label: 'Undo', run: () => removeFromQueue(accountIds[0], campaignId, { quiet: true }) } : null,
    });
  }
  return true;
}

async function removeFromQueue(accountId, campaignId, { quiet = false } = {}) {
  const account = accountById(accountId);
  const index = account?.queue.findIndex((q) => q.campaignId === campaignId) ?? -1;
  const item = account?.queue[index];
  try {
    await api(`/api/accounts/${enc(accountId)}/queue/${enc(campaignId)}`, { method: 'DELETE' });
  } catch (e) {
    toast(e.message, { error: true });
    return;
  }
  if (!quiet && item) {
    toast(`Removed ${item.campaignName}`, {
      action: { label: 'Undo', run: () => addToQueue([accountId], campaignId, { position: String(index), name: item.campaignName }) },
    });
  }
}

async function switchNow(account) {
  try {
    await api(`/api/accounts/${enc(account.id)}/switch`, { method: 'POST' });
    toast(account.status === 'watching' ? `Re-picking for ${account.login}…` : `Checking again for ${account.login}…`);
  } catch (e) {
    toast(e.message, { error: true });
  }
}

// ---------------------------------------------------------------- campaigns

function renderCampaignsFrame() {
  setHtml($('#view-campaigns'), html`
    <div class="toolbar">
      <label class="search">${icon('search', 'sm')}<span class="visually-hidden">Search campaigns</span><input id="campaign-search" type="search" placeholder="Search campaigns or games" autocomplete="off" enterkeyhint="search"></label>
      <button class="btn square" data-action="refresh-campaigns" aria-label="Refresh the campaign list">${icon('refresh', 'sm')}</button>
    </div>
    <div class="chips" id="campaign-filters" role="group" aria-label="Filter"></div>
    <div class="list-meta">
      <span id="campaign-meta"></span>
      <label><span class="visually-hidden">Sort</span><select id="campaign-sort">
        <option value="ending">Ending soonest</option>
        <option value="progress">Most progress</option>
        <option value="name">Name</option>
      </select></label>
    </div>
    <div class="campaigns-scroll" id="campaign-list"></div>`);

  const search = $('#campaign-search');
  search.value = store.search;
  search.addEventListener('input', () => {
    store.search = search.value;
    renderCampaignList(true);
  });

  const sort = $('#campaign-sort');
  sort.value = store.sort;
  sort.addEventListener('change', () => {
    store.sort = sort.value;
    localStorage.setItem(KEY.sort, sort.value);
    renderCampaignList(true);
  });
}

async function ensureCampaigns(force = false) {
  const account = currentAccount();
  if (!account) return;
  const cached = store.campaigns.get(account.id);
  const stale = !cached || !cached.campaigns || cached.version !== account.campaignsVersion;
  if (cached?.loading || (!force && !stale && !cached?.error)) return;

  store.campaigns.set(account.id, { ...cached, loading: true, error: null });
  renderCampaignList(true);

  try {
    const data = force
      ? await api(`/api/accounts/${enc(account.id)}/campaigns/refresh`, { method: 'POST' })
      : await api(`/api/accounts/${enc(account.id)}/campaigns`);
    store.campaigns.set(account.id, { ...data, loading: false, error: null });
    if (force) toast('Campaign list refreshed');
  } catch (e) {
    store.campaigns.set(account.id, { ...cached, loading: false, error: e.message });
    if (force) toast(e.message, { error: true });
  }
  renderCampaignList(true);
}

function filterCampaigns(account, list) {
  const query = store.search.trim().toLowerCase();
  const soon = now() + 24 * 3600e3;
  const test = {
    all: () => true,
    favourites: (c) => c.isFavourite,
    queued: (c) => queuePosition(account, c.id),
    progress: (c) => c.status === 'inProgress' || c.isWatching,
    unclaimed: (c) => c.status === 'waitingClaim',
    done: (c) => c.status === 'done',
    linked: (c) => c.link !== 'unlinked',
    ending: (c) => c.endsAt && new Date(c.endsAt) < soon,
  };
  const counts = Object.fromEntries(FILTERS.map(([id]) => [id, list.filter(test[id]).length]));

  let out = list.filter(test[store.filter] || test.all);
  if (query) out = out.filter((c) => c.name.toLowerCase().includes(query) || (c.gameName || '').toLowerCase().includes(query));

  const end = (c) => (c.endsAt ? new Date(c.endsAt).getTime() : Infinity);
  const progress = (c) => (c.progress ? (c.progress.claimed + c.progress.watched / Math.max(1, c.progress.required)) / Math.max(1, c.progress.total) : -1);
  const sorters = {
    ending: (a, b) => end(a) - end(b),
    name: (a, b) => a.name.localeCompare(b.name),
    progress: (a, b) => progress(b) - progress(a) || end(a) - end(b),
  };
  out.sort(sorters[store.sort] || sorters.ending);
  return { list: out, counts };
}

let lastListKey = '';
function renderCampaignList(force = false) {
  const account = currentAccount();
  const host = $('#campaign-list');
  if (!host || !account) return;

  const cached = store.campaigns.get(account.id);
  const key = JSON.stringify([account.id, account.queue.map((q) => q.campaignId), account.watching?.campaignId, account.onlyFavourites, cached?.version, cached?.loading, cached?.error, store.filter, store.sort, store.search]);
  if (!force && key === lastListKey) return;
  lastListKey = key;

  const all = cached?.campaigns ?? [];
  const { list, counts } = filterCampaigns(account, all);

  setHtml($('#campaign-filters'), FILTERS.map(([id, label]) => html`<button class="chip" data-filter="${id}" aria-pressed="${store.filter === id}">${label}<span class="n">${all.length ? counts[id] : ''}</span></button>`));
  setHtml($('#campaign-meta'), cached?.updatedAt
    ? html`Updated ${live('ago', cached.updatedAt, agoText(cached.updatedAt))}`
    : cached?.loading ? 'Loading…' : '');

  if (!cached?.campaigns) {
    setHtml(host, cached?.error
      ? html`<div class="card empty">${icon('alert')}<h3>Couldn't load campaigns</h3><p>${cached.error}</p><button class="btn" data-action="retry-campaigns">Try again</button></div>`
      : html`<ul class="card list">${[0, 1, 2, 3, 4].map(() => html`<li class="row"><span class="art skeleton"></span><div class="row-main"><div class="skeleton"></div></div></li>`)}</ul>`);
    sizeSkeletons(host);
    return;
  }

  if (all.length === 0) {
    setHtml(host, html`<div class="card empty">${icon('grid')}<h3>No campaigns yet</h3><p>The bot hasn't fetched a campaign list for ${account.login} yet, or Twitch offers none. Refresh to ask Twitch now.</p><button class="btn" data-action="refresh-campaigns">${icon('refresh', 'sm')}Refresh</button></div>`);
    return;
  }

  if (list.length === 0) {
    setHtml(host, html`<div class="card empty">${icon('search')}<h3>Nothing matches</h3><p>Try another filter or search.</p></div>`);
    return;
  }

  setHtml(host, html`<ul class="card list">${list.map((c) => campaignRow(account, c))}</ul>`);
}

function sizeSkeletons(host) {
  for (const bar of $$('.row-main .skeleton', host)) {
    bar.style.height = '12px';
    bar.style.width = `${50 + Math.round(Math.random() * 40)}%`;
  }
}

function linkBadge(c) {
  if (c.kind === 'reward') return html`<span class="badge">${icon('gift')}Reward</span>`;
  if (c.link === 'linked') return html`<span class="badge linked">${icon('check')}Linked</span>`;
  if (c.link === 'unlinked') return html`<span class="badge unlinked">${icon('alert')}Not linked</span>`;
  return html`<span class="badge">No link needed</span>`;
}

function campaignBadges(account, c) {
  const watching = account.watching?.campaignId === c.id;
  return html`
    ${watching ? html`<span class="badge watching"><span class="dot live"></span>Watching</span>` : ''}
    ${c.isFavourite ? html`<span class="badge fav">${icon('star')}Favourite</span>` : account.onlyFavourites ? html`<span class="badge muted">Not a favourite</span>` : ''}
    ${linkBadge(c)}
    ${c.isAvoided ? html`<span class="badge muted">Avoided</span>` : ''}`;
}

function dropsWord(n) {
  return n === 1 ? 'drop' : 'drops';
}

// Where a campaign stands: done, watched but unclaimed, in progress, not started, or not known yet
function campaignStatus(c) {
  const p = c.progress;
  switch (p || c.status === 'done' ? c.status : 'unknown') {
    case 'done':
      return html`<div class="row-state good">${icon('check-circle')}<span>All drops earned</span></div>`;
    case 'waitingClaim':
      return html`<div class="row-state ${c.needsLink ? 'bad' : 'busy'}">${icon(c.needsLink ? 'link' : 'clock')}<span>${p.waiting} ${dropsWord(p.waiting)} watched · ${c.needsLink ? 'link your account to claim' : 'claim pending'}</span></div>`;
    case 'inProgress':
      return html`${meter(p.watched, p.required, 'sm')}<div class="row-progress"><span>${p.claimed} of ${p.total} ${dropsWord(p.total)}</span><span class="num">${p.watched}/${p.required} min</span></div>`;
    case 'notStarted':
      return html`<div class="row-progress"><span>Not started · first drop after ${minutesText(p.required)}</span></div>`;
    default:
      return c.totalMinutes ? html`<div class="row-progress"><span>All drops in ${minutesText(c.totalMinutes)}</span></div>` : '';
  }
}

function campaignRow(account, c) {
  const position = queuePosition(account, c.id);
  const watching = account.watching?.campaignId === c.id;
  const done = c.status === 'done';
  return html`<li class="row clickable">
    <button class="row-open" data-open="${c.id}" aria-label="${c.name}, details">
      ${art(c.boxArtUrl, c.gameName)}
      <div class="row-main">
        <div class="row-title">${c.name}</div>
        <div class="row-sub">${c.gameName || 'Unknown game'} · ${c.endsAt ? live('ends', c.endsAt, endsText(c.endsAt)) : 'no end date'}</div>
        <div class="badges">${campaignBadges(account, c)}</div>
        ${campaignStatus(c)}
      </div>
    </button>
    <div class="row-side">
      ${position ? html`<span class="qpos" title="Position in the queue">#${position}</span>`
        : c.kind === 'drop' && !watching && !done ? html`<button class="btn square sm add-btn" data-quick-add="${c.id}" aria-label="Add ${c.name} to the queue">${icon('plus', 'sm')}</button>` : ''}
    </div>
  </li>`;
}

// ---------------------------------------------------------------- campaign sheet

const sheet = $('#sheet');

async function openCampaign(campaignId, accountId = store.accountId, { recheck = false } = {}) {
  const keep = recheck && store.sheet?.campaignId === campaignId ? store.sheet : null;
  store.sheet = { accountId, campaignId, detail: keep?.detail ?? null, loading: true, checking: recheck, error: null, also: keep?.also ?? false };
  renderSheet();
  if (!sheet.open) {
    sheet.showModal();
    history.pushState({ sheet: true }, '');
  }

  try {
    const detail = await api(`/api/accounts/${enc(accountId)}/campaigns/${enc(campaignId)}${recheck ? '?recheck=true' : ''}`);
    if (store.sheet?.campaignId === campaignId) store.sheet.detail = detail;
  } catch (e) {
    if (store.sheet?.campaignId === campaignId) store.sheet.error = e.message;
  }
  if (store.sheet?.campaignId === campaignId) {
    store.sheet.loading = false;
    store.sheet.checking = false;
    renderSheet();
  }
}

function closeSheet(fromHistory = false) {
  if (!store.sheet) return;
  store.sheet = null;
  if (sheet.open) sheet.close();
  if (!fromHistory && history.state?.sheet) history.back();
}

function cachedCampaign(accountId, campaignId) {
  return store.campaigns.get(accountId)?.campaigns?.find((c) => c.id === campaignId);
}

function renderSheet() {
  const s = store.sheet;
  if (!s) return;
  const account = accountById(s.accountId);
  const queued = account?.queue.find((q) => q.campaignId === s.campaignId);
  const c = s.detail?.campaign ?? cachedCampaign(s.accountId, s.campaignId) ?? (queued && {
    id: queued.campaignId, name: queued.campaignName, gameName: queued.gameName, boxArtUrl: queued.boxArtUrl, endsAt: queued.endsAt, kind: 'drop', link: 'none',
  });
  const body = $('.sheet-body', sheet);
  const scroll = body?.scrollTop ?? 0;

  if (!c || !account) {
    setHtml(sheet, html`<div class="sheet-head"><h3 id="sheet-title">${s.error || 'Loading…'}</h3><button class="btn ghost square close" data-action="close-sheet" aria-label="Close">${icon('x')}</button></div>`);
    return;
  }

  const position = queuePosition(account, c.id);
  const watching = account.watching?.campaignId === c.id;
  const canQueue = s.detail ? s.detail.canQueue : c.kind !== 'reward';
  const finished = c.status === 'done' && !watching;
  const others = accounts().filter((a) => a.id !== account.id);
  const tiers = s.detail?.tiers;
  const note = (queued?.note ?? c.note);

  let status;
  if (watching) status = html`<div class="callout good">${icon('radio', 'sm')}<div class="grow"><strong>Watching now</strong> on ${account.watching.channelName || 'a live channel'}.</div></div>`;
  else if (position) status = html`<div class="callout info">${icon('queue', 'sm')}<div class="grow"><strong>#${position} in ${account.login}'s queue.</strong> ${position === 1 ? 'It goes next, as soon as it can be watched.' : `${position - 1} ahead of it.`}</div></div>`;
  else status = '';

  const noteLine = note && !watching && note.kind !== 'Completed' && c.status !== 'done'
    ? html`<div class="callout warn">${icon('alert', 'sm')}<div class="grow"><strong>${NOTE_TEXT[note.kind] || 'Skipped'}</strong> · last checked ${live('ago', note.at, agoText(note.at))}</div></div>`
    : '';
  const p = c.progress;
  const checkedOn = c.statusCheckedAt && c.statusSource === 'channel'
    ? html` · checked ${live('ago', c.statusCheckedAt, agoText(c.statusCheckedAt))}${s.detail?.statusChannel ? html` on ${s.detail.statusChannel}` : ''}`
    : '';
  const recheck = html`<button class="link-btn" data-action="sheet-recheck" ${raw(s.checking ? 'disabled' : '')}>${s.checking ? 'Checking…' : 'Check again'}</button>`;
  let progressLine = '';
  if (!watching && c.kind === 'drop') {
    if (c.status === 'done') {
      progressLine = html`<div class="callout good">${icon('check-circle', 'sm')}<div class="grow"><strong>All drops earned</strong>${checkedOn}</div></div>`;
    } else if (c.status === 'waitingClaim' && p) {
      progressLine = c.needsLink
        ? html`<div class="callout bad">${icon('link', 'sm')}<div class="grow"><strong>${p.waiting} ${dropsWord(p.waiting)} watched but not claimed.</strong> Twitch hands ${p.waiting === 1 ? 'it' : 'them'} over once ${account.login} links the game account.${checkedOn}</div></div>`
        : html`<div class="callout info">${icon('clock', 'sm')}<div class="grow"><strong>${p.waiting} ${dropsWord(p.waiting)} watched.</strong> The bot claims ${p.waiting === 1 ? 'it' : 'them'} at its next check.</div></div>`;
    } else if (c.status === 'unknown' && !s.loading) {
      // The bot's own note usually gives the same reason, so the check's reason only shows without one
      const problem = !noteLine && s.detail?.statusProblem ? `${s.detail.statusProblem}. ` : '';
      progressLine = html`<div class="callout">${icon('info', 'sm')}<div class="grow"><strong>Progress not known yet.</strong> ${problem}${recheck}</div></div>`;
    } else if (c.statusSource === 'channel') {
      progressLine = html`<p class="muted small">Progress${checkedOn}. ${recheck}</p>`;
    }
  }

  const linkUrl = safeUrl(c.linkUrl);
  const detailsUrl = safeUrl(s.detail?.detailsUrl);

  setHtml(sheet, html`
    <header class="sheet-head">
      ${art(c.boxArtUrl, c.gameName, 'md')}
      <div>
        <div class="eyebrow">${c.gameName || 'Unknown game'}</div>
        <h3 id="sheet-title">${c.name}</h3>
        <div class="sub">${c.endsAt ? html`Ends ${whenText(c.endsAt)} · ${live('until', c.endsAt, untilText(c.endsAt))}` : 'No end date'}</div>
        <div class="badges">${campaignBadges(account, c)}</div>
      </div>
      <button class="btn ghost square close" data-action="close-sheet" aria-label="Close">${icon('x')}</button>
    </header>
    <div class="sheet-body">
      ${status}
      ${progressLine}
      ${noteLine}
      ${c.link === 'unlinked' && c.status !== 'waitingClaim' ? html`<div class="callout warn">${icon('link', 'sm')}<div class="grow"><strong>${account.login} isn't linked to this game.</strong> Watching still counts, and Twitch hands the drops over once the account is linked.${linkUrl ? html` <a href="${linkUrl}" target="_blank" rel="noopener noreferrer">Link the account</a>` : ''}</div></div>` : ''}
      ${c.link === 'unlinked' && c.status === 'waitingClaim' && linkUrl ? html`<div class="sheet-links"><a class="btn sm" href="${linkUrl}" target="_blank" rel="noopener noreferrer">${icon('link', 'sm')}Link the account</a></div>` : ''}
      ${!c.isFavourite && account.onlyFavourites && c.kind !== 'reward' ? html`<div class="callout info">${icon('star', 'sm')}<div class="grow">Not in the favourites list, so the bot only farms it while it's queued.</div></div>` : ''}
      <h4>Drops${c.totalMinutes ? html` · all in ${minutesText(c.totalMinutes)}` : ''}</h4>
      ${s.loading && !tiers ? html`<div class="skeleton" data-skeleton></div>` : ''}
      ${tiers?.length ? tierList(tiers, { all: true, sheet: true }) : ''}
      ${!s.loading && !tiers?.length ? html`<p class="muted">${s.error ? `Couldn't load the drops: ${s.error}` : 'Twitch lists no timed drops for this campaign.'}</p>` : ''}
      ${s.detail?.channels?.length ? html`<h4>Only counts on these channels</h4><div class="channels">${s.detail.channels.map((name) => html`<a href="https://www.twitch.tv/${encodeURIComponent(name.toLowerCase())}" target="_blank" rel="noopener noreferrer">${name}</a>`)}${s.detail.channelCount > s.detail.channels.length ? html`<span>+${s.detail.channelCount - s.detail.channels.length} more</span>` : ''}</div>` : ''}
      ${detailsUrl ? html`<div class="sheet-links"><a class="btn ghost sm" href="${detailsUrl}" target="_blank" rel="noopener noreferrer">${icon('external', 'sm')}Open on Twitch</a></div>` : ''}
    </div>
    <footer class="sheet-actions">
      ${canQueue && !finished && others.length && !position ? html`<label class="also"><input type="checkbox" id="also-others" ${raw(s.also ? 'checked' : '')}> Also for ${others.map((a) => a.login).join(', ')}</label>` : ''}
      ${finished && !position ? html`<p class="reason">Every drop is claimed, so there's nothing left to watch.</p>`
        : canQueue ? html`<div class="row-buttons">
        ${position
          ? html`<button class="btn danger" data-action="sheet-remove">${icon('x', 'sm')}Remove</button>
                 ${position > 1 ? html`<button class="btn" data-action="sheet-top">${icon('top', 'sm')}Move to top</button>` : ''}`
          : html`<button class="btn" data-action="sheet-add">${icon('plus', 'sm')}Add to queue</button>
                 <button class="btn" data-action="sheet-next">${icon('top', 'sm')}Watch next</button>`}
        ${watching || finished ? '' : html`<button class="btn primary" data-action="sheet-now">${icon('play', 'sm solid')}Watch now</button>`}
      </div>` : html`<p class="reason">${s.detail?.cannotQueueReason || 'This campaign can\'t be queued.'}</p>`}
    </footer>`);

  const skeleton = $('[data-skeleton]', sheet);
  if (skeleton) {
    skeleton.style.height = '120px';
  }
  const also = $('#also-others', sheet);
  if (also) also.addEventListener('change', () => { store.sheet.also = also.checked; });
  const newBody = $('.sheet-body', sheet);
  if (newBody) newBody.scrollTop = scroll;
}

function sheetTargets() {
  const s = store.sheet;
  const ids = [s.accountId];
  if (s.also) ids.push(...accounts().filter((a) => a.id !== s.accountId).map((a) => a.id));
  return ids;
}

// ---------------------------------------------------------------- activity

function renderActivityFrame() {
  setHtml($('#view-activity'), html`
    <div class="log-tools">
      <div class="chips" id="log-levels" role="group" aria-label="Show">${LOG_LEVELS.map(([id, label]) => html`<button class="chip" data-log-level="${id}" aria-pressed="${store.logLevel === id}">${label}</button>`)}</div>
      <label class="toggle"><input type="checkbox" id="log-follow" checked> Follow</label>
    </div>
    <div class="card log" id="log" role="log" aria-live="off"></div>`);
  $('#log-follow').addEventListener('change', (e) => { store.logFollow = e.target.checked; });
}

let logTimer = null;
let logBusy = false;
async function pollLogs() {
  clearTimeout(logTimer);
  logTimer = setTimeout(pollLogs, 3000);
  const account = currentAccount();
  if (!account || !activityVisible() || logBusy || document.hidden) return;

  logBusy = true;
  const entry = store.logs.get(account.id) ?? { lines: [], lastSeq: 0 };
  try {
    const page = await api(`/api/accounts/${enc(account.id)}/logs?after=${entry.lastSeq}`);
    if (page.lastSeq < entry.lastSeq) {
      entry.lines = [];
      entry.lastSeq = 0;
    } else if (page.lines.length) {
      entry.lines.push(...page.lines);
      if (entry.lines.length > 600) entry.lines.splice(0, entry.lines.length - 600);
      entry.lastSeq = page.lastSeq;
      store.logs.set(account.id, entry);
      renderActivity();
    } else if (!store.logs.has(account.id)) {
      store.logs.set(account.id, entry);
      renderActivity(true);
    }
  } catch {
    // shown by the connection banner
  } finally {
    logBusy = false;
  }
}

const levelLabel = { debug: 'DEBUG', info: 'INFO', warning: 'WARN', error: 'ERROR' };
function renderActivity(reset = false) {
  const host = $('#log');
  const account = currentAccount();
  if (!host || !account) return;
  for (const chip of $$('[data-log-level]')) chip.setAttribute('aria-pressed', String(chip.dataset.logLevel === store.logLevel));

  const entry = store.logs.get(account.id);
  const show = (line) => store.logLevel === 'all' || (store.logLevel === 'problems' ? line.level === 'warning' || line.level === 'error' : line.level !== 'debug');
  const lines = (entry?.lines ?? []).filter(show);
  const atBottom = host.scrollHeight - host.scrollTop - host.clientHeight < 40;

  setHtml(host, lines.length
    ? lines.map((line) => html`<div class="log-line ${line.level}"><time datetime="${line.at}">${clockFormat.format(new Date(line.at))}</time><span class="lvl">${levelLabel[line.level] || line.level}</span><span class="msg">${line.message}</span></div>`)
    : html`<div class="empty">${icon('activity')}<p>${entry ? 'Nothing logged yet for this filter.' : 'Loading…'}</p></div>`);

  if (store.logFollow && (atBottom || reset)) host.scrollTop = host.scrollHeight;
}

// ---------------------------------------------------------------- menus & toasts

const menuLayer = $('#menu-layer');

function openMenu(anchor, items) {
  setHtml(menuLayer, html`<div class="menu" role="menu">${items.map((item, i) => {
    if (item === 'sep') return html`<hr>`;
    if (item.label && item.heading) return html`<div class="menu-label">${item.label}</div>`;
    if (item.foot) return html`<div class="foot">${item.foot}</div>`;
    return html`<button role="menuitem" data-menu-item="${i}" class="${item.danger ? 'danger' : ''}" ${raw(item.disabled ? 'disabled' : '')}>${icon(item.icon, 'sm')}<span>${item.label}</span>${item.checked ? html`<span class="check">${icon('check', 'sm')}</span>` : ''}</button>`;
  })}</div>`);
  menuLayer.hidden = false;

  const menu = $('.menu', menuLayer);
  const rect = anchor.getBoundingClientRect();
  const width = menu.offsetWidth;
  const height = menu.offsetHeight;
  const left = Math.max(12, Math.min(window.innerWidth - width - 12, rect.right - width));
  const below = rect.bottom + 6;
  const top = below + height > window.innerHeight - 12 ? Math.max(12, rect.top - height - 6) : below;
  menu.style.left = `${left}px`;
  menu.style.top = `${top}px`;

  menuLayer.onclick = (event) => {
    const button = event.target.closest('[data-menu-item]');
    if (button) {
      const item = items[Number(button.dataset.menuItem)];
      closeMenu();
      item.run?.();
    } else if (!event.target.closest('.menu')) {
      closeMenu();
    }
  };
  $('[data-menu-item]:not([disabled])', menu)?.focus();
}

function closeMenu() {
  menuLayer.hidden = true;
  menuLayer.innerHTML = '';
}

function toast(message, { action = null, error = false, timeout = 4500 } = {}) {
  const host = $('#toasts');
  const el = document.createElement('div');
  el.className = `toast${error ? ' error' : ''}`;
  setHtml(el, html`${icon(error ? 'alert' : 'check', 'sm')}<span class="grow">${message}</span>${action ? html`<button type="button">${action.label}</button>` : ''}`);
  if (action) {
    $('button', el).addEventListener('click', () => {
      el.remove();
      action.run();
    });
  }
  host.append(el);
  while (host.children.length > 3) host.firstElementChild.remove();
  setTimeout(() => el.remove(), action ? timeout + 2500 : timeout);
}

// ---------------------------------------------------------------- theme

function applyTheme(theme) {
  if (theme === 'light' || theme === 'dark') document.documentElement.dataset.theme = theme;
  else delete document.documentElement.dataset.theme;
}

function appMenu(anchor) {
  const theme = localStorage.getItem(KEY.theme) || 'system';
  const setTheme = (value) => () => {
    localStorage.setItem(KEY.theme, value);
    applyTheme(value);
  };
  const raw_version = (store.state?.version || store.session?.version || '').split('+')[0];
  const version = raw_version.startsWith('0.0.0') ? '' : raw_version;
  openMenu(anchor, [
    { icon: 'refresh', label: 'Refresh campaign list', run: () => ensureCampaigns(true) },
    'sep',
    { heading: true, label: 'Theme' },
    { icon: 'monitor', label: 'Match device', checked: theme === 'system', run: setTheme('system') },
    { icon: 'moon', label: 'Dark', checked: theme === 'dark', run: setTheme('dark') },
    { icon: 'sun', label: 'Light', checked: theme === 'light', run: setTheme('light') },
    ...(store.session?.loginRequired ? ['sep', { icon: 'logout', label: 'Sign out', danger: true, run: signOut }] : []),
    { foot: `TwitchDropsBot${version ? ` ${version}` : ''}${store.state?.demo ? ' · demo data' : ''}` },
  ]);
}

// ---------------------------------------------------------------- events

document.addEventListener('click', async (event) => {
  const target = event.target.closest('[data-action],[data-goto],[data-account],[data-open],[data-quick-add],[data-menu],[data-tab-button],[data-right],[data-filter],[data-log-level]');
  if (!target || target.disabled) return;
  const account = currentAccount();
  const d = target.dataset;

  if (d.tabButton) return selectTab(d.tabButton);
  if (d.right) return selectRight(d.right);
  if (d.account) return selectAccount(d.account);
  if (d.goto) {
    if (sheet.open) closeSheet();
    if (desktop.matches && (d.goto === 'campaigns' || d.goto === 'activity')) return selectRight(d.goto);
    return selectTab(d.goto);
  }
  if (d.filter) {
    store.filter = d.filter;
    localStorage.setItem(KEY.filter, d.filter);
    return renderCampaignList(true);
  }
  if (d.logLevel) {
    store.logLevel = d.logLevel;
    localStorage.setItem(KEY.logLevel, d.logLevel);
    return renderActivity(true);
  }
  if ('open' in d) return openCampaign(d.open);
  if (d.quickAdd) {
    const c = cachedCampaign(account.id, d.quickAdd);
    target.disabled = true;
    await addToQueue([account.id], d.quickAdd, { name: c?.name ?? 'Campaign' });
    target.disabled = false;
    return;
  }
  if (d.menu === 'queue') {
    const index = account.queue.findIndex((q) => q.campaignId === d.id);
    const item = account.queue[index];
    if (!item) return;
    return openMenu(target, [
      { icon: 'play', label: 'Watch now', disabled: item.state === 'watching', run: () => addToQueue([account.id], item.campaignId, { position: 'top', switchNow: true, name: item.campaignName }) },
      { icon: 'top', label: 'Move to top', disabled: index === 0, run: () => moveInQueue(account, item.campaignId, 0, 0) },
      { icon: 'up', label: 'Move up', disabled: index === 0, run: () => moveInQueue(account, item.campaignId, -1) },
      { icon: 'down', label: 'Move down', disabled: index === account.queue.length - 1, run: () => moveInQueue(account, item.campaignId, 1) },
      { icon: 'info', label: 'Details', run: () => openCampaign(item.campaignId) },
      'sep',
      { icon: 'x', label: 'Remove from queue', danger: true, run: () => removeFromQueue(account.id, item.campaignId) },
    ]);
  }

  switch (d.action) {
    case 'app-menu': return appMenu(target);
    case 'switch': return switchNow(account);
    case 'refresh-campaigns': return ensureCampaigns(true);
    case 'retry-campaigns': return ensureCampaigns();
    case 'close-sheet': return closeSheet();
    case 'clear-finished': {
      const finished = account.queue.filter((q) => q.state === 'done' || q.state === 'ended' || q.state === 'unavailable');
      for (const item of finished) await removeFromQueue(account.id, item.campaignId, { quiet: true });
      return toast(`Cleared ${finished.length} finished`);
    }
    case 'sheet-add':
    case 'sheet-next':
    case 'sheet-now': {
      const s = store.sheet;
      const c = s.detail?.campaign ?? cachedCampaign(s.accountId, s.campaignId);
      const ok = await addToQueue(sheetTargets(), s.campaignId, {
        position: d.action === 'sheet-add' ? 'bottom' : 'top',
        switchNow: d.action === 'sheet-now',
        name: c?.name ?? 'Campaign',
      });
      if (ok && d.action !== 'sheet-add') closeSheet();
      return;
    }
    case 'sheet-remove': return removeFromQueue(store.sheet.accountId, store.sheet.campaignId);
    case 'sheet-recheck': return openCampaign(store.sheet.campaignId, store.sheet.accountId, { recheck: true });
    case 'sheet-top': return moveInQueue(accountById(store.sheet.accountId), store.sheet.campaignId, 0, 0);
    default:
  }
});

document.addEventListener('pointerdown', (event) => {
  const handle = event.target.closest('[data-drag]');
  if (handle && event.button === 0) {
    event.preventDefault();
    startDrag(event, handle);
  }
});

document.addEventListener('keydown', (event) => {
  const handle = event.target.closest?.('[data-drag]');
  if (handle && (event.key === 'ArrowUp' || event.key === 'ArrowDown')) {
    event.preventDefault();
    const id = handle.closest('.q-item').dataset.id;
    moveInQueue(currentAccount(), id, event.key === 'ArrowUp' ? -1 : 1).then(() => {
      setTimeout(() => $(`.q-item[data-id="${CSS.escape(id)}"] [data-drag]`)?.focus(), 250);
    });
    return;
  }
  if (event.key === 'Escape' && !menuLayer.hidden) {
    closeMenu();
    return;
  }
  if (event.key === '/' && !['INPUT', 'TEXTAREA'].includes(document.activeElement?.tagName) && campaignsVisible()) {
    event.preventDefault();
    $('#campaign-search')?.focus();
  }
});

// Thumbnails that fail to load leave the initials underneath
document.addEventListener('error', (event) => {
  if (event.target.tagName === 'IMG') event.target.remove();
}, true);

sheet.addEventListener('close', () => {
  if (store.sheet) closeSheet();
});
sheet.addEventListener('click', (event) => {
  if (event.target === sheet) closeSheet();
});
window.addEventListener('popstate', () => {
  if (sheet.open) closeSheet(true);
});

window.addEventListener('scroll', () => {
  $('#topbar')?.classList.toggle('scrolled', window.scrollY > 4);
}, { passive: true });

desktop.addEventListener('change', () => {
  renderTabs();
  if (campaignsVisible()) ensureCampaigns();
  if (activityVisible()) pollLogs();
});

document.addEventListener('visibilitychange', () => {
  if (document.hidden || app.dataset.view !== 'main') return;
  if (!store.connected) connect();
  if (activityVisible()) pollLogs();
});

// ---------------------------------------------------------------- sign in

function renderLogin(message = '') {
  events?.close();
  closeSheet();
  app.dataset.view = 'login';
  setHtml(app, html`<main class="login">
    <form class="card login-card" id="login-form" novalidate>
      <span class="brand-mark">${icon('logo')}</span>
      <h1>TwitchDropsBot</h1>
      <p>Sign in to see progress and manage the queue.</p>
      <label class="field"><span>Password</span><input id="password" type="password" autocomplete="current-password" required></label>
      <p class="form-error" id="login-error" ${raw(message ? '' : 'hidden')}>${message}</p>
      <button class="btn primary block" type="submit">Sign in</button>
    </form>
  </main>`);

  const form = $('#login-form');
  const input = $('#password');
  const error = $('#login-error');
  input.focus();

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    const button = $('button[type=submit]', form);
    button.disabled = true;
    error.hidden = true;
    try {
      await api('/api/login', { method: 'POST', body: { password: input.value }, auth: false });
      await start();
    } catch (e) {
      error.textContent = e.status === 401 ? 'That password isn\'t right.' : e.message;
      error.hidden = false;
      input.select();
    } finally {
      button.disabled = false;
    }
  });
}

function signedOut(message) {
  if (app.dataset.view === 'login') return;
  renderLogin(message);
}

async function signOut() {
  try {
    await api('/api/logout', { method: 'POST', auth: false });
  } finally {
    renderLogin();
  }
}

// ---------------------------------------------------------------- start

async function start() {
  try {
    store.session = await api('/api/session', { auth: false });
  } catch (e) {
    setHtml(app, html`<main class="login"><div class="card login-card"><span class="brand-mark">${icon('logo')}</span><h1>Can't reach the bot</h1><p>${e.message}</p><button class="btn primary block" data-action="reload">Try again</button></div></main>`);
    $('[data-action="reload"]').addEventListener('click', () => location.reload());
    return;
  }

  if (!store.session.authenticated) {
    renderLogin();
    return;
  }

  renderShell();
  try {
    setState(await api('/api/state'));
  } catch {
    // the event stream delivers it
  }
  connect();
}

applyTheme(localStorage.getItem(KEY.theme) || 'system');
setInterval(tickClocks, 1000);
start();
