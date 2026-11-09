using UnityEngine;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>마굿간·창고 설정창의 칸 미리보기 한 칸입니다. 풀에서 재사용하며 프리팹 Prefabs/UI/Items/ConfigCell에 붙습니다.</summary>
    public sealed class ConfigCell : MonoBehaviour
    {
        [SerializeField] Image _img;

        /// <summary>위치, 크기, 모양, 색을 지정하고 보이게 합니다.</summary>
        public void Show(Vector2 pos, float size, Sprite sprite, Color color)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            var rt = (RectTransform)transform;
            if (rt.anchoredPosition != pos) rt.anchoredPosition = pos;
            var s = new Vector2(size, size);
            if (rt.sizeDelta != s) rt.sizeDelta = s;
            if (_img.sprite != sprite) _img.sprite = sprite;
            if (_img.color != color) _img.color = color;
        }

        /// <summary>풀로 돌려보냅니다.</summary>
        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
