import { B, RES, stockFactor, wagonMaxSlots, wagonSpeed } from './balance.js';

// ---------------- 유틸 ----------------
let _id = 1;
export const uid = () => _id++;
export const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
export const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
export const rnd = (a, b) => a + Math.random() * (b - a);
export const pick = (arr) => arr[(Math.random() * arr.length) | 0];

export const LINE_COLORS = ['#e0564f', '#4f8fe0', '#4fbf7a', '#d9a441', '#a267d4', '#3fb8b0'];

// ---------------- 상태 ----------------
export function newState() {
  const S = {
    t: 0,
    coins: B.economy.startCoin,
    permits: { warehouse: B.economy.startWarehousePermit, stable: B.economy.startStablePermit },
    nodes: [], lines: [], wagons: [], enemies: [], floats: [],
    // 최초 전선은 침공이 예고된 뒤 물류망을 처음부터 깔아야 하므로 준비 시간을 더 준다.
    wave: { n: 0, phase: 'prep', timer: B.wave.prepTime + B.fortress.buildLeadTime, queue: [], spawnTimer: 0 },
    pendingReward: null,
    gameOver: false, victoryWave: 0,
    speed: 1, paused: false,
    log: [],
    cheats: { godFortress: false, freeBuild: false, noSpawn: false, revealPaths: false },
    stats: { kills: 0, delivered: 0, coinEarned: 0 },
  };
  seedMap(S);
  return S;
}

export function say(S, msg) {
  S.log.unshift({ t: S.t, msg });
  if (S.log.length > 40) S.log.pop();
}

// ---------------- 맵 기하 (원형 방어선) ----------------
export const mapCenter = () => ({ x: B.map.cx, y: B.map.cy });

// 방어선 중심으로부터의 거리
export function distFromCenter(p) { return dist(p, mapCenter()); }

// 물류 건물이 들어갈 수 있는 최대 반경 (방어선 안쪽)
export function buildRadius() { return B.map.radius - B.map.innerMargin; }

// 각도(도) → 방어선 위의 좌표
export function onPerimeter(deg, radius = B.map.radius) {
  const r = (deg * Math.PI) / 180;
  return { x: B.map.cx + Math.cos(r) * radius, y: B.map.cy + Math.sin(r) * radius };
}

// ---------------- 맵 생성 ----------------
// 생산지는 방어선 '안쪽'에만 생성된다.
function insideField(p) {
  const m = B.map.canvasMargin;
  return distFromCenter(p) <= buildRadius()
    && p.x >= m && p.x <= B.map.w - m && p.y >= m && p.y <= B.map.h - m;
}

// 생산지는 방어선 안쪽이면서 화면 안에만 생성된다.
function freeSpot(S) {
  const m = B.map.canvasMargin;
  let best = null;
  for (let i = 0; i < 1200; i++) {
    const p = { x: rnd(m, B.map.w - m), y: rnd(m, B.map.h - m) };
    if (!insideField(p)) continue;
    if (S.nodes.every((n) => dist(n, p) > B.map.minNodeDist * 1.8)) return p;
    if (!best) best = p;
  }
  return best || { x: B.map.cx, y: B.map.h - 120 };
}

export function makeNode(S, type, x, y, extra = {}) {
  const n = {
    id: uid(), type, x, y,
    store: { food: 0, weapon: 0, stone: 0 },
    lines: [],
    ...extra,
  };
  if (B.producers[type]) {
    n.res = B.producers[type].res;
    n.timer = 0;
    // 생산지는 침공 이전부터 가동 중이었다고 본다. 재고를 가득 채운 상태로 등장시켜야
    // 첫 웨이브 준비 시간(30초, 전체 생산량 12개) 안에 요새를 채울 여지가 생긴다.
    n.store[n.res] = B.producers[type].cap;
  }
  S.nodes.push(n);
  return n;
}

function seedMap(S) {
  for (const [type, count] of B.map.startProducers) {
    for (let i = 0; i < count; i++) {
      const p = freeSpot(S);
      makeNode(S, type, p.x, p.y);
    }
  }
  spawnFortress(S, false);
}

// 적 침공 루트가 정해지면, 그 루트와 방어선이 만나는 지점에 요새가 "자동" 생성된다.
export function spawnFortress(S, withGarrison = true) {
  const used = S.nodes.filter((n) => n.type === 'fortress').length;
  const angle = B.map.fortressAngles[used % B.map.fortressAngles.length];
  const pos = onPerimeter(angle);
  const f = makeNode(S, 'fortress', pos.x, pos.y, {
    hp: B.fortress.hp, maxHp: B.fortress.hp,
    // 최초 요새는 기획대로 자원 0에서 시작한다. 게임 도중 열리는 새 전선만
    // 수비대가 최소 보급을 들고 부임한다(그렇지 않으면 확장이 불가능하다).
    store: withGarrison ? {
      food: B.fortress.caps.food * B.fortress.startStockRatio,
      weapon: B.fortress.caps.weapon * B.fortress.startStockRatio,
      stone: B.fortress.caps.stone * B.fortress.startStockRatio,
    } : { food: 0, weapon: 0, stone: 0 },
    atkTimer: 0, inCombat: 0,
    angle,                                    // 침공 루트의 방향
    spawn: onPerimeter(angle, B.map.radius + B.map.enemyRingGap),
  });
  // 새 전선 뒤쪽(안쪽)에 생산지 한 세트를 함께 배치한다.
  if (withGarrison) {
    for (const type of B.map.frontProducers) spawnProducerNear(S, f);
  }
  say(S, `⚑ 새로운 전선! 요새가 생성되었다. 보급로를 이어라.`);
  return f;
}

// 요새 안쪽(중심 방향)의 빈 자리에 생산지를 하나 만든다.
function spawnProducerNear(S, f) {
  const type = pick(B.map.frontProducers);
  const inward = Math.atan2(B.map.cy - f.y, B.map.cx - f.x);
  for (let i = 0; i < 300; i++) {
    const a = inward + rnd(-1.0, 1.0);
    const r = rnd(90, B.map.frontProducerSpread);
    const p = { x: f.x + Math.cos(a) * r, y: f.y + Math.sin(a) * r };
    if (!insideField(p)) continue;
    if (S.nodes.some((n) => dist(n, p) < B.map.minNodeDist * 1.5)) continue;
    return makeNode(S, type, p.x, p.y);
  }
  const p = freeSpot(S);
  return makeNode(S, type, p.x, p.y);
}

// ---------------- 건설 ----------------
export function buildCost(S, kind, size = 'small') {
  if (S.cheats.freeBuild) return { permit: 0, coin: 0, key: kind === 'stable' ? 'stable' : 'warehouse' };
  if (kind === 'stable') return { permit: B.stable.permit, coin: B.stable.coin, key: 'stable' };
  const w = B.warehouses[size];
  return { permit: w.permit, coin: w.coin, key: 'warehouse' };
}

export function canBuild(S, kind, size, x, y) {
  const c = buildCost(S, kind, size);
  if (S.permits[c.key] < c.permit) return { ok: false, why: `${c.key === 'stable' ? '마굿간' : '창고'} 건설 허가권이 부족하다` };
  if (S.coins < c.coin) return { ok: false, why: `코인이 부족하다 (${c.coin} 필요)` };
  if (!insideField({ x, y })) return { ok: false, why: '방어선 안쪽에만 지을 수 있다' };
  if (S.nodes.some((n) => dist(n, { x, y }) < B.map.minNodeDist)) return { ok: false, why: '다른 건물과 너무 가깝다' };
  return { ok: true, cost: c };
}

export function build(S, kind, size, x, y) {
  const chk = canBuild(S, kind, size, x, y);
  if (!chk.ok) return chk;
  S.permits[chk.cost.key] -= chk.cost.permit;
  S.coins -= chk.cost.coin;
  const node = kind === 'stable'
    ? makeNode(S, 'stable', x, y, { wagonType: 'basic', slots: defaultSlots('basic'), pendingCfg: null })
    : makeNode(S, 'warehouse', x, y, { size });
  say(S, kind === 'stable' ? '🐴 마굿간을 세웠다.' : `📦 ${B.warehouses[size].name}를 세웠다.`);
  return { ok: true, node };
}

export function upgradeWarehouse(S, node, size) {
  const order = ['small', 'mid', 'large'];
  if (order.indexOf(size) <= order.indexOf(node.size)) return { ok: false, why: '이미 그 이상이다' };
  const coin = S.cheats.freeBuild ? 0 : B.warehouseUpgrade[size];
  if (S.coins < coin) return { ok: false, why: `코인 ${coin} 필요` };
  S.coins -= coin;
  node.size = size;
  say(S, `📦 창고를 ${B.warehouses[size].name}로 확장했다.`);
  return { ok: true };
}

export function warehouseCap(node) { return B.warehouses[node.size].cap; }

export function nodeRate(node) {
  if (node.type === 'warehouse') return B.warehouses[node.size].rate;
  if (node.type === 'stable') return B.stable.rate;
  if (B.producers[node.type]) return B.producers[node.type].loadRate;
  return 0.3;
}

export function nodeCap(node, res) {
  if (node.type === 'fortress') return B.fortress.caps[res];
  if (node.type === 'warehouse') return warehouseCap(node);
  if (B.producers[node.type]) return node.res === res ? B.producers[node.type].cap : 0;
  return 0;
}

export function defaultSlots(type) {
  const rec = B.wagons[type].rec;
  const each = Math.floor(rec / 3);
  const s = { food: each, weapon: each, stone: rec - each * 2 };
  return s;
}

export function slotTotal(slots) { return RES.reduce((a, r) => a + slots[r], 0); }

// ---------------- 노선 ----------------
export function createLine(S) {
  const used = S.lines.map((l) => l.color);
  const color = LINE_COLORS.find((c) => !used.includes(c)) || pick(LINE_COLORS);
  const line = { id: uid(), color, nodes: [], loop: false, wagonCount: 1 };
  S.lines.push(line);
  return line;
}

export function lineHasStable(S, line) {
  return line.nodes.some((id) => nodeById(S, id)?.type === 'stable');
}

export function nodeById(S, id) { return S.nodes.find((n) => n.id === id); }

export function linePoints(S, line) {
  const pts = line.nodes.map((id) => nodeById(S, id)).filter(Boolean);
  return line.loop && pts.length > 2 ? pts.concat([pts[0]]) : pts;
}

// 노선이 유효해지면(마굿간 포함 + 2개 이상 노드) 마차를 자동 편성한다.
export function syncWagons(S) {
  for (const line of S.lines) {
    const valid = line.nodes.length >= 2 && lineHasStable(S, line);
    const have = S.wagons.filter((w) => w.lineId === line.id);
    const want = valid ? (line.wagonCount || 1) : 0;
    if (have.length < want) {
      const stable = line.nodes.map((id) => nodeById(S, id)).find((n) => n?.type === 'stable');
      for (let i = have.length; i < want; i++) {
        const w = makeWagon(line, stable);
        // 노선 위에 고르게 퍼뜨려 배치한다
        const segCount = Math.max(1, linePoints(S, line).length - 1);
        w.seg = Math.floor((segCount * i) / want) % segCount;
        S.wagons.push(w);
      }
      if (have.length === 0) say(S, '🛒 마차가 노선에 배치되었다.');
    } else if (have.length > want) {
      const drop = have.slice(want).map((w) => w.id);
      S.wagons = S.wagons.filter((w) => !drop.includes(w.id));
    }
  }
  // 삭제된 노선 정리
  const ids = new Set(S.lines.map((l) => l.id));
  S.wagons = S.wagons.filter((w) => ids.has(w.lineId));
}

function makeWagon(line, stable) {
  const type = stable?.wagonType || 'basic';
  return {
    id: uid(), lineId: line.id, type,
    slots: { ...(stable?.slots || defaultSlots(type)) },
    cargo: { food: 0, weapon: 0, stone: 0 },
    seg: 0, u: 0, dir: 1, state: 'move', dwell: 0, atNode: null,
    x: stable?.x || 0, y: stable?.y || 0,
  };
}

// ============================================================
// 시뮬레이션
// ============================================================
export function step(S, dt) {
  if (S.gameOver || S.paused || S.pendingReward) return;
  S.t += dt;
  stepProducers(S, dt);
  stepWagons(S, dt);
  stepFortresses(S, dt);
  stepEnemies(S, dt);
  stepWave(S, dt);
  S.floats = S.floats.filter((f) => (f.life -= dt) > 0);
}

function stepProducers(S, dt) {
  for (const n of S.nodes) {
    const p = B.producers[n.type];
    if (!p) continue;
    n.timer += dt;
    while (n.timer >= p.interval) {
      n.timer -= p.interval;
      if (n.store[p.res] < p.cap) n.store[p.res]++;
      else n.timer = 0; // 최대 적재량 도달 시 생산 정지
    }
  }
}

// ---------------- 마차 ----------------
function stepWagons(S, dt) {
  for (const w of S.wagons) {
    const line = S.lines.find((l) => l.id === w.lineId);
    if (!line) continue;
    const pts = linePoints(S, line);
    if (pts.length < 2) continue;

    if (w.state === 'dwell') {
      w.dwell -= dt;
      const n = w.atNode;
      if (n) { w.x = n.x; w.y = n.y; }
      if (w.dwell <= 0) { w.state = 'move'; w.atNode = null; }
      continue;
    }

    const segCount = pts.length - 1;
    w.seg = clamp(w.seg, 0, segCount - 1);
    const a = pts[w.seg], b = pts[w.seg + 1];
    const len = Math.max(1, dist(a, b));
    const spd = wagonSpeed(w.type, slotTotal(w.slots)) * B.pxPerSpeed; // 표의 속도 단위 → px/s
    w.u += (spd * dt) / len * w.dir;

    // 노드 도착 판정. 전진 중 u>=1 이면 pts[seg+1]에, 후진 중 u<=0 이면 pts[seg]에 도착한 것이다.
    if (w.dir > 0 && w.u >= 1) {
      const reached = w.seg + 1;
      if (reached >= segCount) {
        if (line.loop) { w.seg = 0; w.u = 0; }        // 순환선은 계속 돈다
        else { w.seg = segCount - 1; w.u = 1; w.dir = -1; }  // 종점에서 되돌아온다
      } else { w.seg = reached; w.u = 0; }
      arrive(S, w, pts[reached]);
    } else if (w.dir < 0 && w.u <= 0) {
      const reached = w.seg;
      if (reached <= 0) { w.seg = 0; w.u = 0; w.dir = 1; }   // 시점에서 되돌아온다
      else { w.seg = reached - 1; w.u = 1; }
      arrive(S, w, pts[reached]);
    }
    const aa = pts[clamp(w.seg, 0, segCount - 1)], bb = pts[clamp(w.seg + 1, 1, segCount)];
    w.x = aa.x + (bb.x - aa.x) * w.u;
    w.y = aa.y + (bb.y - aa.y) * w.u;
  }
}

function arrive(S, w, node) {
  if (!node) return;
  const moved = transfer(S, w, node);
  w.state = 'dwell';
  w.dwell = Math.max(0.15, moved * nodeRate(node));
  w.atNode = node;
  if (node.type === 'stable' && node.pendingCfg) {
    // 변경된 편성은 마굿간을 지나갈 때 적용된다.
    w.type = node.pendingCfg.wagonType;
    w.slots = { ...node.pendingCfg.slots };
    node.wagonType = w.type; node.slots = { ...w.slots };
    node.pendingCfg = null;
    say(S, '🐴 마굿간에서 마차 편성이 변경되었다.');
  }
}

function capFor(w, res) { return w.slots[res] * B.slotUnit; }

// 상/하차. 반환값 = 옮긴 총 물류량 (상하차 시간 계산용)
function transfer(S, w, node) {
  let moved = 0;
  if (node.type === 'fortress' || node.type === 'warehouse') {
    // 하차: 목적지가 받을 수 있는 만큼 전부 내린다.
    for (const r of RES) {
      const cap = node.type === 'warehouse'
        ? warehouseCap(node) - RES.reduce((a, x) => a + node.store[x], 0)
        : B.fortress.caps[r] - node.store[r];
      const amt = Math.min(w.cargo[r], Math.max(0, cap));
      if (amt > 0) {
        w.cargo[r] -= amt; node.store[r] += amt; moved += amt;
        if (node.type === 'fortress') S.stats.delivered += amt;
      }
    }
  }
  if (B.producers[node.type]) {
    // 생산지: 실을 수 있는 만큼 최대한 싣는다.
    const r = node.res;
    const room = capFor(w, r) - w.cargo[r];
    const amt = Math.min(node.store[r], Math.max(0, room));
    if (amt > 0) { node.store[r] -= amt; w.cargo[r] += amt; moved += amt; }
  }
  if (node.type === 'warehouse') {
    // 창고: 하차 후, 이 노선의 수요만큼만 다시 싣는다.
    const demand = lineDemand(S, w.lineId);
    for (const r of RES) {
      const room = capFor(w, r) - w.cargo[r];
      const amt = Math.min(node.store[r], Math.max(0, room), Math.max(0, demand[r]));
      if (amt > 0) { node.store[r] -= amt; w.cargo[r] += amt; moved += amt; }
    }
  }
  return moved;
}

// 노선 위 요새들이 아직 필요로 하는 자원량
function lineDemand(S, lineId) {
  const line = S.lines.find((l) => l.id === lineId);
  const d = { food: 0, weapon: 0, stone: 0 };
  if (!line) return d;
  for (const id of line.nodes) {
    const n = nodeById(S, id);
    if (n?.type !== 'fortress') continue;
    for (const r of RES) d[r] += Math.max(0, B.fortress.caps[r] - n.store[r]);
  }
  return d;
}

// ---------------- 요새 ----------------
// 강화 레벨에 따른 배수
export function fortressPower(f) {
  const u = B.fortress.upgrade;
  return { dmg: 1 + u.dmgPerLevel * (f.level || 0), spd: 1 + u.spdPerLevel * (f.level || 0) };
}
export function upgradeCost(f) {
  const u = B.fortress.upgrade;
  return Math.round(u.baseCost * Math.pow(u.costGrow, f.level || 0));
}
export function upgradeFortress(S, f) {
  const u = B.fortress.upgrade;
  if ((f.level || 0) >= u.maxLevel) return { ok: false, why: '이미 최대 강화 단계다' };
  const cost = S.cheats.freeBuild ? 0 : upgradeCost(f);
  if (S.coins < cost) return { ok: false, why: `코인 ${cost} 필요` };
  S.coins -= cost;
  f.level = (f.level || 0) + 1;
  say(S, `🔨 요새를 ${f.level}단계로 강화했다.`);
  return { ok: true };
}

export function fortressFactors(f) {
  return {
    food: stockFactor(f.store.food, B.fortress.caps.food),
    weapon: stockFactor(f.store.weapon, B.fortress.caps.weapon),
  };
}

function stepFortresses(S, dt) {
  for (const f of S.nodes) {
    if (f.type !== 'fortress') continue;
    f.inCombat = Math.max(0, f.inCombat - dt);

    // 식량은 전투와 관계없이 상시 소모, 무기는 전투 중에만 소모
    f.store.food = Math.max(0, f.store.food - B.fortress.foodPerSec * dt);
    if (f.inCombat > 0) f.store.weapon = Math.max(0, f.store.weapon - B.fortress.weaponPerCombatSec * dt);

    // 석재로 성벽 자동 수리
    while (f.hp < f.maxHp * B.fortress.healThreshold && f.store.stone >= 1) {
      f.store.stone -= 1;
      f.hp = Math.min(f.maxHp, f.hp + B.fortress.stoneHeal);
      S.floats.push({ x: f.x, y: f.y - 26, text: `+${B.fortress.stoneHeal}`, color: '#a8a29a', life: 1 });
    }

    const fac = fortressFactors(f);
    if (fac.food <= 0 || fac.weapon <= 0) { f.atkTimer = 0; continue; } // 하나라도 0이면 공격 불가

    f.atkTimer -= dt;
    if (f.atkTimer > 0) continue;
    const target = S.enemies
      .filter((e) => dist(e, f) <= B.fortress.range)
      .sort((a, b) => dist(a, f) - dist(b, f))[0];
    if (!target) { f.atkTimer = 0; continue; }

    const pw = fortressPower(f);
    f.atkTimer = B.fortress.interval / (fac.food * pw.spd);   // 식량 부족 → 공격 속도 감소
    const dmg = B.fortress.atk * fac.weapon * pw.dmg;         // 무기 부족 → 공격력 감소
    target.hp -= dmg;
    f.inCombat = 1.2;
    f.shot = { x: target.x, y: target.y, life: 0.12 };
    if (target.hp <= 0) killEnemy(S, target);
  }
}

function killEnemy(S, e) {
  S.enemies = S.enemies.filter((x) => x !== e);
  S.stats.kills++;
  const c = Math.max(1, Math.round(B.monsters[e.type].coin * B.wave.killCoinMult));
  S.coins += c; S.stats.coinEarned += c;
  S.floats.push({ x: e.x, y: e.y, text: `+${c}`, color: '#f0c96a', life: 0.8 });
}

// ---------------- 적 ----------------
function stepEnemies(S, dt) {
  for (const e of S.enemies) {
    const f = nodeById(S, e.target);
    if (!f || f.type !== 'fortress') { e.dead = true; continue; }
    const d = dist(e, f);
    if (d > B.monsters[e.type].range) {
      const m = B.monsters[e.type];
      e.x += ((f.x - e.x) / d) * m.speed * dt;
      e.y += ((f.y - e.y) / d) * m.speed * dt;
    } else {
      e.atkTimer -= dt;
      if (e.atkTimer <= 0) {
        e.atkTimer = B.monsters[e.type].interval;
        if (!S.cheats.godFortress) f.hp -= e.atk;
        f.inCombat = 1.2;
        if (f.hp <= 0) { f.hp = 0; S.gameOver = true; say(S, '💀 요새가 함락되었다. 게임 오버.'); }
      }
    }
  }
  S.enemies = S.enemies.filter((e) => !e.dead);
}

// ---------------- 웨이브 ----------------
function stepWave(S, dt) {
  const W = S.wave;
  if (W.phase === 'prep') {
    W.timer -= dt;
    if (W.timer <= 0) startWave(S);
    return;
  }
  // combat
  W.spawnTimer -= dt;
  while (W.queue.length && W.spawnTimer <= 0 && !S.cheats.noSpawn) {
    W.spawnTimer += W.gap;
    spawnEnemy(S, W.queue.pop());
  }
  if (!W.queue.length && S.enemies.length === 0) clearWave(S);
}

function startWave(S) {
  const W = S.wave;
  W.n++;
  const forts = S.nodes.filter((n) => n.type === 'fortress');
  const total = B.wave.baseCount + B.wave.countGrow * (W.n - 1);
  W.queue = [];
  for (let i = 0; i < total; i++) {
    let type = 'basicA';
    if (W.n >= 3 && i % 5 === 0) type = 'runner';
    if (W.n >= 5 && i % 9 === 0) type = 'brute';
    W.queue.push({ type, fort: forts[i % forts.length]?.id });
  }
  W.queue.sort(() => Math.random() - 0.5);
  W.gap = B.wave.duration / Math.max(1, total);
  W.spawnTimer = 0;
  W.phase = 'combat';
  say(S, `⚔️ 웨이브 ${W.n} 시작! 몬스터 ${total}마리.`);
}

function spawnEnemy(S, spec) {
  const f = nodeById(S, spec.fort) || S.nodes.find((n) => n.type === 'fortress');
  if (!f) return;
  const m = B.monsters[spec.type];
  const g = S.wave.n - 1;
  // 요새의 각도를 중심으로 부채꼴로 퍼져서, 방어선 바깥에서 등장한다.
  const spread = rnd(-16, 16);
  const ring = B.map.radius + B.map.enemyRingGap + rnd(-45, 45);
  const p = onPerimeter((f.angle ?? 0) + spread, ring);
  S.enemies.push({
    type: spec.type, target: f.id,
    x: p.x, y: p.y,
    hp: m.hp * Math.pow(1 + B.wave.hpGrow, g),
    maxHp: m.hp * Math.pow(1 + B.wave.hpGrow, g),
    atk: m.atk * Math.pow(1 + B.wave.atkGrow, g),
    atkTimer: 0,
  });
}

function clearWave(S) {
  const W = S.wave;
  const forts = S.nodes.filter((n) => n.type === 'fortress');
  const hpRatio = forts.reduce((a, f) => a + f.hp / f.maxHp, 0) / Math.max(1, forts.length);
  const coin = B.wave.clearCoin + B.wave.clearCoinPerWave * W.n + Math.round(B.wave.hpBonusCoin * hpRatio);
  S.coins += coin; S.stats.coinEarned += coin;
  say(S, `✅ 웨이브 ${W.n} 방어 성공! 코인 +${coin} (성벽 보너스 ${Math.round(hpRatio * 100)}%)`);

  // 보상 선택지
  if (B.wave.permitWaves.includes(W.n)) {
    S.pendingReward = rewardChoices(S);
  }
  // 다음 웨이브 준비: 전선/생산지 확장
  let openedFront = false;
  if (W.n % B.wave.newFortressEvery === 0 && forts.length < B.wave.maxFortress) {
    spawnFortress(S); openedFront = true;
  }
  if (W.n % B.map.newProducerEvery === 0) {
    const type = pick(Object.keys(B.producers));
    const p = freeSpot(S);
    makeNode(S, type, p.x, p.y);
    say(S, `🏗️ 새로운 ${B.producers[type].name}이(가) 발견되었다.`);
  }
  W.phase = 'prep';
  // 새 전선이 열렸다면 보급선을 새로 깔 시간을 더 준다
  W.timer = B.wave.prepTime + (openedFront ? B.fortress.buildLeadTime : 0);
}

function rewardChoices(S) {
  const coin = 60 + 20 * S.wave.n;
  return {
    title: `웨이브 ${S.wave.n} 보상 — 하나를 선택하라`,
    options: [
      { id: 'wp', label: '📦 창고 건설 허가권 x1', desc: '아무 곳에나 소형 창고를 지을 수 있다.' },
      { id: 'sp', label: '🐴 마굿간 허가권 x1', desc: '새 노선을 굴릴 마굿간을 지을 수 있다.' },
      { id: 'coin', label: `🪙 코인 +${coin}`, desc: '창고 확장·마차 업그레이드에 쓴다.' },
    ],
    coin,
  };
}

export function takeReward(S, id) {
  const r = S.pendingReward;
  if (!r) return;
  if (id === 'wp') { S.permits.warehouse++; say(S, '📦 창고 건설 허가권을 얻었다.'); }
  if (id === 'sp') { S.permits.stable++; say(S, '🐴 마굿간 허가권을 얻었다.'); }
  if (id === 'coin') { S.coins += r.coin; S.stats.coinEarned += r.coin; say(S, `🪙 코인 +${r.coin}`); }
  S.pendingReward = null;
}

// 노선에 마차를 추가 투입한다 (코인 소모)
export function extraWagonCost(line) {
  return B.economy.extraWagonBase * (line.wagonCount || 1);
}
export function addWagon(S, line) {
  if ((line.wagonCount || 1) >= B.economy.maxWagonsPerLine) return { ok: false, why: '이 노선의 마차 수가 한계다' };
  const cost = S.cheats.freeBuild ? 0 : extraWagonCost(line);
  if (S.coins < cost) return { ok: false, why: `코인 ${cost} 필요` };
  S.coins -= cost;
  line.wagonCount = (line.wagonCount || 1) + 1;
  syncWagons(S);
  say(S, `🛒 마차를 1대 더 투입했다 (총 ${line.wagonCount}대).`);
  return { ok: true };
}

// 창고 잉여 자원 판매 → 코인
export function sellFrom(S, node, res) {
  const amt = Math.floor(node.store[res]);
  if (amt <= 0) return;
  node.store[res] -= amt;
  const c = amt * B.economy.sellPrice;
  S.coins += c; S.stats.coinEarned += c;
  say(S, `🪙 ${amt}개를 팔아 코인 ${c}을 얻었다.`);
}

export { wagonMaxSlots, wagonSpeed };
