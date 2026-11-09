using System;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>오른쪽 위에 토글하는 치트 패널입니다. 프로토타입 빌드에서는 항상 쓸 수 있습니다. 프리팹 Prefabs/UI/Cheat에 붙습니다.</summary>
    public sealed class CheatPanel : MonoBehaviour
    {
        [SerializeField] private UiButton _goldButton, _tokensButton, _healButton, _invincibleButton, _skipCountdownButton, _killAllButton, _winWaveButton, _wavePlus5Button;
        [SerializeField] private UiButton _speed5Button, _speed10Button, _speed20Button, _autoButton, _newGameButton, _balanceLoadButton, _balanceResetButton;
        [SerializeField] private Text _info, _balance;

        private int _infoKey = int.MinValue;

        public Action OnGold, OnTokens, OnHeal, OnInvincible, OnSkipCountdown, OnKillAll, OnWinWave, OnWavePlus5, OnAuto, OnNewGame;
        public Action OnBalanceLoad, OnBalanceReset;
        public Action<int> OnSpeed;

        /// <summary>패널이 보이는지 여부입니다.</summary>
        public bool Visible => gameObject.activeSelf;

        private void Awake()
        {
            void On(UiButton button, Func<Action> actionGetter) => button.Button.onClick.AddListener(() => actionGetter()?.Invoke());
            On(_goldButton, () => OnGold);
            On(_tokensButton, () => OnTokens);
            On(_healButton, () => OnHeal);
            On(_invincibleButton, () => OnInvincible);
            On(_skipCountdownButton, () => OnSkipCountdown);
            On(_killAllButton, () => OnKillAll);
            On(_winWaveButton, () => OnWinWave);
            On(_wavePlus5Button, () => OnWavePlus5);
            On(_autoButton, () => OnAuto);
            On(_newGameButton, () => OnNewGame);
            On(_balanceLoadButton, () => OnBalanceLoad);
            On(_balanceResetButton, () => OnBalanceReset);
            _speed5Button.Button.onClick.AddListener(() => OnSpeed?.Invoke(5));
            _speed10Button.Button.onClick.AddListener(() => OnSpeed?.Invoke(10));
            _speed20Button.Button.onClick.AddListener(() => OnSpeed?.Invoke(20));
        }

        /// <summary>지금 쓰는 밸런스 시트 이름을 표시합니다. null이면 기본 시트입니다.</summary>
        public void SetBalanceSource(string importedName)
        {
            const int MaxName = 24;
            string displayName = string.IsNullOrEmpty(importedName) ? "기본 시트" : importedName;
            if (displayName.Length > MaxName)
            {
                displayName = displayName.Substring(0, MaxName - 1) + "…";
            }

            _balance.text = "밸런스: " + displayName;
        }

        /// <summary>보임과 숨김을 바꿉니다.</summary>
        public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

        /// <summary>머리말 높이에 맞춰 위치를 잡습니다.</summary>
        public void SetTop(float topInset) => ((RectTransform)transform).anchoredPosition = new Vector2(-14f, -(topInset + 14f));

        /// <summary>토글 상태와 FPS, 시뮬레이션 시간을 갱신합니다.</summary>
        public void Refresh(Game game, int speed, bool auto, float framesPerSecond)
        {
            if (!Visible)
            {
                return;
            }

            _invincibleButton.SetText(game.CheatInvincible ? "무적 on" : "무적 off");
            _invincibleButton.SetOn(game.CheatInvincible);
            _autoButton.SetText(auto ? "자동 플레이 on" : "자동 플레이 off");
            _autoButton.SetOn(auto);
            _speed5Button.SetOn(speed == 5);
            _speed10Button.SetOn(speed == 10);
            _speed20Button.SetOn(speed == 20);
            int seconds = Mathf.FloorToInt(game.ElapsedTime), roundedFramesPerSecond = Mathf.RoundToInt(framesPerSecond);
            int key = HashCode.Combine(seconds, roundedFramesPerSecond);
            if (key == _infoKey)
            {
                return;
            }

            _infoKey = key;
            _info.text = $"FPS {roundedFramesPerSecond} · 시뮬 {seconds / 60}:{seconds % 60:00}";
        }
    }
}
