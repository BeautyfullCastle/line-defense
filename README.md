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

치트 패널에는 골드, 건설 권한, 성벽 회복, 무적, 침공 시작, 몬스터 전멸, 웨이브 클리어와 스킵, 5/10/20배속, 자동 플레이(봇)가 있습니다.

## 구조

| 경로 | 내용 |
|---|---|
| `Assets/Scripts/Core` | UnityEngine에 의존하지 않는 시뮬레이션(`Game`), 밸런스 값(`Balance`), 자동 플레이 봇(`Bot`) |
| `Assets/Scripts/Game` | 렌더링(도형 메시), 입력, uGUI 화면. 빈 씬에서 코드로 전부 생성 |
| `Assets/Scripts/Editor/BuildScript.cs` | WebGL 빌드 진입점 |
| `Tools/SimRunner` | Core를 그대로 컴파일해 봇으로 수백 판을 돌리는 밸런스 시뮬레이터 |
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

`--set KEY=VALUE`로 `Balance` 값을 바꿔 볼 수 있고, `--trace`는 10초마다 노선과 요새 재고를 출력합니다. 현재 값에서 봇은 100판 평균 12.0웨이브(중앙값 11, 최소 5, 최대 19)까지 버팁니다. 봇은 더 이을 것이 없으면 요새가 있는 노선을 순환으로 닫습니다.

## 글꼴

NanumGothic (SIL Open Font License 1.1). `Assets/Resources/Fonts/OFL.txt` 참고.
