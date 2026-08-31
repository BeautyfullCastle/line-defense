import { B, RES, RES_META } from './balance.js';
import * as G from './game.js';

const $ = (s) => document.querySelector(s);
const el = (tag, cls, txt) => { const e = document.createElement(tag); if (cls) e.className = cls; if (txt != null) e.textContent = txt; return e; };

export function renderHUD(S) {
  $('#coins').textContent = Math.floor(S.coins);
  $('#permitW').textContent = S.permits.warehouse;
  $('#permitS').textContent = S.permits.stable;
  const W = S.wave;
  $('#waveNo').textContent = W.n;
  $('#wavePhase').textContent = W.phase === 'prep' ? `준비 ${Math.ceil(W.timer)}s` : `교전중 · 잔여 ${W.queue.length + S.enemies.length}`;
  $('#waveBar').style.width = W.phase === 'prep'
    ? `${100 - (W.timer / B.wave.prepTime) * 100}%`
    : `${100 - (W.queue.length / Math.max(1, B.wave.baseCount + B.wave.countGrow * (W.n - 1))) * 100}%`;
  $('#speedLabel').textContent = `x${S.speed}`;
  $('#pauseBtn').textContent = S.paused ? '▶ 재개' : '⏸ 일시정지';
}

export function renderLog(S) {
  const box = $('#log');
  box.innerHTML = '';
  for (const l of S.log.slice(0, 8)) {
    const d = el('div', 'logline', l.msg);
    box.appendChild(d);
  }
}

// ---------------- 인스펙터 ----------------
export function renderInspector(S, view, actions) {
  const box = $('#inspector');
  box.innerHTML = '';
  const n = S.nodes.find((x) => x.id === view.selected);
  if (!n) { box.appendChild(el('div', 'muted', '건물을 클릭하면 상세 정보가 나온다.')); return; }

  const title = { fortress: '🏰 요새', warehouse: '📦 창고', stable: '🐴 마굿간' }[n.type]
    || `${RES_META[n.res].icon} ${B.producers[n.type].name}`;
  box.appendChild(el('h3', null, title));

  if (n.type === 'fortress') {
    const fac = G.fortressFactors(n);
    box.appendChild(row('성벽', `${Math.round(n.hp)} / ${n.maxHp}`));
    for (const r of RES) {
      box.appendChild(row(`${RES_META[r].icon} ${RES_META[r].name}`,
        `${n.store[r].toFixed(1)} / ${B.fortress.caps[r]}`));
    }
    box.appendChild(row('공격 속도', fac.food <= 0 ? '정지 (식량 0)' :
      `${(B.fortress.interval / fac.food).toFixed(2)}s / 회 (계수 ${(fac.food * 100).toFixed(0)}%)`));
    box.appendChild(row('공격력', fac.weapon <= 0 ? '정지 (무기 0)' :
      `${(B.fortress.atk * fac.weapon).toFixed(1)} (계수 ${(fac.weapon * 100).toFixed(0)}%)`));
    box.appendChild(el('div', 'muted', '석재는 성벽이 손상되면 자동 소모되어 1개당 ' + B.fortress.stoneHeal + ' 회복한다.'));
  }

  if (B.producers[n.type]) {
    const p = B.producers[n.type];
    box.appendChild(row('생산품', `${RES_META[p.res].icon} ${RES_META[p.res].name}`));
    box.appendChild(row('생산 속도', `${p.interval}s 당 1`));
    box.appendChild(row('재고', `${n.store[p.res]} / ${p.cap}`));
    if (n.store[p.res] >= p.cap) box.appendChild(el('div', 'warn', '⚠ 가득 참 — 생산이 멈췄다. 마차를 보내라.'));
  }

  if (n.type === 'warehouse') {
    box.appendChild(row('등급', B.warehouses[n.size].name));
    box.appendChild(row('최대 적재량', G.warehouseCap(n)));
    box.appendChild(row('상하차 속도', `${B.warehouses[n.size].rate}s / 물류 1`));
    for (const r of RES) {
      const line = el('div', 'kv');
      line.appendChild(el('span', null, `${RES_META[r].icon} ${RES_META[r].name}`));
      const right = el('span', 'right');
      right.appendChild(el('b', null, String(Math.floor(n.store[r]))));
      const sell = el('button', 'mini', `판매 +${Math.floor(n.store[r]) * B.economy.sellPrice}`);
      sell.disabled = n.store[r] < 1;
      sell.onclick = () => { G.sellFrom(S, n, r); actions.refresh(); };
      right.appendChild(sell);
      line.appendChild(right);
      box.appendChild(line);
    }
    for (const size of ['mid', 'large']) {
      if (['small', 'mid', 'large'].indexOf(size) <= ['small', 'mid', 'large'].indexOf(n.size)) continue;
      const b = el('button', 'wide', `${B.warehouses[size].name}로 확장 — 🪙${B.warehouseUpgrade[size]}`);
      b.onclick = () => { const r = G.upgradeWarehouse(S, n, size); if (!r.ok) actions.toast(r.why); actions.refresh(); };
      box.appendChild(b);
    }
  }

  if (n.type === 'stable') box.appendChild(stablePanel(S, n, actions));

  const linesOn = S.lines.filter((l) => l.nodes.includes(n.id));
  if (linesOn.length) {
    const d = el('div', 'kv');
    d.appendChild(el('span', null, '연결된 노선'));
    const right = el('span', 'right');
    for (const l of linesOn) { const dot = el('span', 'dot'); dot.style.background = l.color; right.appendChild(dot); }
    d.appendChild(right); box.appendChild(d);
  }
}

function stablePanel(S, n, actions) {
  const wrap = el('div');
  const cfg = n.pendingCfg || { wagonType: n.wagonType, slots: { ...n.slots } };

  const typeRow = el('div', 'chips');
  for (const [key, w] of Object.entries(B.wagons)) {
    const owned = w.coin === 0 || n.unlocked?.includes(key);
    const c = el('button', 'chip' + (cfg.wagonType === key ? ' on' : '') + (owned ? '' : ' locked'),
      `${w.name} (권장 ${w.rec}칸 / 속도 ${w.speed})`);
    c.onclick = () => {
      if (!owned) {
        const cost = w.coin;
        if (S.coins < cost && !S.cheats.freeBuild) return actions.toast(`코인 ${cost} 필요`);
        if (!S.cheats.freeBuild) S.coins -= cost;
        n.unlocked = (n.unlocked || []).concat([key]);
        G.say(S, `🛠 ${w.name}로 업그레이드했다.`);
      }
      cfg.wagonType = key;
      cfg.slots = clampSlots(cfg.slots, key);
      n.pendingCfg = cfg; actions.refresh();
    };
    typeRow.appendChild(c);
  }
  wrap.appendChild(el('div', 'sub', '마차 종류'));
  wrap.appendChild(typeRow);

  const max = G.wagonMaxSlots(cfg.wagonType);
  const rec = B.wagons[cfg.wagonType].rec;
  const total = G.slotTotal(cfg.slots);
  wrap.appendChild(el('div', 'sub', `짐칸 편성 — ${total} / ${max}칸 (권장 ${rec})`));
  wrap.appendChild(el('div', 'muted', '짐칸 부착에는 재화가 들지 않는다. 권장 초과분은 속도로 지불한다.'));

  for (const r of RES) {
    const line = el('div', 'kv');
    line.appendChild(el('span', null, `${RES_META[r].icon} ${RES_META[r].name}칸`));
    const right = el('span', 'right');
    const minus = el('button', 'mini', '−');
    minus.onclick = () => { cfg.slots[r] = Math.max(0, cfg.slots[r] - 1); n.pendingCfg = cfg; actions.refresh(); };
    const val = el('b', null, String(cfg.slots[r]));
    const plus = el('button', 'mini', '+');
    plus.disabled = total >= max;
    plus.onclick = () => { if (G.slotTotal(cfg.slots) < max) cfg.slots[r]++; n.pendingCfg = cfg; actions.refresh(); };
    right.append(minus, val, plus);
    line.appendChild(right);
    wrap.appendChild(line);
  }
  const spd = G.wagonSpeed(cfg.wagonType, total);
  wrap.appendChild(row('예상 이동 속도', `${spd.toFixed(2)} ${total > rec ? `(권장 초과 −${(B.wagons[cfg.wagonType].speed - spd).toFixed(2)})` : ''}`));
  if (n.pendingCfg) wrap.appendChild(el('div', 'warn', '⏳ 변경된 편성은 마차가 이 마굿간을 지나갈 때 적용된다.'));
  return wrap;
}

function clampSlots(slots, type) {
  const max = G.wagonMaxSlots(type);
  const out = { ...slots };
  while (G.slotTotal(out) > max) {
    const r = RES.slice().sort((a, b) => out[b] - out[a])[0];
    out[r]--;
  }
  return out;
}

function row(k, v) {
  const d = el('div', 'kv');
  d.appendChild(el('span', null, k));
  d.appendChild(el('b', 'right', String(v)));
  return d;
}

// ---------------- 보상 모달 ----------------
export function renderReward(S, actions) {
  const m = $('#modal');
  const r = S.pendingReward;
  if (!r) { m.classList.add('hidden'); return; }
  m.classList.remove('hidden');
  m.innerHTML = '';
  const card = el('div', 'card');
  card.appendChild(el('h2', null, r.title));
  const opts = el('div', 'opts');
  for (const o of r.options) {
    const b = el('button', 'opt');
    b.appendChild(el('div', 'optLabel', o.label));
    b.appendChild(el('div', 'optDesc', o.desc));
    b.onclick = () => { G.takeReward(S, o.id); actions.refresh(); };
    opts.appendChild(b);
  }
  card.appendChild(opts);
  m.appendChild(card);
}

export function renderGameOver(S, actions) {
  const m = $('#modal');
  if (!S.gameOver) return;
  m.classList.remove('hidden');
  m.innerHTML = '';
  const card = el('div', 'card');
  card.appendChild(el('h2', null, '💀 방어선이 무너졌다'));
  card.appendChild(el('p', null, `도달 웨이브 ${S.wave.n} · 처치 ${S.stats.kills} · 보급 ${Math.round(S.stats.delivered)} · 누적 코인 ${Math.round(S.stats.coinEarned)}`));
  const b = el('button', 'opt', '다시 시작');
  b.onclick = actions.restart;
  card.appendChild(b);
  m.appendChild(card);
}

export { $, el };
