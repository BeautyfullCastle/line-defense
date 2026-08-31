// ============================================================
// balance.js — 모든 수치의 단일 원본(Single Source of Truth)
// 치트 메뉴는 이 객체를 런타임에 직접 수정한다.
// ============================================================

export const RES = ['food', 'weapon', 'stone'];

export const RES_META = {
  food:   { name: '식량', icon: '🌾', color: '#e5b45a' },
  weapon: { name: '무기', icon: '⚔️', color: '#8fb8de' },
  stone:  { name: '석재', icon: '🪨', color: '#a8a29a' },
};

export const B = {
  // ---------- 1. 생산지 ----------
  producers: {
    farm:   { name: '농경지', res: 'food',   interval: 5,  cap: 10, loadRate: 0.3, color: '#e5b45a' },
    smith:  { name: '대장간', res: 'weapon', interval: 10, cap: 5,  loadRate: 0.3, color: '#8fb8de' },
    quarry: { name: '채석장', res: 'stone',  interval: 10, cap: 5,  loadRate: 0.3, color: '#a8a29a' },
  },

  // ---------- 2. 창고 ----------
  warehouses: {
    small: { name: '소형 창고', cap: 20, rate: 0.3, permit: 1, coin: 0   },
    mid:   { name: '중형 창고', cap: 40, rate: 0.2, permit: 1, coin: 50  },
    large: { name: '대형 창고', cap: 60, rate: 0.1, permit: 1, coin: 100 },
  },
  // 신축이 아니라 "업그레이드"로 올릴 때의 코인 비용 (허가권 불필요)
  warehouseUpgrade: { mid: 50, large: 100 },

  // ---------- 3. 마굿간 ----------
  stable: { permit: 1, coin: 0, rate: 0.3 },

  // ---------- 4. 마차 ----------
  wagons: {
    basic:    { name: '기본 마차',   rec: 10, speed: 4, drop: 2, coin: 0,   upgradeTo: 'heavy' },
    speed:    { name: '스피드 마차', rec: 6,  speed: 8, drop: 4, coin: 0,   upgradeTo: 'fastX' },
    heavy:    { name: '중형 마차',   rec: 20, speed: 4, drop: 1, coin: 100, upgradeTo: null },
    fastX:    { name: '강화 스피드', rec: 12, speed: 8, drop: 2, coin: 100, upgradeTo: null },
  },
  pxPerSpeed: 20,   // 표의 '이동 속도' 1 = 20px/s
  wagonSlotMult: 2,   // 최대 짐칸 = 권장 짐칸 x2
  wagonMinSpeed: 1,   // 속도 하한
  slotUnit: 1,        // 짐칸 1칸 = 자원 1

  // ---------- 5. 요새 ----------
  fortress: {
    hp: 5000,
    atk: 25,              // 1회 공격 데미지(무기 100%일 때)
    interval: 0.8,        // 기본 공격 간격(초) — 식량 100%일 때
    range: 210,
    caps: { food: 40, weapon: 30, stone: 20 },
    foodPerSec: 1 / 10,        // 상시 소모: 10초당 식량 1 (전투와 무관)
    weaponPerCombatSec: 1 / 8, // 전투 중에만 소모: 8초당 무기 1
    stoneHeal: 100,       // 석재 1당 성벽 체력 회복
    healThreshold: 0.98,  // 체력이 이 비율 미만이면 석재 자동 소모
    // 재고 계수: 재고비율 >= safe 면 100%, 0이면 floor, 사이는 선형
    stockCurve: { safe: 0.30, floor: 0.40 },
    // 요새 강화 — 코인의 핵심 사용처이자, 웨이브 성장에 맞서는 유일한 성장 축
    upgrade: { baseCost: 100, costGrow: 1.45, dmgPerLevel: 0.30, spdPerLevel: 0.12, maxLevel: 6 },
    buildLeadTime: 30,    // 새 전선이 열리면 다음 준비 시간에 이만큼을 더 준다
    // 수비대는 최소한의 보급을 들고 부임한다. 0에서 시작하면 보급선을 잇기도 전에
    // 공격 불능 상태로 함락되어, 전선 확장 자체가 불가능해진다.
    startStockRatio: 0.5,
  },

  // ---------- 6. 몬스터 ----------
  monsters: {
    basicA: { name: '기본 A', atk: 10, interval: 2, hp: 30,  speed: 26, range: 60, coin: 1 },
    runner: { name: '척후병', atk: 6,  interval: 1.5, hp: 20, speed: 46, range: 55, coin: 1 },
    brute:  { name: '공성병', atk: 30, interval: 3, hp: 150, speed: 18, range: 70, coin: 3 },
  },

  // ---------- 7. 웨이브 ----------
  wave: {
    prepTime: 30,          // 웨이브 사이 준비 시간(초)
    baseCount: 20,         // 1웨이브 몬스터 수
    countGrow: 4,          // 웨이브당 +N 마리
    duration: 30,          // 등장 기간(초)
    hpGrow: 0.12,          // 웨이브당 몬스터 체력 +12%(복리)
    atkGrow: 0.08,         // 웨이브당 몬스터 공격력 +8%(복리)
    newFortressEvery: 3,   // N 웨이브마다 새 전선(요새) 추가
    maxFortress: 4,
    // 웨이브 클리어 보상
    clearCoin: 25,         // 기본 코인
    clearCoinPerWave: 18,  // 웨이브당 추가 코인
    hpBonusCoin: 40,       // 요새 평균 체력 비율 x 이 값 만큼 추가 코인
    killCoinMult: 2,       // 몬스터 처치 코인 배수
    // 허가권 선택 보상이 나오는 웨이브 (그 외 웨이브는 코인만)
    permitWaves: [1, 2, 3, 5, 7, 9, 12, 15, 18, 21],
  },

  // ---------- 8. 경제 ----------
  economy: {
    startCoin: 50,
    startWarehousePermit: 1,
    startStablePermit: 1,
    sellPrice: 2,          // 창고 잉여 자원 1개 판매가
    // 노선에 마차를 추가 투입하는 비용. N번째 추가 마차 = base * N
    extraWagonBase: 80,
    maxWagonsPerLine: 4,
  },

  // ---------- 9. 맵 ----------
  // 원형 방어선. 요새는 원 위(침공 루트와 원의 접점)에 서고,
  // 생산지·창고·마굿간 등 물류망은 전부 원 안쪽에 들어간다.
  // 방어선은 화면보다 훨씬 큰 원이고, 중심은 화면 아래쪽 바깥에 있다.
  // 따라서 화면에는 '완만한 호'만 보이고, 그 안쪽(아래) 전체가 아군 영토가 된다.
  map: {
    w: 1280, h: 800,
    cx: 640, cy: 1150,     // 원 중심 (화면 아래 바깥)
    radius: 900,           // 방어선 반지름
    innerMargin: 55,       // 건물은 방어선에서 이만큼 안쪽에만 지을 수 있다
    canvasMargin: 45,      // 화면 가장자리 여백
    enemyRingGap: 170,     // 적은 방어선 바깥 이만큼 떨어진 곳에서 출현한다
    // 요새가 서는 각도(도). 침공 루트가 방어선과 만나는 접점.
    fortressAngles: [-112, -68, -91, -129],
    minNodeDist: 62,       // 노드 간 최소 배치 거리
    startProducers: [
      ['farm', 1], ['smith', 1], ['quarry', 1],
    ],
    newProducerEvery: 2,   // N 웨이브마다 생산지 1개 추가 생성
    // 새 전선이 열리면 그 전선 뒤쪽에 생산지 한 세트가 함께 생긴다.
    // 이것이 없으면 생산량이 요새 수를 못 따라가 어떤 전략도 5웨이브를 못 넘긴다.
    frontProducers: ['farm', 'smith', 'quarry'],
    frontProducerSpread: 210,   // 요새로부터 이 정도 안쪽에 배치
  },
};

// 재고 비율 → 성능 계수
export function stockFactor(cur, cap) {
  if (cur <= 0) return 0;
  const { safe, floor } = B.fortress.stockCurve;
  const r = cap > 0 ? cur / cap : 0;
  if (r >= safe) return 1;
  return floor + (1 - floor) * (r / safe);
}

export function wagonMaxSlots(type) {
  return Math.round(B.wagons[type].rec * B.wagonSlotMult);
}

export function wagonSpeed(type, slots) {
  const w = B.wagons[type];
  // 권장 짐칸 초과분에 비례해 선형 감속.
  // 최대 짐칸(권장 x2)까지 채우면 표의 '감소치'가 전부 적용된다(= 기본 속도의 절반).
  const over = Math.max(0, slots - w.rec);
  const t = over / (w.rec * (B.wagonSlotMult - 1));
  return Math.max(B.wagonMinSpeed, w.speed - w.drop * t);
}
