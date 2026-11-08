// Browser client for the Game Data Aggregator REST API. It calls the same public endpoints as any other
// client (Postman, curl...). Data from external APIs is only ever inserted as text, never as HTML.
'use strict';

//  Addresses of the API endpoints.
const API = {
  aggregation: 'api/v1/aggregation',
  statistics: 'api/v1/statistics',
  sources: 'api/v1/sources',
  token: 'api/v1/auth/token',
};

//  Readable names for the API's values, and how often statistics refresh.
const CATEGORIES = ['GameInfo', 'Deal', 'PlayerActivity'];
const CATEGORY_LABELS = { GameInfo: 'Game info', Deal: 'Deal', PlayerActivity: 'Player activity' };
const STATUS_LABELS = { NotConfigured: 'Not configured' };
const DATE_KIND_LABELS = { Released: 'Released', PriceUpdated: 'Price updated', Measured: 'Measured' };
const STATS_REFRESH_MS = 5000;
const NO_TOKEN_MESSAGE = 'This endpoint needs an access token. The page gets one automatically only while the API runs in Development.';

//  Shortcuts for finding elements and formatting numbers and dates in the reader's language.
const $ = (selector) => document.querySelector(selector);
const numberFormat = new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 });
const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' });
const dateTimeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'medium' });

//  What the page is currently showing, and each source with the categories it gives.
const state = { page: 1, lastResult: null, statsTimer: null, token: null, sources: [], searching: false };

/** Creates an element; string children become text nodes, so values are never parsed as HTML. */
function el(tag, props = {}, ...children) {
  //  Apply each property, skipping empty ones.
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(props)) {
    if (value === undefined || value === null || value === false) continue;
    if (key === 'className') node.className = value;
    else if (key === 'dataset') Object.assign(node.dataset, value);
    else node.setAttribute(key, value === true ? '' : value);
  }

  //  Add the children, leaving out empty ones.
  node.append(...children.flat().filter((c) => c !== null && c !== undefined && c !== false));
  return node;
}

/** Only http(s) links from external data are used, so a "javascript:" URL can never be rendered. */
function safeUrl(value) {
  //  Keep web links only, and drop anything that is not a valid address.
  try {
    const url = new URL(value);
    return url.protocol === 'https:' || url.protocol === 'http:' ? url.href : null;
  } catch {
    return null;
  }
}

/** "salePrice" -> "Sale price" */
function humanize(key) {
  //  Split the name into lower-case words and capitalise the first.
  const words = key.replace(/([a-z0-9])([A-Z])/g, '$1 $2').toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

/** Shows a duration in milliseconds. */
function formatMs(value) {
  return `${numberFormat.format(value)} ms`;
}

/**
 * Gets an access token from the development token endpoint, reusing it until shortly before it expires.
 * Returns null when there is none, for example outside Development, where the endpoint does not exist.
 */
async function getAccessToken() {
  //  Reuse the token while it has more than a minute left.
  if (state.token && state.token.expiresAt - Date.now() > 60_000) return state.token.value;

  //  Ask for a new one, and give up quietly when the endpoint is not there.
  try {
    const response = await fetch(API.token, { method: 'POST', headers: { Accept: 'application/json' } });
    if (!response.ok) return null;
    const body = await response.json();
    state.token = { value: body.accessToken, expiresAt: Date.now() + body.expiresIn * 1000 };
    return state.token.value;
  } catch {
    return null;
  }
}

/** Calls the API with an access token and reads the answer, when it is JSON. */
async function fetchJson(url) {
  //  Send the request with a token when one is available.
  const send = async () => {
    const token = await getAccessToken();
    const headers = { Accept: 'application/json' };
    if (token) headers.Authorization = `Bearer ${token}`;
    return fetch(url, { headers });
  };

  //  Try once more with a fresh token when the old one was refused, for example after the app restarted.
  let response = await send();
  if (response.status === 401 && state.token) {
    state.token = null;
    response = await send();
  }

  //  Read the body only if JSON came back.
  const body = response.headers.get('content-type')?.includes('json') ? await response.json() : null;
  return { response, body };
}

// ---------------------------------------------------------------- Tabs

/** Shows one tab and hides the others. */
function selectTab(name) {
  //  Mark the chosen tab as selected and show only its panel.
  for (const tab of document.querySelectorAll('[role="tab"]')) {
    const selected = tab.id === `tab-${name}`;
    tab.setAttribute('aria-selected', String(selected));
    tab.tabIndex = selected ? 0 : -1;
    document.getElementById(tab.getAttribute('aria-controls')).hidden = !selected;
  }

  //  Load statistics when their tab opens, and stop refreshing them when it closes.
  if (name === 'statistics') loadStatistics();
  else stopAutoRefresh();
}

// ---------------------------------------------------------------- Search form

/** Fills the source choices from the API, and remembers which categories each source gives. */
async function loadSources() {
  //  Show one choice per source, or a note if the list cannot be loaded.
  const group = $('#sources-group');
  try {
    const { response, body } = await fetchJson(API.sources);
    if (!response.ok || !Array.isArray(body)) throw new Error(`HTTP ${response.status}`);
    state.sources = body;
    group.replaceChildren(...body.map((source) => pill('sources', source.name, source.name)));
  } catch (error) {
    group.replaceChildren(el('span', { className: 'muted' }, `Could not load sources (${error.message}).`));
  }
}

/** Joins names into readable text: "A", "A and B", "A, B and C". */
function joinNames(names) {
  return names.length <= 1 ? names.join('') : `${names.slice(0, -1).join(', ')} and ${names.at(-1)}`;
}

/** Returns the boxes of one choice group, "sources" or "categories". */
function boxesOf(group) {
  return [...$('#search-form').querySelectorAll(`input[name="${group}"]`)];
}

/**
 * Returns what the user wants from a group: the choices they ticked plus the ticks parked on choices that are turned
 * off for now. Ticks the page added by itself do not count. While the group has none of the user's own live ticks,
 * every choice counts, so an empty group never limits the other one while the user is still choosing.
 */
function wantedValues(group) {
  //  Count every choice while the user has no live tick of their own in the group.
  const boxes = boxesOf(group);
  const ownLiveTicks = boxes.filter((b) => b.checked && b.dataset.auto !== 'true');
  if (!ownLiveTicks.length) return boxes.map((b) => b.value);

  //  Otherwise use the user's live ticks and their parked ticks.
  return boxes.filter((b) => ownLiveTicks.includes(b) || b.dataset.wasChecked === 'true').map((b) => b.value);
}

/** Removes the ticks the page added by itself, so they can be worked out again. */
function clearAutomaticTicks(group) {
  for (const box of boxesOf(group).filter((b) => b.dataset.auto === 'true')) {
    box.checked = false;
    delete box.dataset.auto;
  }
}

/** Makes every tick in a group the user's own, for example after they click one of its choices. */
function keepTicksAsChosen(group) {
  for (const box of boxesOf(group)) delete box.dataset.auto;
}

/**
 * Ticks a group's only available choice when nothing in the group is ticked. With one possible answer, asking the
 * user to click it adds nothing. The tick is marked as the page's own, so it does not limit the other group.
 */
function tickOnlyChoiceIfEmpty(group) {
  //  Act only on an empty group with exactly one available choice.
  const boxes = boxesOf(group);
  const available = boxes.filter((b) => !b.disabled);
  if (boxes.some((b) => b.checked) || available.length !== 1) return;

  //  Tick it, and mark the tick as automatic.
  available[0].checked = true;
  available[0].dataset.auto = 'true';
}

/** Returns the groups that have no ticked choice, so a search cannot run yet. */
function emptyGroups() {
  return ['sources', 'categories'].filter((group) => {
    const boxes = boxesOf(group);
    return boxes.length > 0 && !boxes.some((b) => b.checked);
  });
}

/**
 * Turns the Search button off while a group is empty, and says, right under the search box, what is missing.
 * The message sits there because the button is where the user looks, and the filters may be folded away.
 */
function updateSearchAvailability() {
  //  Name the empty groups in one sentence.
  const empty = emptyGroups();
  const names = empty.map((group) => (group === 'sources' ? 'one source' : 'one category'));
  const message = $('#search-blocked');
  message.textContent = empty.length ? `Choose at least ${joinNames(names)} to search.` : '';
  message.hidden = empty.length === 0;

  //  Allow a search only when every group has a tick and no search is running.
  $('#search-button').disabled = empty.length > 0 || state.searching;
}

/**
 * Turns a choice on or off. A choice that is turned off is unticked, and remembers whether it was ticked,
 * so it comes back as the user left it once it can match again. Returns true when a tick was parked.
 */
function setAvailable(box, available) {
  //  Turn the choice off, parking the user's tick for later.
  if (!available && !box.disabled) {
    const wasChecked = box.checked;
    box.dataset.wasChecked = String(wasChecked);
    box.checked = false;
    box.disabled = true;
    return wasChecked;
  }

  //  Turn the choice back on, with the tick it had before.
  if (available && box.disabled) {
    box.disabled = false;
    box.checked = box.dataset.wasChecked === 'true';
    delete box.dataset.wasChecked;
  }
  return false;
}

/**
 * Turns each choice of a group on or off with the given rule. When that parks the group's last tick, the remaining
 * choices are ticked: no tick already means "all", so the search is the same, but the page now shows what it uses.
 */
function applyAvailability(group, isAvailable) {
  //  Turn each choice on or off, noting whether a tick was parked.
  const boxes = boxesOf(group);
  let parkedATick = false;
  for (const box of boxes) parkedATick = setAvailable(box, isAvailable(box.value)) || parkedATick;

  //  Show the remaining choices as ticked when the last tick was just parked.
  if (parkedATick && !boxes.some((b) => b.checked)) {
    for (const box of boxes.filter((b) => !b.disabled)) box.checked = true;
  }
}

/** Turns off each category that none of the wanted sources gives. */
function updateCategoryChoices() {
  const sources = state.sources.filter((s) => wantedValues('sources').includes(s.name));
  applyAvailability('categories', (category) => sources.some((s) => s.categories.includes(category)));
}

/** Turns off each source that gives none of the wanted categories. */
function updateSourceChoices() {
  const categories = wantedValues('categories');
  applyAvailability('sources', (name) => {
    const source = state.sources.find((s) => s.name === name);
    return !source || source.categories.some((c) => categories.includes(c));
  });
}

/**
 * Turns off the choices that cannot match the other group's choices, so the user never builds a search that is sure
 * to find nothing. The group the user just changed is kept as it is, and the other group adapts to it.
 */
function updateChoiceAvailability(changedGroup = 'sources') {
  //  Skip the check when the sources could not be loaded, but still check whether a search can run.
  if (!state.sources.length) {
    updateSearchAvailability();
    return;
  }

  //  Treat the user's ticks in the changed group as their own, and work out the other group's automatic ticks again.
  const otherGroup = changedGroup === 'categories' ? 'sources' : 'categories';
  keepTicksAsChosen(changedGroup);
  clearAutomaticTicks(otherGroup);

  //  Adapt the other group first, then check the changed one, twice so both settle.
  const steps = changedGroup === 'categories'
    ? [updateSourceChoices, updateCategoryChoices]
    : [updateCategoryChoices, updateSourceChoices];
  for (let pass = 0; pass < 2; pass++) steps.forEach((step) => step());

  //  Tick the other group's only possible choice, then show the reasons and whether a search can run.
  tickOnlyChoiceIfEmpty(otherGroup);
  renderChoiceHint();
  updateSearchAvailability();
}

/** Explains, under the filters, why each turned-off choice is unavailable. */
function renderChoiceHint() {
  //  Write one short reason per unavailable category.
  const reasons = boxesOf('categories').filter((b) => b.disabled).map((box) => {
    const givers = state.sources.filter((s) => s.categories.includes(box.value)).map((s) => s.name);
    return givers.length
      ? `${CATEGORY_LABELS[box.value]} is unavailable: only ${joinNames(givers)} ${givers.length === 1 ? 'gives' : 'give'} it.`
      : `${CATEGORY_LABELS[box.value]} is unavailable: no source gives it.`;
  });

  //  Write one short reason per unavailable source.
  for (const box of boxesOf('sources').filter((b) => b.disabled)) {
    const source = state.sources.find((s) => s.name === box.value);
    reasons.push(`${source.name} is unavailable: it gives only ${joinNames(source.categories.map((c) => CATEGORY_LABELS[c] ?? c))}.`);
  }

  //  Show the reasons, or hide the box when every choice is available.
  const hint = $('#choice-hint');
  hint.replaceChildren(...reasons.map((reason) => el('li', {}, reason)));
  hint.hidden = reasons.length === 0;
}

/** Creates a ticked checkbox choice. */
function pill(name, value, label) {
  return el('label', { className: 'pill' },
    el('input', { type: 'checkbox', name, value, checked: true }),
    el('span', {}, label));
}

/** Fills the category choices. */
function renderCategories() {
  $('#categories-group').replaceChildren(...CATEGORIES.map((c) => pill('categories', c, CATEGORY_LABELS[c])));
}

/** Builds the query string exactly as an API client would send it. Unticking nothing means "all". */
function buildQuery(page) {
  //  Read the form.
  const form = $('#search-form');
  const data = new FormData(form);
  const params = new URLSearchParams();

  //  Always send the search text.
  params.set('search', (data.get('search') ?? '').toString().trim());

  //  Find which available boxes in a group are ticked, and whether all of them are. Unavailable choices never
  //  match, so leaving them out of the request changes nothing.
  const allChecked = (name) => {
    const boxes = boxesOf(name).filter((b) => !b.disabled);
    const checked = boxes.filter((b) => b.checked).map((b) => b.value);
    return { checked, all: checked.length === boxes.length };
  };

  //  Send the chosen sources only when some are unticked.
  const sources = allChecked('sources');
  if (!sources.all) params.set('sources', sources.checked.join(','));

  //  Send the chosen categories only when some are unticked.
  const categories = allChecked('categories');
  if (!categories.all) params.set('categories', categories.checked.join(','));

  //  Send the tag when one is given.
  const tag = data.get('tag')?.toString().trim();
  if (tag) params.set('tag', tag);

  //  Cover whole days, from the start of the first to the end of the last, in UTC.
  const from = data.get('from')?.toString();
  if (from) params.set('from', `${from}T00:00:00Z`);
  const to = data.get('to')?.toString();
  if (to) params.set('to', `${to}T23:59:59Z`);

  //  Send sorting and paging only when they differ from the defaults.
  if (data.get('sortBy') !== 'Relevance') params.set('sortBy', data.get('sortBy'));
  if (data.get('sortDirection') !== 'Descending') params.set('sortDirection', data.get('sortDirection'));
  if (page > 1) params.set('page', String(page));
  if (data.get('pageSize') !== '20') params.set('pageSize', data.get('pageSize'));

  return params;
}

/** Restores the form from the page's own query string, so a search can be bookmarked or shared. */
function restoreFromLocation() {
  //  Do nothing unless the address holds a search.
  const params = new URLSearchParams(location.search);
  const form = $('#search-form');
  if (!params.has('search')) return false;

  //  Fill in the text and date fields.
  form.elements.search.value = params.get('search');
  form.elements.tag.value = params.get('tag') ?? '';
  form.elements.from.value = (params.get('from') ?? '').slice(0, 10);
  form.elements.to.value = (params.get('to') ?? '').slice(0, 10);

  //  Pick the matching option in each list, keeping the default for unknown values.
  for (const name of ['sortBy', 'sortDirection', 'pageSize']) {
    const select = form.elements[name];
    const value = params.get(name);
    if (value && [...select.options].some((o) => o.value.toLowerCase() === value.toLowerCase())) {
      select.value = [...select.options].find((o) => o.value.toLowerCase() === value.toLowerCase()).value;
    }
  }

  //  Tick only the sources and categories named in the address. An empty or unknown list means all, as in the API.
  for (const name of ['sources', 'categories']) {
    const wanted = (params.get(name) ?? '').toLowerCase().split(',').map((v) => v.trim()).filter(Boolean);
    const boxes = boxesOf(name);
    if (!boxes.some((box) => wanted.includes(box.value.toLowerCase()))) continue;
    for (const box of boxes) box.checked = wanted.includes(box.value.toLowerCase());
  }

  //  Turn off the categories the chosen sources cannot give, for example in an old or hand-made link.
  updateChoiceAvailability('sources');

  //  Remember the page to open.
  state.page = Number(params.get('page')) || 1;
  return true;
}

/** Puts every filter back to its default. */
function resetFilters() {
  //  Make every choice available and ticked, and clear or reset every other field.
  const form = $('#search-form');
  for (const box of form.querySelectorAll('.pill input[type="checkbox"]')) {
    box.disabled = false;
    box.checked = true;
    delete box.dataset.wasChecked;
    delete box.dataset.auto;
  }
  renderChoiceHint();
  updateSearchAvailability();
  form.elements.tag.value = '';
  form.elements.from.value = '';
  form.elements.to.value = '';
  form.elements.sortBy.value = 'Relevance';
  form.elements.sortDirection.value = 'Descending';
  form.elements.pageSize.value = '20';
}

// ---------------------------------------------------------------- Search

/** Runs a search and shows the results. */
async function search(page = 1) {
  //  Ask for more text when the search is too short.
  const searchInput = $('#search-form').elements.search;
  const text = searchInput.value.trim();
  if (text.length < 2) {
    showSearchError('Enter at least 2 characters.');
    searchInput.focus();
    return;
  }

  //  Do not search while a group of choices is empty. The message under the search box already says what is missing.
  if (emptyGroups().length) {
    updateSearchAvailability();
    return;
  }

  //  Build the request and put it in the page address, so it can be shared.
  state.page = page;
  const params = buildQuery(page);
  const url = `${API.aggregation}?${params}`;
  history.replaceState(null, '', `?${params}`);

  //  Show that the search is running.
  const button = $('#search-button');
  state.searching = true;
  button.disabled = true;
  button.textContent = 'Searching…';
  hide('#search-error');
  showRequest(url, 'Sending…');

  //  Send the request and show the results, or the reason it failed.
  const started = performance.now();
  try {
    const { response, body } = await fetchJson(url);
    const elapsed = performance.now() - started;
    showRequest(url, `HTTP ${response.status} · ${formatMs(elapsed)}`);

    if (!response.ok) {
      showProblem(body, response.status);
      renderResult(null);
      return;
    }
    renderResult(body);
  } catch (error) {
    showRequest(url, 'No response');
    showSearchError(`The request failed: ${error.message}`);
    renderResult(null);
  } finally {
    //  Allow the next search, unless the choices changed meanwhile and a group is now empty.
    state.searching = false;
    button.textContent = 'Search';
    updateSearchAvailability();
  }
}

/** Shows the request address and its outcome. */
function showRequest(url, meta) {
  //  Show the full address and keep it for copying.
  const absolute = new URL(url, location.href).href;
  $('#request-url').textContent = `GET ${absolute}`;
  $('#request-url').dataset.url = absolute;
  $('#request-meta').textContent = meta;
  $('#request-bar').hidden = false;
}

/** Hides an element. */
function hide(selector) {
  $(selector).hidden = true;
}

/** Shows an error message with an optional list of details. */
function showSearchError(message, details = []) {
  //  Write the message and its details, then show the box.
  const box = $('#search-error');
  //  The details list is added only when there are details. replaceChildren() would show a null as the text "null".
  const list = details.length ? [el('ul', {}, details.map((d) => el('li', {}, d)))] : [];
  box.replaceChildren(el('strong', {}, message), ...list);
  box.hidden = false;
}

/** Renders an RFC 9457 problem details response (validation errors list each invalid parameter). */
function showProblem(problem, status) {
  //  List every field error, or the general reason when there are none.
  const details = [];
  if (problem?.errors) {
    for (const [field, messages] of Object.entries(problem.errors)) {
      for (const message of messages) details.push(`${field}: ${message}`);
    }
  } else if (problem?.detail) {
    details.push(problem.detail);
  } else if (status === 401) {
    details.push(NO_TOKEN_MESSAGE);
  }

  showSearchError(problem?.title ?? `The request failed with HTTP ${status}.`, details);
}

/** Shows a search result, or clears the results when there is none. */
function renderResult(result) {
  //  Show or hide the result areas, and fill in the sources and items.
  state.lastResult = result;
  const hasResult = result !== null;

  $('#empty-state').hidden = hasResult;
  $('#results-header').hidden = !hasResult;
  $('#raw-json').hidden = !hasResult;
  $('#source-status').replaceChildren(...(hasResult ? result.sources.map(renderSource) : []));
  $('#results').replaceChildren(...(hasResult ? result.items.map(renderItem) : []));
  if (!hasResult) return;

  //  Show the totals, the page buttons and the raw answer.
  const pages = Math.max(1, Math.ceil(result.totalCount / result.pageSize));
  $('#results-summary').textContent =
    `${result.totalCount} result${result.totalCount === 1 ? '' : 's'} for “${result.search}”`;
  $('#page-info').textContent = `Page ${result.page} of ${pages}`;
  $('#prev-page').disabled = result.page <= 1;
  $('#next-page').disabled = result.page >= pages;
  $('#raw-json-content').textContent = JSON.stringify(result, null, 2);

  //  When nothing matched, say whether the sources found nothing or the filters removed what they found.
  if (result.items.length === 0) {
    const found = result.sources.reduce((sum, s) => sum + s.itemCount, 0);
    const message = found > 0 && result.totalCount === 0
      ? `The sources found ${found} item${found === 1 ? '' : 's'}, but none matches your filters. Try another tag or wider dates.`
      : result.totalCount > 0
        ? 'This page is past the last result.'
        : `No source found anything for “${result.search}”.`;
    $('#results').append(el('p', { className: 'muted' }, message));
  }
}

/** Builds the card showing how one source did. */
function renderSource(source) {
  //  Note how many items the source found and how many pass the filters, unless it gave none at all.
  const facts = [];
  if (source.status !== 'Failed' && source.status !== 'NotConfigured') {
    facts.push(`${source.itemCount} found · ${source.matchCount} match`);
  }

  //  Note the time taken, and the age of the data when it is not live.
  facts.push(formatMs(source.durationMs));
  if (source.fetchedAt && source.status !== 'Live') {
    facts.push(`data from ${dateTimeFormat.format(new Date(source.fetchedAt))}`);
  }

  //  Show the name, status, facts and any error.
  return el('div', { className: 'card source-card' },
    el('div', { className: 'row' },
      el('strong', {}, source.source),
      el('span', { className: `status status-${source.status}` }, STATUS_LABELS[source.status] ?? source.status)),
    el('span', { className: 'muted' }, facts.join(' · ')),
    source.error
      ? el('span', { className: source.status === 'NotConfigured' ? 'error config' : 'error' }, source.error)
      : null);
}

/** Builds the card for one result item. */
function renderItem(item) {
  const card = $('#item-template').content.firstElementChild.cloneNode(true);

  //  Show the image, or a note when there is none or it fails to load.
  const media = card.querySelector('.item-media');
  const image = safeUrl(item.imageUrl);
  if (image) {
    const img = el('img', { src: image, alt: '', loading: 'lazy', referrerpolicy: 'no-referrer' });
    img.addEventListener('error', () => media.replaceChildren('No image'), { once: true });
    media.append(img);
  } else {
    media.textContent = 'No image';
  }

  //  Show the source and category badges.
  card.querySelector('.item-badges').append(
    el('span', { className: 'badge source' }, item.source),
    el('span', { className: 'badge' }, CATEGORY_LABELS[item.category] ?? item.category));

  //  Show the title, as a link when there is a safe one.
  const link = safeUrl(item.url);
  card.querySelector('.item-title').append(
    link ? el('a', { href: link, target: '_blank', rel: 'noopener noreferrer' }, item.title) : item.title);

  //  Show the date with what it means, or note that there is none.
  const meta = card.querySelector('.item-meta');
  if (item.date) {
    const label = DATE_KIND_LABELS[item.dateKind];
    const date = dateFormat.format(new Date(item.date));
    meta.textContent = label ? `${label} ${date}` : date;
  } else {
    meta.textContent = 'No date';
  }

  //  Show the relevance as a bar and a number.
  const relevance = Math.max(0, Math.min(1, item.relevance ?? 0));
  card.querySelector('.relevance-bar span').style.width = `${relevance * 100}%`;
  card.querySelector('.relevance-value').textContent = `Relevance ${numberFormat.format(relevance)}`;

  //  List the tags.
  card.querySelector('.tags').append(...(item.tags ?? []).map((tag) => el('li', {}, tag)));

  //  List the figures, or remove their section when there are none.
  const metrics = Object.entries(item.metrics ?? {});
  const metricsList = card.querySelector('.metrics');
  if (metrics.length) {
    for (const [key, value] of metrics) metricsList.append(el('dt', {}, humanize(key)), el('dd', {}, numberFormat.format(value)));
  } else {
    metricsList.remove();
  }

  //  List the other details, or remove their section when there are none.
  const attributes = Object.entries(item.attributes ?? {});
  const attributesBlock = card.querySelector('.attributes');
  if (attributes.length) {
    const list = attributesBlock.querySelector('dl');
    for (const [key, value] of attributes) list.append(el('dt', {}, humanize(key)), el('dd', {}, value));
  } else {
    attributesBlock.remove();
  }

  return card;
}

/** Copies the request address and briefly confirms it on the button. */
async function copyRequestUrl() {
  //  Copy the address and say whether it worked.
  const button = $('#copy-url');
  try {
    await navigator.clipboard.writeText($('#request-url').dataset.url);
    button.textContent = 'Copied';
  } catch {
    button.textContent = 'Copy failed';
  }

  //  Restore the button text shortly after.
  setTimeout(() => { button.textContent = 'Copy URL'; }, 1500);
}

// ---------------------------------------------------------------- Statistics

/** Loads the statistics and shows them, or an error. */
async function loadStatistics() {
  //  Fetch the figures and show them, or say why they could not be loaded.
  hide('#stats-error');
  try {
    const { response, body } = await fetchJson(API.statistics);
    if (response.status === 401) throw new Error(NO_TOKEN_MESSAGE);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    renderStatistics(body);
  } catch (error) {
    const box = $('#stats-error');
    box.textContent = `Could not load statistics: ${error.message}`;
    box.hidden = false;
  }
}

/** Fills the statistics table, one row per source. */
function renderStatistics(statistics) {
  //  Explain what is counted and when it was updated.
  $('#stats-generated').textContent =
    `Every HTTP request sent to each external API since start-up, retries included; cache hits are not counted. ` +
    `Updated ${dateTimeFormat.format(new Date(statistics.generatedAt))}.`;

  //  Build one row per source.
  $('#stats-body').replaceChildren(...statistics.apis.map((api) => {
    //  Helpers to read a speed group, show it as a cell, and work out its share of all requests.
    const bucket = (name) => api.buckets.find((b) => b.bucket === name) ?? { requests: 0, averageResponseTimeMs: 0 };
    const bucketCell = (name) => {
      const b = bucket(name);
      return el('td', { className: 'bucket-cell' },
        String(b.requests),
        el('small', {}, b.requests ? `avg ${formatMs(b.averageResponseTimeMs)}` : '—'));
    };
    const share = (name) => (api.totalRequests ? (bucket(name).requests / api.totalRequests) * 100 : 0);

    //  Show the totals, each speed group and a bar of how requests split between them.
    return el('tr', {},
      el('th', { scope: 'row' }, api.api),
      el('td', { className: 'num' }, String(api.totalRequests)),
      el('td', { className: 'num' }, String(api.failedRequests)),
      el('td', { className: 'num' }, api.totalRequests ? formatMs(api.averageResponseTimeMs) : '—'),
      bucketCell('Fast'),
      bucketCell('Average'),
      bucketCell('Slow'),
      el('td', {},
        el('div', { className: 'distribution', role: 'img', 'aria-label': `Fast ${share('Fast').toFixed(0)}%, average ${share('Average').toFixed(0)}%, slow ${share('Slow').toFixed(0)}%` },
          el('span', { className: 'fast', style: `width:${share('Fast')}%` }),
          el('span', { className: 'average', style: `width:${share('Average')}%` }),
          el('span', { className: 'slow', style: `width:${share('Slow')}%` }))));
  }));
}

/** Stops refreshing the statistics and unticks the option. */
function stopAutoRefresh() {
  clearInterval(state.statsTimer);
  state.statsTimer = null;
  $('#auto-refresh').checked = false;
}

/** Starts or stops refreshing the statistics every few seconds. */
function toggleAutoRefresh(enabled) {
  clearInterval(state.statsTimer);
  state.statsTimer = enabled ? setInterval(loadStatistics, STATS_REFRESH_MS) : null;
}

// ---------------------------------------------------------------- Start-up

/** Shows the API reference link only when the reference is available. */
async function showApiReferenceLinkIfAvailable() {
  //  Check for the API description, and keep the link hidden when it is missing.
  try {
    const response = await fetch('openapi/v1.json');
    $('#api-reference-link').hidden = !response.ok;
  } catch {
    //  Leave the link hidden, as the reference only exists while developing.
  }
}

/** Sets up the page when it loads. */
async function init() {
  //  Fill in the choices.
  renderCategories();
  await loadSources();

  //  Turn off impossible choices each time a source or category is ticked or unticked, and check that a search can run.
  $('#sources-group').addEventListener('change', () => updateChoiceAvailability('sources'));
  $('#categories-group').addEventListener('change', () => updateChoiceAvailability('categories'));
  updateSearchAvailability();

  //  Connect the buttons and fields to their actions.
  $('#search-form').addEventListener('submit', (event) => {
    event.preventDefault();
    search(1);
  });
  $('#reset-filters').addEventListener('click', resetFilters);
  $('#prev-page').addEventListener('click', () => search(state.page - 1));
  $('#next-page').addEventListener('click', () => search(state.page + 1));
  $('#copy-url').addEventListener('click', copyRequestUrl);
  $('#tab-search').addEventListener('click', () => selectTab('search'));
  $('#tab-statistics').addEventListener('click', () => selectTab('statistics'));
  $('#refresh-stats').addEventListener('click', loadStatistics);
  $('#auto-refresh').addEventListener('change', (event) => toggleAutoRefresh(event.target.checked));

  //  Show the API reference link if there is one.
  showApiReferenceLinkIfAvailable();

  //  Repeat a search from the address, or wait for the user to type one.
  if (restoreFromLocation()) search(state.page);
  else $('#search-form').elements.search.focus();
}

//  Start the page.
init();
