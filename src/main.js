import { B } from './balance.js';
import * as G from './game.js';
import { draw } from './render.js';
import * as UI from './ui.js';
import { buildCheatMenu } from './cheats.js';

const { $, el } = UI;
const canvas = $('#stage');
const ctx = canvas.getContext('2d');
canvas.width = B.map.w; canvas.height = B.map.h;

let S = G.newState();
const view = {
  mouse: { x: 0, y: 0 },
  hover: null, selected: null,
  buildMode: null, buildSize: 'small', buildOk: false,
  drawing: null,
};

const actions = {
  refresh: () => { UI.renderInspector(S, view, actions); UI.renderReward(S, actions); buildCheatMenu(S, actions); renderLineBar(); },
  toast,
  restart: () => { S = G.newState(); view.selected = null; view.drawing = null; view.buildMode = null; $('#modal').classList.add('hidden'); actions.refresh(); },
};

// ---------------- 좌표 ----------------
function toWorld(ev) {
  const r = canvas.getBoundingClientRect();
  return { x: ((ev.clientX - r.left) / r.width) * B.map.w, y: ((ev.clientY - r.top) / r.height) * B.map.h };
}
function nodeAt(p) {
  return S.nodes.find((n) => G.dist(n, p) <= (n.type === 'fortress' ? 32 : 26));
}

// ---------------- 입력 ----------------
canvas.addEventListener('mousemove', (ev) => {
  view.mouse = toWorld(ev);
  const n = nodeAt(view.mouse);
  view.hover = n?.id ?? null;
  if (view.buildMode) {
    const chk = G.canBuild(S, view.buildMode, view.buildSize, view.mouse.x, view.mouse.y);
    view.buildOk = chk.ok;
  }
  canvas.style.cursor = view.buildMode ? 'crosshair' : n ? 'pointer' : 'default';
});

canvas.addEventListener('click', (ev) => {
  const p = toWorld(ev);
  const n = nodeAt(p);

  if (view.buildMode) {
    const res = G.build(S, view.buildMode, view.buildSize, p.x, p.y);
    if (!res.ok) return toast(res.why);
    view.selected = res.node.id;
    view.buildMode = null;
    actions.refresh();
    return;
  }

  if (view.drawing) {
    if (!n) return toast('노선은 건물끼리만 이을 수 있다');
    const d = view.drawing;
    if (d.nodes[d.nodes.length - 1] === n.id) return finishLine();
    if (d.nodes[0] === n.id && d.nodes.length > 2) { d.loop = true; return finishLine(); }
    if (d.nodes.includes(n.id)) return toast('이미 이 노선에 포함된 건물이다');
    d.nodes.push(n.id);
    renderLineBar();
    return;
  }

  view.selected = n?.id ?? null;
  actions.refresh();
});

canvas.addEventListener('contextmenu', (ev) => { ev.preventDefault(); if (view.drawing) finishLine(); });

window.addEventListener('keydown', (ev) => {
  if (ev.target.tagName === 'INPUT') return;
  if (ev.key === 'Escape') { view.buildMode = null; if (view.drawing) cancelLine(); actions.refresh(); }
  if (ev.key === 'Enter' && view.drawing) finishLine();
  if (ev.key === ' ') { ev.preventDefault(); S.paused = !S.paused; }
  if (ev.key === '`' || ev.key === '~') $('#cheat').classList.toggle('hidden');
  if (['1', '2', '3'].includes(ev.key)) S.speed = [1, 2, 4][+ev.key - 1];
});

// ---------------- 노선 ----------------
function startLine() {
  if (view.drawing) return finishLine();
  view.drawing = { nodes: [], color: G.LINE_COLORS[S.lines.length % G.LINE_COLORS.length], loop: false };
  toast('건물을 순서대로 클릭해 노선을 잇는다. 우클릭/Enter로 완성, 시작점을 다시 누르면 순환선.');
  renderLineBar();
}

function finishLine() {
  const d = view.drawing;
  view.drawing = null;
  if (!d || d.nodes.length < 2) { toast('노선에는 건물이 2개 이상 필요하다'); renderLineBar(); return; }
  const line = G.createLine(S);
  line.nodes = d.nodes; line.loop = d.loop; line.color = d.color;
  if (!G.lineHasStable(S, line)) {
    toast('⚠ 이 노선에는 마굿간이 없어 마차가 다니지 않는다. 마굿간을 지어 노선에 포함시켜라.');
  }
  G.syncWagons(S);
  actions.refresh();
}

function cancelLine() { view.drawing = null; renderLineBar(); }

function deleteLine(line) {
  S.lines = S.lines.filter((l) => l !== line);
  G.syncWagons(S);
  actions.refresh();
}

function renderLineBar() {
  const bar = $('#lines');
  bar.innerHTML = '';
  for (const l of S.lines) {
    const chip = el('div', 'lineChip');
    const dot = el('span', 'dot'); dot.style.background = l.color;
    chip.appendChild(dot);
    chip.appendChild(el('span', null, `${l.nodes.length}역${l.loop ? ' · 순환' : ''}${G.lineHasStable(S, l) ? '' : ' · ⚠마굿간없음'}`));
    const x = el('button', 'mini', '✕');
    x.onclick = () => deleteLine(l);
    chip.appendChild(x);
    bar.appendChild(chip);
  }
  if (view.drawing) {
    const chip = el('div', 'lineChip drawing');
    const dot = el('span', 'dot'); dot.style.background = view.drawing.color;
    chip.append(dot, el('span', null, `그리는 중… ${view.drawing.nodes.length}역`));
    bar.appendChild(chip);
  }
}

// ---------------- 툴바 ----------------
$('#newLine').onclick = startLine;
$('#buildStableBtn').onclick = () => { view.buildMode = 'stable'; toast('마굿간을 지을 위치를 클릭하라 (원하는 곳 어디든).'); };
$('#buildWhBtn').onclick = () => { view.buildMode = 'warehouse'; view.buildSize = $('#whSize').value; toast('창고를 지을 위치를 클릭하라.'); };
$('#whSize').onchange = () => { view.buildSize = $('#whSize').value; refreshBuildLabels(); };
$('#pauseBtn').onclick = () => { S.paused = !S.paused; };
$('#speedBtn').onclick = () => { S.speed = S.speed === 1 ? 2 : S.speed === 2 ? 4 : 1; };
$('#cheatBtn').onclick = () => $('#cheat').classList.toggle('hidden');
$('#cheatClose').onclick = () => $('#cheat').classList.add('hidden');
$('#helpBtn').onclick = () => $('#help').classList.toggle('hidden');
$('#helpClose').onclick = () => $('#help').classList.add('hidden');

function refreshBuildLabels() {
  const size = $('#whSize').value;
  const w = B.warehouses[size];
  $('#whCost').textContent = w.coin ? `허가권1 + 🪙${w.coin}` : '허가권1';
}
refreshBuildLabels();

let toastTimer = 0;
function toast(msg) {
  const t = $('#toast');
  t.textContent = msg;
  t.classList.remove('hidden');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => t.classList.add('hidden'), 3200);
}

// ---------------- 루프 ----------------
let last = performance.now();
let uiTimer = 0;
function loop(now) {
  const raw = Math.min(0.05, (now - last) / 1000);
  last = now;
  for (let i = 0; i < S.speed; i++) G.step(S, raw);
  G.syncWagons(S);

  draw(ctx, S, view);
  UI.renderHUD(S);

  uiTimer += raw;
  if (uiTimer > 0.25) {
    uiTimer = 0;
    UI.renderLog(S);
    UI.renderInspector(S, view, actions);
    UI.renderReward(S, actions);
    UI.renderGameOver(S, actions);
    renderLineBar();
  }
  requestAnimationFrame(loop);
}

actions.refresh();
G.say(S, '보급로를 이어라. 요새는 식량·무기·석재가 없으면 버티지 못한다.');
requestAnimationFrame(loop);

// 디버그용 전역 노출
window.LD = { get S() { return S; }, B, G, view };
