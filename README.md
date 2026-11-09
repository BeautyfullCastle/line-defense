# 요새 노선도 (Line Defense)

노선을 이어 보급을 나르는 운송 퍼즐과 타워 디펜스를 섞은 웹 게임 프로토타입입니다. Unity 6.3 LTS(6000.3.28f1)로 만들었고 WebGL로 빌드해 GitHub Pages에 올립니다.

플레이: https://beautyfullcastle.github.io/line-defense/

## 규칙

- 마굿간을 지으면 노선과 마차 한 대가 생깁니다. 마굿간에서 농장(원), 대장간(삼각형), 요새(육각형)로 드래그해 길을 잇습니다.
- 농장은 식량, 대장간은 무기를 만듭니다. 마차가 이를 요새로 나릅니다.
- 요새는 성벽 위에만 지을 수 있습니다. 식량이 사기(공격력)를, 무기가 화살 수를 정합니다.
- 침투로에서 몬스터가 와서 성벽을 공격합니다. 성벽이 무너지면 게임이 끝납니다.
- 웨이브를 막을 때마다 보급 선택지 셋 중 하나를 고릅니다. 4웨이브마다 개척지가 넓어지고, 3웨이브마다 침투로가 늘며 요새 권한이 1 추가됩니다.
- 창고(X 사각형)는 여러 노선이 짐을 주고받는 환승 거점입니다.

## 조작

| 입력 | 동작 |
|---|---|
| 하단 버튼, 1 / 2 / 3 | 마굿간 / 요새 / 창고 건설 모드 |
| 드래그 | 노선 끝 막대나 마굿간에서 끌어 정거장을 잇기 |
| 클릭 | 마굿간, 창고, 요새 설정창 열기 |
| 우클릭, Esc | 건설 취소 |
| Space | 일시정지 |
| Enter | 대사 넘기기 |
| F1, ` | 치트 패널 |

치트 패널에는 골드, 건설 권한, 성벽 회복, 무적, 침공 시작, 몬스터 전멸, 웨이브 클리어와 스킵, 5/10/20배속, 자동 플레이(봇), 밸런스 시트 불러오기가 있습니다.

## 밸런스 시트

밸런스 값은 기획 시트(xlsx)에서 읽습니다.

- 기본 시트는 `Assets/Resources/BalanceSheet.bytes`입니다. Notion [P1] 프로토 타입 1의 `요새노선도_밸런스_보급선택지.xlsx`를 확장자만 바꿔 넣었습니다. 게임과 SimRunner가 모두 이 파일을 읽습니다.
- "밸런스" 시트의 머리글에서 "코드 상수"와 "값" 열을 찾아 읽습니다. 코드 상수 칸이 빈 계산값 행은 건너뜁니다.
- "보급 선택지" 시트에서 "ID", "값", "등장 가중치", "최대 선택 횟수", "첫 등장 웨이브" 열을 읽습니다. 최대 선택 횟수가 빈 칸이면 제한이 없습니다. 강화 보급(역참 마필 개량, 수레 개조, 창고 확장, 성벽 보수 자재)의 효과 값은 "밸런스" 시트의 상수를 씁니다. `SUP_GOLD_GROW` 행은 군자금 지원의 웨이브당 증가량입니다.
- 같은 ID가 두 번 있으면 첫 행만 쓰고 경고합니다. 코드에 없는 ID는 무시하고 알림에 개수를 보여 줍니다.
- 시트에 없는 상수와 선택지는 코드 값(`Balance.cs`, `Supply.cs`)을 씁니다. 지금은 `WH_RESERVE`, `WH_MIN_TRANSFER`가 그렇습니다.
- 기본 밸런스를 바꾸려면 기본 시트 파일을 새 xlsx로 바꿉니다.
- 플레이 중에 시험하려면 치트 패널의 "밸런스 불러오기"로 xlsx를 고릅니다. 값을 적용하고 새 게임으로 시작합니다. 불러온 값은 브라우저에 남고, "밸런스 기본값"을 누르면 기본 시트로 돌아갑니다.

시트 읽기 테스트:

```bash
dotnet test Tools/CoreTests
```

## 구조

| 경로 | 내용 |
|---|---|
| `Assets/Scripts/Core` | UnityEngine에 의존하지 않는 시뮬레이션(`Game`), 밸런스 값(`Balance`), 자동 플레이 봇(`Bot`) |
| `Assets/Scripts/Game` | 렌더링(도형 메시), 입력, uGUI 화면. 빈 씬에서 코드로 전부 생성 |
| `Assets/Scripts/Editor/BuildScript.cs` | WebGL 빌드 진입점 |
| `Tools/SimRunner` | Core를 그대로 컴파일해 봇으로 수백 판을 돌리는 밸런스 시뮬레이터 |
| `Tools/CoreTests` | Core 단위 테스트(xUnit) |
| `site/` | 배포용 WebGL 빌드 결과 |

## 빌드와 배포

```bash
Unity -batchmode -quit -nographics -projectPath . -buildTarget WebGL -executeMethod BuildScript.BuildWebGL
```

빌드 결과는 `Build/WebGL`에 생깁니다. 이를 `site/`로 복사해 `main`에 푸시하면 `.github/workflows/pages.yml`이 Pages로 배포합니다. CI에는 Unity 라이선스가 없어 빌드는 로컬에서 합니다.

## 밸런스 시뮬레이션

```bash
dotnet run -c Release --project Tools/SimRunner -- --runs 100 --seed 1
```

기본으로 기본 시트를 읽습니다. `--sheet 파일.xlsx`로 다른 시트를 읽고, `--no-sheet`는 `Balance.cs` 값만 씁니다. `--set KEY=VALUE`는 시트 값 위에 덮어씁니다. `--trace`는 10초마다 노선과 요새 재고를 출력합니다. 현재 기본 시트에서 봇은 100판 평균 5.3웨이브(중앙값 5, 최소 1, 최대 10)까지 버팁니다. 봇은 더 이을 것이 없으면 요새가 있는 노선을 순환으로 닫습니다.

## 글꼴

NanumGothic (SIL Open Font License 1.1). `Assets/Resources/Fonts/OFL.txt` 참고.
