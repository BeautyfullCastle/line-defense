using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>건설 모드에서 커서 위치에 놓일 건물을 반투명하게 보입니다.</summary>
    public sealed class GhostView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _sprite;
        [SerializeField] private Sprite _fort, _warehouse, _stable;

        /// <summary>건물 종류, 위치(y 아래 방향 월드), 색을 반영합니다.</summary>
        public void Show(BuildKind kind, float x, float y, Color color)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            transform.localPosition = new Vector3(x, -y, 0f);
            var spriteTransform = _sprite.transform;
            if (kind == BuildKind.Fort)
            {
                _sprite.drawMode = SpriteDrawMode.Simple;
                _sprite.sprite = _fort;
                spriteTransform.localPosition = Vector3.zero;
                spriteTransform.localScale = Vector3.one * (15f / 16f);
            }
            else if (kind == BuildKind.Warehouse)
            {
                _sprite.drawMode = SpriteDrawMode.Simple;
                _sprite.sprite = _warehouse;
                spriteTransform.localPosition = Vector3.zero;
                spriteTransform.localScale = Vector3.one;
            }
            else
            {
                // 마굿간 몸체 자리(-12, -8, 24×19)
                _sprite.sprite = _stable;
                _sprite.drawMode = SpriteDrawMode.Sliced;
                _sprite.size = new Vector2(24f, 19f);
                spriteTransform.localPosition = new Vector3(0f, -1.5f, 0f);
                spriteTransform.localScale = Vector3.one;
            }
            _sprite.color = Palette.WithAlpha(color, 0.55f);
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
