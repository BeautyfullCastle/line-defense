using System;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>상단 가운데에 잠깐 뜨는 알림입니다.</summary>
    public sealed class ToastView
    {
        const float Duration = 3.2f;

        readonly RectTransform _root;
        readonly Text _text;
        float _left;
        float _top = 60f;

        /// <summary>알림 요소를 만듭니다.</summary>
        public ToastView(Transform parent)
        {
            _root = UiFactory.Rect(parent, "Toast");
            UiFactory.Fill(_root, Palette.Ink, true, false);
            _text = UiFactory.Label(_root, string.Empty, 14, Palette.Ground, true, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_text.rectTransform, 12f, 0f, 12f, 0f);
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _root.gameObject.SetActive(false);
        }

        /// <summary>머리말 높이에 맞춰 위치를 잡습니다.</summary>
        public void SetTop(float topInset)
        {
            _top = topInset;
            _root.anchoredPosition = new Vector2(0f, -(topInset + 14f));
        }

        /// <summary>알림을 띄웁니다.</summary>
        public void Show(string msg, float canvasW)
        {
            if (string.IsNullOrEmpty(msg)) return;
            _text.text = msg;
            float w = Mathf.Min(UiFactory.TextWidth(msg, 14, true) + 32f, canvasW - 32f);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.sizeDelta = new Vector2(w, 34f);
            SetTop(_top);
            _left = Duration;
            _root.gameObject.SetActive(true);
        }

        /// <summary>남은 시간을 줄이고 끝나면 숨깁니다.</summary>
        public void Tick(float dt)
        {
            if (_left <= 0f) return;
            _left -= dt;
            if (_left <= 0f) _root.gameObject.SetActive(false);
        }
    }

    /// <summary>도움말 오버레이입니다. 내용이 길면 스크롤됩니다.</summary>
    public sealed class HelpView
    {
        static readonly string[] Items =
        {
            "<b>마굿간</b>을 성 안에 놓으면 노선 하나와 마차 한 대가 생깁니다.",
            "마굿간에서 <b>농장(●)</b>, <b>대장간(▲)</b>, <b>요새</b>로 드래그해 길을 잇습니다. 길 끝 막대를 끌면 늘어나고, 바로 앞 정거장에 놓으면 줄어듭니다. <b>선 중간</b>을 잡아 끌어 다른 정거장에 놓으면 두 정거장 사이에 끼워 넣습니다.",
            "정거장이 3개 이상일 때 길 끝을 <b>반대쪽 끝 정거장</b>에 놓으면 순환 노선이 되어 마차가 한 방향으로 계속 돕니다.",
            "<b>요새</b>는 성벽 위에만 지을 수 있고, 무기가 있어야 사거리 안의 몬스터를 쏩니다. 수비대는 12초마다 식량을 1개 먹고 그때마다 <b>사기</b>가 20% 오릅니다. 먹을 식량이 없으면 사기가 20% 떨어집니다. 공격력은 사기에 비례하고(요새 아래 막대), 사기가 0이면 공격하지 않습니다. 요새 아래 칸에서 속이 찬 ●▲는 보유분, 속이 빈 ○△는 부족분입니다.",
            "<b>요새를 클릭</b>하면 몬스터 처치로 모은 골드로 강화합니다. 공격력을 올리면 식량을 더 자주 먹고, 공격 속도를 올리면 무기를 더 빨리 씁니다.",
            "<b>마굿간</b>을 클릭하면 그 노선 마차의 식량 칸·무기 칸 수를 정합니다. 마차가 마굿간을 지날 때 적용되고, 칸을 넘는 자원은 마굿간에 남습니다.",
            "<b>창고</b>를 클릭하면 창고의 식량 칸·무기 칸 수를 정합니다. 바로 적용되고, 칸을 넘는 자원은 버려지지 않고 마차가 먼저 실어 갑니다.",
            "농장·대장간은 한 노선에만 속합니다. <b>요새</b>와 <b>창고</b>는 여러 노선이 함께 들를 수 있습니다(개수 제한 없음). 짐을 넘겨받는 환승은 창고에서만 됩니다. 마차는 창고에 오면 <b>다음 창고까지 필요한 만큼만</b> 싣습니다. 지나갈 요새들의 빈 칸만큼, 그리고 다음 창고가 이 창고보다 더 모자라면 그 차이만큼입니다. 창고 칸의 점선 ○△는 목표량까지 모자란 칸, 요새 칸의 점선은 마차가 가져오는 중인 칸입니다.",
        };

        const string Tail = "몬스터는 붉은 점선의 침투로로만 옵니다. 새 침투로는 약하게 시작해 웨이브마다 위협이 커집니다. 침투로는 3웨이브마다, 개척지는 4웨이브마다 늘어납니다.";

        readonly RectTransform _root;
        RectTransform _card;

        /// <summary>닫기 버튼을 눌렀을 때 호출됩니다.</summary>
        public Action OnClose;

        /// <summary>오버레이 루트를 만듭니다.</summary>
        public HelpView(Transform parent)
        {
            _root = UiFactory.Rect(parent, "Help");
            UiFactory.Stretch(_root);
            UiFactory.Fill(_root, Palette.WithAlpha(Palette.Outside, 0.55f), false, true);
            _root.gameObject.SetActive(false);
        }

        /// <summary>열려 있는지 여부입니다.</summary>
        public bool IsOpen => _root.gameObject.activeSelf;

        /// <summary>도움말을 보여줍니다.</summary>
        public void Show(float canvasW, float canvasH)
        {
            if (_card != null) UnityEngine.Object.Destroy(_card.gameObject);
            _root.gameObject.SetActive(true);
            float w = Mathf.Min(480f, canvasW - 32f);
            _card = UiFactory.Card(_root, "Card", Palette.Panel, Palette.Line);
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = new Vector2(0.5f, 0.5f);

            var view = UiFactory.Rect(_card, "Viewport");
            UiFactory.Stretch(view, 2f, 2f, 2f, 2f);
            UiFactory.Fill(view, new Color(0f, 0f, 0f, 0.001f), false, true);
            view.gameObject.AddComponent<RectMask2D>();
            var content = UiFactory.Rect(view, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;

            var s = new VStack(content, w - 4f, 20f);
            s.AddText("변경 영지 운영법", 26, Palette.Ink, true, TextAnchor.UpperLeft, 10f);
            for (int i = 0; i < Items.Length; i++)
                s.AddText($"{i + 1}. {Items[i]}", 14, Palette.Ink, false, TextAnchor.UpperLeft, 6f);
            s.Gap(6f);
            s.AddText(Tail, 14, Palette.Ink, false, TextAnchor.UpperLeft, 10f);
            var close = UiFactory.Button(content, "닫기", () => OnClose?.Invoke(), 90f, 38f, 15, true);
            close.Rt.anchorMin = close.Rt.anchorMax = new Vector2(0, 1);
            close.Rt.pivot = new Vector2(0, 1);
            close.Rt.anchoredPosition = new Vector2(20f, -(s.Height - 20f));
            float contentH = s.Height + 38f;
            content.sizeDelta = new Vector2(0f, contentH);

            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = view;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;

            _card.sizeDelta = new Vector2(w, Mathf.Min(contentH + 4f, canvasH - 32f));
            _root.gameObject.SetActive(true);
        }

        /// <summary>오버레이를 숨깁니다.</summary>
        public void Hide()
        {
            _root.gameObject.SetActive(false);
            if (_card != null) UnityEngine.Object.Destroy(_card.gameObject);
            _card = null;
        }
    }

    /// <summary>성벽이 무너졌을 때 결과를 보여주는 오버레이입니다.</summary>
    public sealed class OverView
    {
        readonly RectTransform _root;
        RectTransform _card;

        /// <summary>다시 시작 버튼을 눌렀을 때 호출됩니다.</summary>
        public Action OnRestart;

        /// <summary>오버레이 루트를 만듭니다.</summary>
        public OverView(Transform parent)
        {
            _root = UiFactory.Rect(parent, "Over");
            UiFactory.Stretch(_root);
            UiFactory.Fill(_root, Palette.WithAlpha(Palette.Outside, 0.55f), false, true);
            _root.gameObject.SetActive(false);
        }

        /// <summary>열려 있는지 여부입니다.</summary>
        public bool IsOpen => _root.gameObject.activeSelf;

        /// <summary>결과를 보여줍니다.</summary>
        public void Show(Game g, float canvasW)
        {
            if (_card != null) UnityEngine.Object.Destroy(_card.gameObject);
            _root.gameObject.SetActive(true);
            float w = Mathf.Min(440f, canvasW - 32f);
            _card = UiFactory.Card(_root, "Card", Palette.Panel, Palette.Line);
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = new Vector2(0.5f, 0.5f);
            var s = new VStack(_card, w, 22f);
            s.Gap(-4f);
            s.AddText("성벽이 무너졌습니다", 26, Palette.Ink, true, TextAnchor.UpperLeft, 10f);
            var q = s.AddText("변경의 성벽이 무너졌다. 수도는 그 소식을 반란의 징조로 받아들였다.", 14, Palette.Muted, false, TextAnchor.UpperLeft, 14f, 13f);
            q.fontStyle = FontStyle.Italic;
            var bar = UiFactory.Rect(_card, "QuoteBar");
            bar.anchorMin = bar.anchorMax = new Vector2(0, 1);
            bar.pivot = new Vector2(0, 1);
            bar.anchoredPosition = new Vector2(22f, q.rectTransform.anchoredPosition.y);
            bar.sizeDelta = new Vector2(3f, q.rectTransform.sizeDelta.y);
            UiFactory.Fill(bar, Palette.Line, false, false);

            var row = s.AddRect("Kpis", 46f, -1f, 16f);
            string[] labels = { "버틴 웨이브", "처치", "운송한 보급품" };
            string[] values = { Mathf.Max(0, g.Wave - 1).ToString(), g.Kills.ToString(), g.Delivered.ToString() };
            float x = 0f;
            for (int i = 0; i < 3; i++)
            {
                float cw = Mathf.Max(UiFactory.TextWidth(labels[i], 11, true), 40f) + 18f;
                var l = UiFactory.Label(row, labels[i], 11, Palette.Muted, true);
                Top(l.rectTransform, x, 0f, cw, 16f);
                var v = UiFactory.Label(row, values[i], 22, Palette.Ink, true);
                Top(v.rectTransform, x, 18f, cw, 28f);
                x += cw;
            }
            var btn = UiFactory.Button(_card, "다시 시작", () => OnRestart?.Invoke(), 120f, 40f, 15, true);
            Top(btn.Rt, 22f, s.Height - 22f - 8f, 120f, 40f);
            _card.sizeDelta = new Vector2(w, s.Height - 22f + 40f + 22f - 8f);
            _root.gameObject.SetActive(true);
        }

        static void Top(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>오버레이를 숨깁니다.</summary>
        public void Hide()
        {
            _root.gameObject.SetActive(false);
            if (_card != null) UnityEngine.Object.Destroy(_card.gameObject);
            _card = null;
        }
    }
}
