using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>몬스터 그림입니다. 몸체는 위협 수준에 따라 크기가 바뀌고, 다친 몬스터는 체력 막대를 보입니다.</summary>
    public sealed class MonsterView : MonoBehaviour
    {
        const float HpW = 20f, HpH = 3f, SquareSide = 4f;

        [SerializeField] SpriteRenderer _body, _eyeL, _eyeR, _hpBack, _hpFill;

        internal int Stamp;

        public void Sync(Monster m)
        {
            transform.localPosition = new Vector3(m.Pos.x, -(m.Pos.y + Mathf.Sin(m.Bob) * 1.5f), 0f);
            float sz = m.Size > 0f ? m.Size : 1f;
            _body.transform.localScale = new Vector3(sz, sz, 1f);
            _body.color = Palette.WithAlpha(m.Hit > 0f ? Palette.Danger : Palette.Monster, m.Alpha);
            var eye = Palette.WithAlpha(Palette.Ground, m.Alpha);
            _eyeL.color = eye;
            _eyeR.color = eye;

            bool hurt = m.Hp < m.MaxHp;
            if (_hpBack.gameObject.activeSelf != hurt)
            {
                _hpBack.gameObject.SetActive(hurt);
                _hpFill.gameObject.SetActive(hurt);
            }
            if (!hurt) return;
            _hpBack.color = Palette.WithAlpha(Palette.Line, m.Alpha);
            float w = HpW * Mathf.Clamp01(m.Hp / m.MaxHp);
            // 막대는 왼쪽 끝(-10)에 붙여 줄어듦
            _hpFill.transform.localPosition = new Vector3(-HpW / 2f + w / 2f, 17f - HpH / 2f, 0f);
            _hpFill.transform.localScale = new Vector3(w / SquareSide, HpH / SquareSide, 1f);
            _hpFill.color = Palette.WithAlpha(Palette.Danger, m.Alpha);
        }
    }
}
