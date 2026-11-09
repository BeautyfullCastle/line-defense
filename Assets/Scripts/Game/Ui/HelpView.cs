using System;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>도움말 오버레이입니다. 내용이 길면 스크롤됩니다. 프리팹 Prefabs/UI/Help에 붙습니다.</summary>
    public sealed class HelpView : MonoBehaviour
    {
        private const float Padding = 20f;

        private static readonly string[] s_items =
        {
            "<b>마굿간</b>을 성 안에 놓으면 노선 하나와 마차 한 대가 생깁니다.",
            "마굿간에서 <b>농장(●)</b>, <b>대장간(▲)</b>, <b>요새</b>로 드래그해 길을 잇습니다. 길 끝 막대를 끌면 늘어나고, 바로 앞 정거장에 놓으면 줄어듭니다. <b>선 중간</b>을 잡아 끌어 다른 정거장에 놓으면 두 정거장 사이에 끼워 넣습니다.",
            "정거장이 3개 이상일 때 길 끝을 <b>반대쪽 끝 정거장</b>에 놓으면 순환 노선이 되어 마차가 한 방향으로 계속 돕니다.",
            "<b>요새</b>는 성벽 위에만 지을 수 있고, 무기가 있어야 사거리 안의 몬스터를 쏩니다. 수비대는 {food}초마다 식량을 1개 먹고 그때마다 <b>사기</b>가 {up}% 오릅니다. 먹을 식량이 없으면 사기가 {down}% 떨어집니다. 공격력은 사기에 비례하고(요새 아래 막대), 사기가 0이면 공격하지 않습니다. 요새 아래 칸에서 속이 찬 ●▲는 보유분, 속이 빈 ○△는 부족분입니다.",
            "<b>요새를 클릭</b>하면 몬스터 처치로 모은 골드로 강화합니다. 공격력을 올리면 식량을 더 자주 먹고, 공격 속도를 올리면 무기를 더 빨리 씁니다.",
            "<b>마굿간</b>을 클릭하면 그 노선 마차의 식량 칸·무기 칸 수를 정합니다. 마차가 마굿간을 지날 때 적용되고, 칸을 넘는 자원은 마굿간에 남습니다.",
            "<b>창고</b>를 클릭하면 창고의 식량 칸·무기 칸 수를 정합니다. 바로 적용되고, 칸을 넘는 자원은 버려지지 않고 마차가 먼저 실어 갑니다.",
            "농장·대장간은 한 노선에만 속합니다. <b>요새</b>와 <b>창고</b>는 여러 노선이 함께 들를 수 있습니다(개수 제한 없음). 짐을 넘겨받는 환승은 창고에서만 됩니다. 마차는 창고에 오면 <b>다음 창고까지 필요한 만큼만</b> 싣습니다. 지나갈 요새들의 빈 칸만큼, 그리고 다음 창고가 이 창고보다 더 모자라면 그 차이만큼입니다. 창고 칸의 점선 ○△는 목표량까지 모자란 칸, 요새 칸의 점선은 마차가 가져오는 중인 칸입니다.",
        };

        private const string Tail = "몬스터는 붉은 점선의 침투로로만 옵니다. 새 침투로는 약하게 시작해 웨이브마다 위협이 커집니다. 침투로는 {route}웨이브마다, 개척지는 {expand}웨이브마다 늘어납니다.";

        // 도움말의 수치를 지금 밸런스 값으로 채움
        private static string Fill(string text, Balance balance) => text
            .Replace("{food}", HudView.FormatNumber(balance.FORT_FOOD_EVERY))
            .Replace("{up}", HudView.FormatNumber(balance.MORALE_UP * 100f))
            .Replace("{down}", HudView.FormatNumber(balance.MORALE_DOWN * 100f))
            .Replace("{route}", HudView.FormatNumber(balance.ROUTE_EVERY))
            .Replace("{expand}", Game.ExpandEvery.ToString());

        [SerializeField] private RectTransform _card, _content;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private Text _title, _tail;
        [SerializeField] private Text[] _items;
        [SerializeField] private UiButton _close;

        /// <summary>닫기 버튼을 눌렀을 때 호출됩니다.</summary>
        public Action OnClose;

        /// <summary>열려 있는지 여부입니다.</summary>
        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            _close.Button.onClick.AddListener(() => OnClose?.Invoke());
        }

        /// <summary>도움말을 보여줍니다.</summary>
        public void Show(Balance balance, float canvasWidth, float canvasHeight)
        {
            // 글자 높이를 재려면 켜져 있어야 함
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            float cardWidth = Mathf.Min(480f, canvasWidth - 32f);
            float inner = cardWidth - 4f - Padding * 2f;
            float y = Padding;
            y += Put(_title, null, inner, y) + 10f;
            int itemCount = Mathf.Min(_items.Length, s_items.Length);
            for (int i = 0; i < itemCount; i++)
            {
                y += Put(_items[i], $"{i + 1}. {Fill(s_items[i], balance)}", inner, y) + 6f;
            }

            y += 6f;
            y += Put(_tail, Fill(Tail, balance), inner, y) + 10f;
            var closePosition = new Vector2(Padding, -y);
            if (_close.RectTransform.anchoredPosition != closePosition)
            {
                _close.RectTransform.anchoredPosition = closePosition;
            }

            float contentHeight = y + Padding + 38f;
            var contentSize = new Vector2(0f, contentHeight);
            if (_content.sizeDelta != contentSize)
            {
                _content.sizeDelta = contentSize;
            }
            // 열 때마다 맨 위에서 시작함
            if (_content.anchoredPosition != Vector2.zero)
            {
                _content.anchoredPosition = Vector2.zero;
            }

            _scroll.StopMovement();

            var card = new Vector2(cardWidth, Mathf.Min(contentHeight + 4f, canvasHeight - 32f));
            if (_card.sizeDelta != card)
            {
                _card.sizeDelta = card;
            }
        }

        // 폭을 먼저 맞춘 뒤 높이를 재서 놓고, 차지한 높이를 돌려줌
        private static float Put(Text label, string text, float width, float y)
        {
            if (text != null && label.text != text)
            {
                label.text = text;
            }

            var rectTransform = label.rectTransform;
            var sizeDelta = rectTransform.sizeDelta;
            if (sizeDelta.x != width)
            {
                sizeDelta.x = width;
                rectTransform.sizeDelta = sizeDelta;
            }
            float height = Mathf.Ceil(label.preferredHeight);
            if (sizeDelta.y != height)
            {
                sizeDelta.y = height;
                rectTransform.sizeDelta = sizeDelta;
            }
            var position = new Vector2(Padding, -y);
            if (rectTransform.anchoredPosition != position)
            {
                rectTransform.anchoredPosition = position;
            }

            return height;
        }

        /// <summary>오버레이를 숨깁니다.</summary>
        public void Hide() => gameObject.SetActive(false);
    }
}
