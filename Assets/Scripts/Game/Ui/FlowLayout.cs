using System;
using System.Collections.Generic;
using UnityEngine;

namespace LineDefense.View.Ui
{
    /// <summary>
    /// 자식을 왼쪽에서 오른쪽으로 놓고 폭을 넘으면 줄바꿈하는 배치 컴포넌트입니다.
    /// 항목 순서와 줄바꿈 위치는 프리팹에 저장하고, 실행 중에 바꿀 수도 있습니다.
    /// 화면 폭이 바뀌거나 항목이 바뀌었을 때만 Layout을 부릅니다.
    /// </summary>
    public sealed class FlowLayout : MonoBehaviour
    {
        [Serializable]
        public struct Item
        {
            public RectTransform Rt;
            public bool BreakBefore;
        }

        [SerializeField] List<Item> _items = new List<Item>();

        /// <summary>항목을 끝에 추가합니다. 크기는 RectTransform.sizeDelta를 씁니다.</summary>
        public void Add(RectTransform rt, bool breakBefore = false)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            _items.Add(new Item { Rt = rt, BreakBefore = breakBefore });
        }

        /// <summary>항목을 모두 비웁니다. 오브젝트는 파괴하지 않습니다.</summary>
        public void Clear() => _items.Clear();

        /// <summary>폭에 맞춰 배치하고 필요한 높이를 돌려줍니다.</summary>
        public float Layout(float width, float padX, float padY, float gapX, float gapY)
        {
            float x = padX, y = padY, rowH = 0f, avail = width - padX;
            foreach (var it in _items)
            {
                if (!it.Rt.gameObject.activeSelf) continue;
                var s = it.Rt.sizeDelta;
                bool wrap = it.BreakBefore || (x > padX && x + s.x > avail);
                if (wrap && rowH > 0f)
                {
                    y += rowH + gapY;
                    x = padX;
                    rowH = 0f;
                }
                it.Rt.anchoredPosition = new Vector2(x, -y);
                x += s.x + gapX;
                rowH = Mathf.Max(rowH, s.y);
            }
            return y + rowH + padY;
        }
    }
}
