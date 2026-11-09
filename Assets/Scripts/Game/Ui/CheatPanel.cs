using System;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>오른쪽 위에 토글하는 치트 패널입니다. 프로토타입 빌드에서는 항상 쓸 수 있습니다. 프리팹 Prefabs/UI/Cheat에 붙습니다.</summary>
    public sealed class CheatPanel : MonoBehaviour
    {
        [SerializeField] UiButton _bGold, _bTokens, _bHeal, _bInvincible, _bSkip, _bKillAll, _bWinWave, _bWave5;
        [SerializeField] UiButton _b5, _b10, _b20, _bAuto, _bNewGame, _bBalanceLoad, _bBalanceReset;
        [SerializeField] Text _info, _balance;

        int _infoKey = int.MinValue;

        public Action OnGold, OnTokens, OnHeal, OnInvincible, OnSkipCountdown, OnKillAll, OnWinWave, OnWavePlus5, OnAuto, OnNewGame;
        public Action OnBalanceLoad, OnBalanceReset;
        public Action<int> OnSpeed;

        /// <summary>패널이 보이는지 여부입니다.</summary>
        public bool Visible => gameObject.activeSelf;

        void Awake()
        {
            void On(UiButton b, Func<Action> a) => b.Button.onClick.AddListener(() => a()?.Invoke());
            On(_bGold, () => OnGold); On(_bTokens, () => OnTokens);
            On(_bHeal, () => OnHeal); On(_bInvincible, () => OnInvincible);
            On(_bSkip, () => OnSkipCountdown); On(_bKillAll, () => OnKillAll);
            On(_bWinWave, () => OnWinWave); On(_bWave5, () => OnWavePlus5);
            On(_bAuto, () => OnAuto); On(_bNewGame, () => OnNewGame);
            On(_bBalanceLoad, () => OnBalanceLoad); On(_bBalanceReset, () => OnBalanceReset);
            _b5.Button.onClick.AddListener(() => OnSpeed?.Invoke(5));
            _b10.Button.onClick.AddListener(() => OnSpeed?.Invoke(10));
            _b20.Button.onClick.AddListener(() => OnSpeed?.Invoke(20));
        }

        /// <summary>지금 쓰는 밸런스 시트 이름을 표시합니다. null이면 기본 시트입니다.</summary>
        public void SetBalanceSource(string importedName)
        {
            const int MaxName = 24;
            string n = string.IsNullOrEmpty(importedName) ? "기본 시트" : importedName;
            if (n.Length > MaxName) n = n.Substring(0, MaxName - 1) + "…";
            _balance.text = "밸런스: " + n;
        }

        /// <summary>보임과 숨김을 바꿉니다.</summary>
        public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

        /// <summary>머리말 높이에 맞춰 위치를 잡습니다.</summary>
        public void SetTop(float topInset) => ((RectTransform)transform).anchoredPosition = new Vector2(-14f, -(topInset + 14f));

        /// <summary>토글 상태와 FPS, 시뮬레이션 시간을 갱신합니다.</summary>
        public void Refresh(Game g, int speed, bool auto, float fps)
        {
            if (!Visible) return;
            _bInvincible.SetText(g.CheatInvincible ? "무적 on" : "무적 off");
            _bInvincible.SetOn(g.CheatInvincible);
            _bAuto.SetText(auto ? "자동 플레이 on" : "자동 플레이 off");
            _bAuto.SetOn(auto);
            _b5.SetOn(speed == 5);
            _b10.SetOn(speed == 10);
            _b20.SetOn(speed == 20);
            int t = Mathf.FloorToInt(g.T), f = Mathf.RoundToInt(fps);
            int key = HashCode.Combine(t, f);
            if (key == _infoKey) return;
            _infoKey = key;
            _info.text = $"FPS {f} · 시뮬 {t / 60}:{t % 60:00}";
        }
    }
}
