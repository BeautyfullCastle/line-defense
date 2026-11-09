using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>정거장 하나의 그림입니다. 루트 위치와 등장 배율은 WorldSprites가 맞추고, 내용은 Sync가 그립니다.</summary>
    public abstract class NodeView : MonoBehaviour
    {
        /// <summary>그리는 정거장입니다.</summary>
        public Node Node { get; private set; }

        /// <summary>마지막으로 쓰인 프레임 번호입니다. WorldSprites가 쓰지 않는 그림을 지울 때 씁니다.</summary>
        internal int Stamp;

        public void Bind(Node n) => Node = n;

        /// <summary>정거장 상태를 그림에 반영합니다. t는 게임 시간입니다.</summary>
        public abstract void Sync(Game g, ViewState v, float t);

        /// <summary>y 아래 방향 월드 좌표를 Unity 로컬 좌표로 바꿉니다.</summary>
        protected static Vector3 P(float x, float y) => new Vector3(x, -y, 0f);

        /// <summary>펄스 고리(창고·요새가 짐을 받을 때)를 그리거나 숨깁니다. 고리 스프라이트 반지름은 20입니다.</summary>
        protected static void Pulse(SpriteRenderer ring, float pulse)
        {
            bool on = pulse > 0f;
            if (ring.gameObject.activeSelf != on) ring.gameObject.SetActive(on);
            if (!on) return;
            float r = 18f + (1f - pulse * 2f) * 10f;
            ring.transform.localScale = Vector3.one * (r / 20f);
            ring.color = Palette.WithAlpha(Palette.Ink, Mathf.Min(1f, pulse));
        }

        /// <summary>설정 창이 열린 정거장에 점선 고리를 보입니다.</summary>
        protected void Cfg(SpriteRenderer ring, ViewState v)
        {
            bool on = v.CfgNode == Node;
            if (ring.gameObject.activeSelf != on) ring.gameObject.SetActive(on);
        }
    }
}
