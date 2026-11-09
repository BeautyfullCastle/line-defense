using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>
    /// 침투로 끝의 몬스터 굴과 침공 예고 화살표입니다. 루트는 성벽 원의 중심(0, RW)에 두고,
    /// 침투로 각도만큼 돌려 자식을 원 바깥쪽에 놓습니다.
    /// </summary>
    public sealed class DenView : MonoBehaviour
    {
        private const float RingR = 20f;

        [SerializeField] private Transform _denRoot;
        [SerializeField] private SpriteRenderer _body, _mouth, _fresh;
        [SerializeField] private SpriteRenderer[] _arrows;

        public void Sync(Route route, float t, bool countdown)
        {
            float degrees = -route.Angle * Mathf.Rad2Deg;
            transform.localPosition = new Vector3(0f, -Game.WallRadius, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
            // 굴 그림은 회전하지 않음
            _denRoot.localPosition = new Vector3(Game.WallRadius + Game.DenDistance + 14f, 0f, 0f);
            _denRoot.rotation = Quaternion.identity;
            _body.color = Palette.Monster;
            _mouth.color = Palette.Outside;

            float fresh = Mathf.Max(0f, 1f - (t - route.Born) / 6f);
            bool on = fresh > 0f;
            if (_fresh.gameObject.activeSelf != on)
            {
                _fresh.gameObject.SetActive(on);
            }

            if (on)
            {
                _fresh.transform.localScale = Vector3.one * ((18f + (1f - fresh) * 14f) / RingR);
                _fresh.color = Palette.WithAlpha(Palette.Danger, fresh);
            }

            float pulse = 0.45f + 0.45f * Mathf.Sin(t * 5f);
            for (int k = 0; k < _arrows.Length; k++)
            {
                var spriteRenderer = _arrows[k];
                if (spriteRenderer.gameObject.activeSelf != countdown)
                {
                    spriteRenderer.gameObject.SetActive(countdown);
                }

                if (!countdown)
                {
                    continue;
                }

                spriteRenderer.transform.localPosition = new Vector3(Game.WallRadius + 34f + k * 16f, 0f, 0f);
                spriteRenderer.color = Palette.WithAlpha(Palette.Danger, pulse * (1f - k * 0.25f));
            }
        }
    }
}
