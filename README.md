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

- 기본 시트는 `Assets/Game/Contents/Balance/BalanceSheet.bytes`입니다. Notion [P1] 프로토 타입 1의 `요새노선도_밸런스_보급선택지.xlsx`를 확장자만 바꿔 넣었습니다. 게임과 SimRunner가 모두 이 파일을 읽습니다.
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
| `Assets/Scripts/Game` | 입력, 월드 그리기, uGUI 뷰 컴포넌트 |
| `Assets/Scripts/Game/World` | 건물·마차·몬스터·굴·건설 미리보기 프리팹의 뷰 컴포넌트 |
| `Assets/Scripts/Game/Ui` | 화면 UI 뷰 컴포넌트(머리말, 꼬리말, 설정창, 보급, 대사 등) |
| `Assets/Art/Sprites` | 스프라이트 원본 PNG. 흰색으로 그려 두고 코드에서 색을 입힘 (`World`, `Icons`, `UI`) |
| `Assets/Art/WorldArt.asset`, `IconSet.asset` | 월드 프리팹·스프라이트·메시 머티리얼 목록, UI 아이콘 목록 |
| `Assets/Art/*.spriteatlasv2` | 월드, UI 스프라이트 아틀라스 |
| `Assets/Game/Contents` | contents-loader가 팩으로 묶는 콘텐츠. `Game.prefab`(게임 루트), `Balance/BalanceSheet.bytes`(기본 밸런스 시트) |
| `Assets/Prefabs/World` | 월드 프리팹 |
| `Assets/Prefabs/UI` | UI 프리팹. `Ui.prefab`이 캔버스와 모든 뷰를 묶음. `Items`는 실행 중에 재사용하는 항목 |
| `Assets/Scripts/Editor/BuildScript.cs` | WebGL 빌드 진입점 |
| `Tools/SimRunner` | Core를 그대로 컴파일해 봇으로 수백 판을 돌리는 밸런스 시뮬레이터 |
| `Tools/CoreTests` | Core 단위 테스트(xUnit) |
| `Assets/Tests/EditMode` | 프리팹과 에셋의 직렬화 참조가 비지 않았는지 확인하는 Unity 테스트 |
| `Assets/Tests/PlayMode` | 게임을 띄워 버튼이 포인터를 받고 눌리는지 확인하는 Unity 테스트 |
| `.editorconfig` | Microsoft .NET 코딩 규칙(이름, 중괄호, 한 줄에 문장 하나) |
| `site/` | 배포용 WebGL 빌드 결과 |

## 그림과 UI

- 모양이 정해진 그림(건물, 마차, 몬스터, 굴, 자원 칸, 아이콘, 둥근 사각형)은 `Assets/Art/Sprites`의 PNG입니다. 같은 이름의 파일로 바꾸면 게임에 바로 반영됩니다.
- 플레이에 따라 모양이 바뀌는 것(땅, 성벽, 침투로, 노선, 사거리, 효과)은 코드가 메시 세 장(바닥, 노선, 효과)으로 만들고, 머티리얼은 `Assets/Art/Materials/WorldMesh.mat`입니다.
- 색은 `Palette`에서 정하고 스프라이트에 입힙니다.
- UI 캔버스는 바뀌는 빈도로 나눴습니다. 월드 글자 캔버스와 UI 캔버스가 있고, UI 캔버스 아래에 머리말, 꼬리말, 설정창, 치트, 팝업, 연출 하위 캔버스가 있습니다. 입력을 받는 하위 캔버스에만 레이캐스터가 있습니다.
- 노선 칩, 설정창 칸, 보급 선택지, 날아가는 아이콘, 월드 글자는 `Prefabs/UI/Items`의 프리팹을 만들어 두고 재사용합니다.

## 콘텐츠 로딩

애셋은 [contents-loader](https://github.com/BeautyfullCastle/contents-loader) 패키지(`com.h.contents-loader`)로 읽습니다. Resources 폴더는 쓰지 않습니다.

- `Assets/Game/Contents` 아래의 애셋은 경로로 읽습니다. 예: `Content.Load<GameObject>("Game")`.
- `Main` 씬에는 `Boot`만 있습니다. `Boot`가 `await Content.Download("")`로 팩을 받은 뒤 `Game` 프리팹을 만듭니다. WebGL은 팩을 웹 주소에서 받아야 해서 이 순서가 필요합니다.
- 프리팹이 쓰는 스프라이트, 글꼴, 셰이더는 패키지가 의존성을 따라가 팩에 넣습니다. 지금은 게임, 밸런스 시트, 셰이더 팩 3개가 앱(StreamingAssets)에 들어갑니다.
- 에디터 플레이 모드는 빌드 없이 AssetDatabase에서 바로 읽습니다.

## 빌드와 배포

```bash
Unity -batchmode -quit -nographics -projectPath . -buildTarget WebGL -executeMethod BuildScript.BuildWebGL
```

빌드 스크립트는 `ContentsBuilder.BuildPlayer`로 콘텐츠 팩을 먼저 만든 뒤 플레이어를 빌드합니다. 팩 빌드 결과와 리포트는 `ContentsBuild/WebGL`에 생깁니다. 빌드 결과는 `Build/WebGL`에 생깁니다. 이를 `site/`로 복사해 `main`에 푸시하면 `.github/workflows/pages.yml`이 Pages로 배포합니다. CI에는 Unity 라이선스가 없어 빌드는 로컬에서 합니다.

## 테스트

```bash
dotnet test Tools/CoreTests
unity test . --mode EditMode --output Temp/editmode.xml --timeout 900
unity test . --mode PlayMode --output Temp/playmode.xml --timeout 900
```

Unity 테스트는 에디터가 열려 있지 않을 때 배치 모드로 돕니다. 직렬화 필드 이름을 바꾸면 프리팹에 저장된 키도 같이 바꿔야 합니다. 바꾸지 않으면 EditMode 테스트가 빈 참조로 잡습니다.

## 코드 규칙

`.editorconfig`가 Microsoft .NET 코딩 규칙을 정합니다. 이름은 줄이지 않고 끝까지 씁니다. 밸런스 상수(`FORT_ATK` 등)는 밸런스 시트의 코드 상수 열과 같은 이름이라 대문자 그대로 둡니다.

```bash
dotnet format LineDefense.Game.csproj --verify-no-changes --severity warn
```

## 밸런스 시뮬레이션

```bash
dotnet run -c Release --project Tools/SimRunner -- --runs 100 --seed 1
```

기본으로 기본 시트를 읽습니다. `--sheet 파일.xlsx`로 다른 시트를 읽고, `--no-sheet`는 `Balance.cs` 값만 씁니다. `--set KEY=VALUE`는 시트 값 위에 덮어씁니다. `--trace`는 10초마다 노선과 요새 재고를 출력합니다. 현재 기본 시트에서 봇은 100판 평균 5.3웨이브(중앙값 5, 최소 1, 최대 10)까지 버팁니다. 봇은 더 이을 것이 없으면 요새가 있는 노선을 순환으로 닫습니다.

## 글꼴

NanumGothic (SIL Open Font License 1.1). `Assets/Art/Fonts/OFL.txt` 참고.
