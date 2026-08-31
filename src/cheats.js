import { B, RES } from './balance.js';
import * as G from './game.js';
import { $, el } from './ui.js';

// 치트 메뉴: ` (백틱) 또는 우측 상단 버튼으로 토글
export function buildCheatMenu(S, actions) {
  const box = $('#cheatBody');
  box.innerHTML = '';

  section(box, '재화', [
    btn('🪙 코인 +100', () => { S.coins += 100; }),
    btn('🪙 코인 +1000', () => { S.coins += 1000; }),
    btn('📦 창고 허가권 +1', () => { S.permits.warehouse++; }),
    btn('🐴 마굿간 허가권 +1', () => { S.permits.stable++; }),
  ], actions);

  section(box, '요새', [
    btn('모든 요새 자원 만땅', () => {
      for (const f of S.nodes) if (f.type === 'fortress') for (const r of RES) f.store[r] = B.fortress.caps[r];
    }),
    btn('모든 요새 성벽 전체 회복', () => {
      for (const f of S.nodes) if (f.type === 'fortress') f.hp = f.maxHp;
    }),
    toggle('무적 요새', 'godFortress', S),
    btn('요새 1개 추가 생성', () => G.spawnFortress(S)),
  ], actions);

  section(box, '웨이브 / 적', [
    btn('☠ 화면의 적 전멸', () => { S.enemies.forEach((e) => (e.dead = true)); }),
    btn('⏭ 준비시간 즉시 종료', () => { if (S.wave.phase === 'prep') S.wave.timer = 0.01; }),
    btn('⏩ 현재 웨이브 즉시 클리어', () => { S.wave.queue = []; S.enemies.forEach((e) => (e.dead = true)); }),
    toggle('적 스폰 중지', 'noSpawn', S),
  ], actions);

  section(box, '물류', [
    btn('모든 생산지 재고 가득', () => {
      for (const n of S.nodes) { const p = B.producers[n.type]; if (p) n.store[p.res] = p.cap; }
    }),
    btn('모든 창고 자원 +20씩', () => {
      for (const n of S.nodes) if (n.type === 'warehouse') for (const r of RES) n.store[r] += 20;
    }),
    toggle('무료 건설 (허가권·코인 0)', 'freeBuild', S),
  ], actions);

  // 밸런스 실시간 편집
  const tune = el('div', 'cheatSection');
  tune.appendChild(el('h4', null, '밸런스 실시간 조정'));
  const rows = [
    ['요새 공격력', () => B.fortress.atk, (v) => (B.fortress.atk = v), 1, 200],
    ['요새 공격 간격(s)', () => B.fortress.interval, (v) => (B.fortress.interval = v), 0.1, 5, 0.1],
    ['요새 사거리', () => B.fortress.range, (v) => (B.fortress.range = v), 60, 500],
    ['식량 소모(1당 초)', () => 1 / B.fortress.foodPerSec, (v) => (B.fortress.foodPerSec = 1 / v), 1, 60],
    ['웨이브 준비시간(s)', () => B.wave.prepTime, (v) => (B.wave.prepTime = v), 5, 120],
    ['웨이브 기본 마릿수', () => B.wave.baseCount, (v) => (B.wave.baseCount = v), 1, 200],
    ['몬스터 체력 성장률', () => B.wave.hpGrow, (v) => (B.wave.hpGrow = v), 0, 0.5, 0.01],
    ['재고 계수 safe (식량·무기 공통)', () => B.fortress.stockCurve.safe, (v) => (B.fortress.stockCurve.safe = v), 0.05, 1, 0.05],
    ['성능 하한 floor', () => B.fortress.stockCurve.floor, (v) => (B.fortress.stockCurve.floor = v), 0, 1, 0.05],
  ];
  for (const [label, get, set, min, max, stepv] of rows) {
    const line = el('div', 'kv');
    line.appendChild(el('span', null, label));
    const right = el('span', 'right');
    const num = el('input');
    num.type = 'number'; num.step = stepv || 1; num.min = min; num.max = max;
    num.value = String(Math.round(get() * 1000) / 1000);
    num.oninput = () => { const v = parseFloat(num.value); if (!isNaN(v)) set(Math.max(min, Math.min(max, v))); };
    num.className = 'num';
    right.appendChild(num);
    line.appendChild(right);
    tune.appendChild(line);
  }
  box.appendChild(tune);

  const foot = el('div', 'cheatSection');
  foot.appendChild(el('div', 'muted', '치트 값은 저장되지 않는다. 새로고침하면 밸런스 표 기본값으로 돌아간다.'));
  box.appendChild(foot);
}

function section(parent, title, buttons, actions) {
  const s = el('div', 'cheatSection');
  s.appendChild(el('h4', null, title));
  for (const b of buttons) { b.addEventListener('click', () => actions.refresh()); s.appendChild(b); }
  parent.appendChild(s);
}

function btn(label, fn) {
  const b = el('button', 'wide', label);
  b.addEventListener('click', fn);
  return b;
}

function toggle(label, key, S) {
  const b = el('button', 'wide' + (S.cheats[key] ? ' on' : ''), `${S.cheats[key] ? '☑' : '☐'} ${label}`);
  b.addEventListener('click', () => {
    S.cheats[key] = !S.cheats[key];
    b.textContent = `${S.cheats[key] ? '☑' : '☐'} ${label}`;
    b.classList.toggle('on', S.cheats[key]);
  });
  return b;
}
