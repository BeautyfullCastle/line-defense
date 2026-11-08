using System;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>오른쪽 위에 토글하는 치트 패널입니다. 프로토타입 빌드에서는 항상 쓸 수 있습니다.</summary>
    public sealed class CheatPanel
    {
        const float ColW = 104f, BtnH = 30f, Gap = 8f, Pad = 12f;

        readonly RectTransform _card;
        readonly UiButton _bInvincible, _bAuto, _b5, _b10, _b20;
        readonly Text _info;
        int _infoKey = int.MinValue;

        public Action OnGold, OnTokens, OnHeal, OnInvincible, OnSkipCountdown, OnKillAll, OnWinWave, OnWavePlus5, OnAuto, OnNewGame;
        public Action<int> OnSpeed;

        /// <summary>패널이 보이는지 여부입니다.</summary>
        public bool Visible => _card.gameObject.activeSelf;

        /// <summary>패널과 버튼을 만듭니다. 처음에는 숨겨져 있습니다.</summary>
        public CheatPanel(Transform parent)
        {
            float w = Pad * 2f + ColW * 2f + Gap;
            _card = UiFactory.Card(parent, "Cheat", Palette.Panel, Palette.Line);
            _card.anchorMin = _card.anchorMax = new Vector2(1, 1);
            _card.pivot = new Vector2(1, 1);

            float y = Pad;
            var title = UiFactory.Label(_card, "치트 (F1 / `)", 13, Palette.Ink, true);
            Top(title.rectTransform, Pad, y, w - Pad * 2f, 18f);
            y += 24f;

            Pair(ref y, "골드 +100", () => OnGold?.Invoke(), "건설 권한 +1", () => OnTokens?.Invoke());
            _bInvincible = Pair(ref y, "성벽 회복", () => OnHeal?.Invoke(), "무적 off", () => OnInvincible?.Invoke(), true);
            Pair(ref y, "침공 바로 시작", () => OnSkipCountdown?.Invoke(), "몬스터 전멸", () => OnKillAll?.Invoke());
            Pair(ref y, "웨이브 클리어", () => OnWinWave?.Invoke(), "웨이브 +5", () => OnWavePlus5?.Invoke());

            float sw = (w - Pad * 2f - Gap * 2f) / 3f;
            _b5 = SpeedButton("5×", 5, Pad, y, sw);
            _b10 = SpeedButton("10×", 10, Pad + sw + Gap, y, sw);
            _b20 = SpeedButton("20×", 20, Pad + (sw + Gap) * 2f, y, sw);
            y += BtnH + Gap;

            _bAuto = Pair(ref y, "자동 플레이 off", () => OnAuto?.Invoke(), "새 게임", () => OnNewGame?.Invoke(), false, true);

            _info = UiFactory.Label(_card, string.Empty, 11, Palette.Muted);
            Top(_info.rectTransform, Pad, y, w - Pad * 2f, 16f);
            y += 16f + Pad;

            _card.sizeDelta = new Vector2(w, y);
            _card.gameObject.SetActive(false);
        }

        UiButton Pair(ref float y, string a, Action ca, string b, Action cb, bool secondIsToggle = false, bool firstIsToggle = false)
        {
            var ba = UiFactory.Button(_card, a, ca, ColW, BtnH, 12);
            Top(ba.Rt, Pad, y, ColW, BtnH);
            var bb = UiFactory.Button(_card, b, cb, ColW, BtnH, 12);
            Top(bb.Rt, Pad + ColW + Gap, y, ColW, BtnH);
            y += BtnH + Gap;
            return secondIsToggle ? bb : firstIsToggle ? ba : null;
        }

        UiButton SpeedButton(string label, int speed, float x, float y, float w)
        {
            var b = UiFactory.Button(_card, "배속 " + label, () => OnSpeed?.Invoke(speed), w, BtnH, 12);
            Top(b.Rt, x, y, w, BtnH);
            return b;
        }

        static void Top(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>보임과 숨김을 바꿉니다.</summary>
        public void Toggle() => _card.gameObject.SetActive(!_card.gameObject.activeSelf);

        /// <summary>머리말 높이에 맞춰 위치를 잡습니다.</summary>
        public void SetTop(float topInset) => _card.anchoredPosition = new Vector2(-14f, -(topInset + 14f));

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
