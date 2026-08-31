import { B, RES, RES_META } from './balance.js';
import { linePoints, nodeById, warehouseCap, fortressFactors, slotTotal, wagonSpeed } from './game.js';

const NODE_R = 20;

export function draw(ctx, S, view) {
  const { w, h } = { w: B.map.w, h: B.map.h };
  ctx.clearRect(0, 0, w, h);

  // 배경
  ctx.fillStyle = '#12151b'; ctx.fillRect(0, 0, w, h);
  ctx.strokeStyle = 'rgba(255,255,255,0.035)'; ctx.lineWidth = 1;
  for (let x = 0; x < w; x += 40) { ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, h); ctx.stroke(); }
  for (let y = 0; y < h; y += 40) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(w, y); ctx.stroke(); }

  drawLines(ctx, S, view);
  drawEnemies(ctx, S);
  drawNodes(ctx, S, view);
  drawWagons(ctx, S);
  drawFloats(ctx, S);
  drawGhost(ctx, S, view);
}

function drawLines(ctx, S, view) {
  for (const line of S.lines) {
    const pts = linePoints(S, line);
    if (pts.length < 2) continue;
    ctx.strokeStyle = line.color;
    ctx.lineWidth = 7; ctx.lineCap = 'round'; ctx.lineJoin = 'round';
    ctx.globalAlpha = 0.85;
    ctx.beginPath();
    ctx.moveTo(pts[0].x, pts[0].y);
    for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i].x, pts[i].y);
    ctx.stroke();
    ctx.globalAlpha = 1;
  }
  // 그리는 중인 노선의 미리보기
  if (view.drawing && view.drawing.nodes.length) {
    const pts = view.drawing.nodes.map((id) => nodeById(S, id)).filter(Boolean);
    ctx.strokeStyle = view.drawing.color; ctx.lineWidth = 7; ctx.setLineDash([10, 8]);
    ctx.beginPath(); ctx.moveTo(pts[0].x, pts[0].y);
    for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i].x, pts[i].y);
    ctx.lineTo(view.mouse.x, view.mouse.y);
    ctx.stroke(); ctx.setLineDash([]);
  }
}

function roundRect(ctx, x, y, w, h, r) {
  ctx.beginPath(); ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r); ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r); ctx.arcTo(x, y, x + w, y, r); ctx.closePath();
}

function drawNodes(ctx, S, view) {
  for (const n of S.nodes) {
    const hovered = view.hover === n.id;
    const selected = view.selected === n.id;
    ctx.save();
    ctx.translate(n.x, n.y);

    if (n.type === 'fortress') drawFortress(ctx, S, n);
    else if (n.type === 'warehouse') {
      ctx.fillStyle = '#2b3242'; ctx.strokeStyle = '#7f8ca6'; ctx.lineWidth = 3;
      const r = n.size === 'large' ? 26 : n.size === 'mid' ? 22 : 18;
      roundRect(ctx, -r, -r, r * 2, r * 2, 6); ctx.fill(); ctx.stroke();
      label(ctx, '📦', 0, 6, 20);
      storeBar(ctx, n, r);
    } else if (n.type === 'stable') {
      // 헛간 모양(지붕 + 몸통) — 경고 삼각형으로 오인되지 않도록
      ctx.fillStyle = '#3a2f24'; ctx.strokeStyle = '#c9a26b'; ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(-20, -4); ctx.lineTo(0, -20); ctx.lineTo(20, -4);
      ctx.lineTo(20, 18); ctx.lineTo(-20, 18); ctx.closePath();
      ctx.fill(); ctx.stroke();
      label(ctx, '🐴', 0, 12, 15);
    } else {
      const p = B.producers[n.type];
      ctx.fillStyle = '#1c2330'; ctx.strokeStyle = p.color; ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(0, 0, NODE_R, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
      label(ctx, RES_META[p.res].icon, 0, 6, 18);
      // 적재량 표시
      ctx.fillStyle = p.color; ctx.font = 'bold 11px system-ui';
      ctx.textAlign = 'center';
      ctx.fillText(`${n.store[p.res]}/${p.cap}`, 0, NODE_R + 15);
    }

    if (hovered || selected) {
      ctx.strokeStyle = selected ? '#fff' : 'rgba(255,255,255,0.5)';
      ctx.lineWidth = 2; ctx.setLineDash([4, 4]);
      ctx.beginPath(); ctx.arc(0, 0, 34, 0, Math.PI * 2); ctx.stroke(); ctx.setLineDash([]);
    }
    ctx.restore();
  }
}

function drawFortress(ctx, S, f) {
  const factors = fortressFactors(f);
  const danger = factors.food <= 0 || factors.weapon <= 0;
  ctx.fillStyle = danger ? '#3b2020' : '#232b38';
  ctx.strokeStyle = danger ? '#e0564f' : (f.inCombat > 0 ? '#ffb347' : '#9fb4d6');
  ctx.lineWidth = 4;
  roundRect(ctx, -26, -26, 52, 52, 8); ctx.fill(); ctx.stroke();
  label(ctx, '🏰', 0, 8, 24);

  // 사거리
  ctx.strokeStyle = 'rgba(159,180,214,0.13)'; ctx.lineWidth = 1;
  ctx.beginPath(); ctx.arc(0, 0, B.fortress.range, 0, Math.PI * 2); ctx.stroke();

  // 체력바
  const bw = 66;
  ctx.fillStyle = '#000a'; roundRect(ctx, -bw / 2, -42, bw, 7, 3); ctx.fill();
  ctx.fillStyle = f.hp / f.maxHp > 0.4 ? '#5fd18a' : '#e0564f';
  roundRect(ctx, -bw / 2, -42, bw * Math.max(0, f.hp / f.maxHp), 7, 3); ctx.fill();

  // 자원바 3종
  RES.forEach((r, i) => {
    const y = 32 + i * 8;
    const cap = B.fortress.caps[r];
    ctx.fillStyle = '#000a'; roundRect(ctx, -bw / 2, y, bw, 6, 3); ctx.fill();
    ctx.fillStyle = RES_META[r].color;
    roundRect(ctx, -bw / 2, y, bw * Math.min(1, f.store[r] / cap), 6, 3); ctx.fill();
  });

  if (f.shot && f.shot.life > 0) {
    f.shot.life -= 1 / 60;
    ctx.strokeStyle = 'rgba(255,220,140,0.8)'; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(f.shot.x - f.x, f.shot.y - f.y); ctx.stroke();
  }
}

function storeBar(ctx, n, r) {
  const total = RES.reduce((a, x) => a + n.store[x], 0);
  const cap = warehouseCap(n);
  const bw = r * 2;
  let x = -r;
  ctx.fillStyle = '#000a'; roundRect(ctx, -r, r + 4, bw, 6, 3); ctx.fill();
  for (const res of RES) {
    const wpx = bw * (n.store[res] / cap);
    if (wpx <= 0) continue;
    ctx.fillStyle = RES_META[res].color;
    ctx.fillRect(x, r + 4, wpx, 6);
    x += wpx;
  }
  ctx.fillStyle = '#8b94a5'; ctx.font = '10px system-ui'; ctx.textAlign = 'center';
  ctx.fillText(`${Math.round(total)}/${cap}`, 0, r + 22);
}

function drawWagons(ctx, S) {
  for (const w of S.wagons) {
    const line = S.lines.find((l) => l.id === w.lineId);
    ctx.save(); ctx.translate(w.x, w.y);
    ctx.fillStyle = line?.color || '#fff';
    roundRect(ctx, -9, -6, 18, 12, 3); ctx.fill();
    ctx.strokeStyle = '#0008'; ctx.lineWidth = 1; ctx.stroke();
    // 적재 상태 점
    const total = slotTotal(w.slots);
    const carried = RES.reduce((a, r) => a + w.cargo[r], 0);
    ctx.fillStyle = '#fff'; ctx.font = 'bold 9px system-ui'; ctx.textAlign = 'center';
    ctx.fillText(`${Math.round(carried)}`, 0, 3);
    if (w.state === 'dwell') {
      ctx.strokeStyle = '#ffd77a'; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.arc(0, 0, 13, 0, Math.PI * 2); ctx.stroke();
    }
    ctx.restore();
  }
}

function drawEnemies(ctx, S) {
  for (const e of S.enemies) {
    ctx.save(); ctx.translate(e.x, e.y);
    const m = B.monsters[e.type];
    const r = e.type === 'brute' ? 11 : e.type === 'runner' ? 6 : 8;
    ctx.fillStyle = e.type === 'brute' ? '#c0504d' : e.type === 'runner' ? '#d98cc0' : '#a4574f';
    ctx.beginPath(); ctx.arc(0, 0, r, 0, Math.PI * 2); ctx.fill();
    ctx.strokeStyle = '#000a'; ctx.lineWidth = 1; ctx.stroke();
    if (e.hp < e.maxHp) {
      ctx.fillStyle = '#000a'; ctx.fillRect(-r, -r - 6, r * 2, 3);
      ctx.fillStyle = '#e0564f'; ctx.fillRect(-r, -r - 6, r * 2 * (e.hp / e.maxHp), 3);
    }
    ctx.restore();
  }
}

function drawFloats(ctx, S) {
  for (const f of S.floats) {
    ctx.globalAlpha = Math.min(1, f.life * 2);
    ctx.fillStyle = f.color; ctx.font = 'bold 13px system-ui'; ctx.textAlign = 'center';
    ctx.fillText(f.text, f.x, f.y - (1 - f.life) * 18);
    ctx.globalAlpha = 1;
  }
}

function drawGhost(ctx, S, view) {
  if (!view.buildMode) return;
  const { x, y } = view.mouse;
  const ok = view.buildOk;
  ctx.globalAlpha = 0.6;
  ctx.fillStyle = ok ? '#5fd18a' : '#e0564f';
  ctx.beginPath(); ctx.arc(x, y, 22, 0, Math.PI * 2); ctx.fill();
  ctx.globalAlpha = 1;
  ctx.strokeStyle = ok ? '#5fd18a' : '#e0564f'; ctx.lineWidth = 2; ctx.setLineDash([5, 5]);
  ctx.beginPath(); ctx.arc(x, y, B.map.minNodeDist, 0, Math.PI * 2); ctx.stroke(); ctx.setLineDash([]);
  label2(ctx, view.buildMode === 'stable' ? '🐴' : '📦', x, y + 7, 20);
}

function label(ctx, txt, x, y, size) {
  ctx.font = `${size}px system-ui`; ctx.textAlign = 'center'; ctx.fillStyle = '#fff';
  ctx.fillText(txt, x, y);
}
function label2(ctx, txt, x, y, size) { label(ctx, txt, x, y, size); }
