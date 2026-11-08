using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>개척지 크기에 맞춰 직교 카메라를 맞추고 화면과 월드 좌표를 변환합니다.</summary>
    public sealed class CameraFit
    {
        float _camY;

        /// <summary>제어 대상 카메라입니다.</summary>
        public Camera Cam { get; }

        /// <summary>월드 한 단위당 화면 픽셀 수입니다.</summary>
        public float S { get; private set; } = 1f;

        /// <summary>화면 가로 픽셀입니다.</summary>
        public float W { get; private set; } = 1f;

        /// <summary>화면 세로 픽셀입니다.</summary>
        public float H { get; private set; } = 1f;

        /// <summary>화면에 보이는 월드 가로 반폭입니다.</summary>
        public float HalfW => W / (2f * S);

        /// <summary>머리말 바로 아래에 보이는 월드 y(아래 방향 좌표)입니다.</summary>
        public float WorldTop { get; private set; }

        /// <summary>꼬리말 바로 위에 보이는 월드 y(아래 방향 좌표)입니다.</summary>
        public float WorldBottom { get; private set; }

        /// <summary>카메라를 설정하고 배경색을 지정합니다.</summary>
        public CameraFit(Camera cam)
        {
            Cam = cam;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Outside;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
        }

        /// <summary>현재 개척 크기와 UI 여백(픽셀)으로 카메라를 갱신합니다.</summary>
        public void Update(float hw, float d, float topPx, float bottomPx)
        {
            W = Mathf.Max(1, Screen.width);
            H = Mathf.Max(1, Screen.height);
            float stageH = Mathf.Max(1f, H - topPx - bottomPx);
            float topW = Game.DEN + 50f, botW = d + 24f;
            S = Mathf.Min(W / (2f * (hw + 30f)), stageH / (topW + botW));
            float cy = (-topW + botW) * 0.5f;
            float dy = bottomPx + stageH * 0.5f - H * 0.5f;
            _camY = -cy - dy / S;
            Cam.orthographicSize = H / (2f * S);
            Cam.transform.position = new Vector3(0f, _camY, -10f);
            WorldTop = -(_camY + (H - topPx - H * 0.5f) / S);
            WorldBottom = -(_camY + (bottomPx - H * 0.5f) / S);
        }

        /// <summary>화면 픽셀(왼쪽 아래 원점)을 월드(y 아래 방향) 좌표로 바꿉니다.</summary>
        public Vec2 ScreenToWorld(Vector2 sp)
        {
            float ux = (sp.x - W * 0.5f) / S;
            float uy = (sp.y - H * 0.5f) / S + _camY;
            return new Vec2(ux, -uy);
        }

        /// <summary>월드(y 아래 방향) 좌표를 화면 픽셀로 바꿉니다.</summary>
        public Vector2 WorldToScreen(float x, float y)
        {
            return new Vector2(x * S + W * 0.5f, (-y - _camY) * S + H * 0.5f);
        }
    }
}
