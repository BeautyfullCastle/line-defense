using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>몬스터 그림입니다. 몸체는 위협 수준에 따라 크기가 바뀌고, 다친 몬스터는 체력 막대를 보입니다.</summary>
    public sealed class MonsterView : MonoBehaviour
    {
        private const float HpBarWidth = 20f, HpBarHeight = 3f, SquareSide = 4f;

        [SerializeField] private SpriteRenderer _body, _leftEye, _rightEye, _hpBack, _hpFill;

        internal int Stamp;

        public void Sync(Monster monster)
        {
            transform.localPosition = new Vector3(monster.Position.X, -(monster.Position.Y + Mathf.Sin(monster.BobPhase) * 1.5f), 0f);
            float bodyScale = monster.Size > 0f ? monster.Size : 1f;
            _body.transform.localScale = new Vector3(bodyScale, bodyScale, 1f);
            _body.color = Palette.WithAlpha(monster.HitFlash > 0f ? Palette.Danger : Palette.Monster, monster.Alpha);
            var eyeColor = Palette.WithAlpha(Palette.Ground, monster.Alpha);
            _leftEye.color = eyeColor;
            _rightEye.color = eyeColor;

            bool hurt = monster.Hp < monster.MaxHp;
            if (_hpBack.gameObject.activeSelf != hurt)
            {
                _hpBack.gameObject.SetActive(hurt);
                _hpFill.gameObject.SetActive(hurt);
            }
            if (!hurt)
            {
                return;
            }

            _hpBack.color = Palette.WithAlpha(Palette.Line, monster.Alpha);
            float fillWidth = HpBarWidth * Mathf.Clamp01(monster.Hp / monster.MaxHp);
            // 막대는 왼쪽 끝(-10)에 붙여 줄어듦
            _hpFill.transform.localPosition = new Vector3(-HpBarWidth / 2f + fillWidth / 2f, 17f - HpBarHeight / 2f, 0f);
            _hpFill.transform.localScale = new Vector3(fillWidth / SquareSide, HpBarHeight / SquareSide, 1f);
            _hpFill.color = Palette.WithAlpha(Palette.Danger, monster.Alpha);
        }
    }
}
