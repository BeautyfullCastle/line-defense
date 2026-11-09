using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>개척지 크기에 맞춰 직교 카메라를 맞추고 화면과 월드 좌표를 변환합니다.</summary>
    public sealed class CameraFit
    {
        private float _cameraY;

        /// <summary>제어 대상 카메라입니다.</summary>
        public Camera Camera
        {
            get;
        }

        /// <summary>월드 한 단위당 화면 픽셀 수입니다.</summary>
        public float PixelsPerUnit { get; private set; } = 1f;

        /// <summary>화면 가로 픽셀입니다.</summary>
        public float ScreenWidth { get; private set; } = 1f;

        /// <summary>화면 세로 픽셀입니다.</summary>
        public float ScreenHeight { get; private set; } = 1f;

        /// <summary>화면에 보이는 월드 가로 반폭입니다.</summary>
        public float HalfWidth => ScreenWidth / (2f * PixelsPerUnit);

        /// <summary>머리말 바로 아래에 보이는 월드 y(아래 방향 좌표)입니다.</summary>
        public float WorldTop
        {
            get; private set;
        }

        /// <summary>꼬리말 바로 위에 보이는 월드 y(아래 방향 좌표)입니다.</summary>
        public float WorldBottom
        {
            get; private set;
        }

        /// <summary>카메라를 설정하고 배경색을 지정합니다.</summary>
        public CameraFit(Camera camera)
        {
            Camera = camera;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Outside;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
        }

        /// <summary>현재 개척 크기와 UI 여백(픽셀)으로 카메라를 갱신합니다.</summary>
        public void Update(float halfWidth, float depth, float topPixels, float bottomPixels)
        {
            ScreenWidth = Mathf.Max(1, Screen.width);
            ScreenHeight = Mathf.Max(1, Screen.height);
            float stageHeight = Mathf.Max(1f, ScreenHeight - topPixels - bottomPixels);
            float worldAbove = Game.DenDistance + 50f, worldBelow = depth + 24f;
            PixelsPerUnit = Mathf.Min(ScreenWidth / (2f * (halfWidth + 30f)), stageHeight / (worldAbove + worldBelow));
            float centerY = (-worldAbove + worldBelow) * 0.5f;
            float dy = bottomPixels + stageHeight * 0.5f - ScreenHeight * 0.5f;
            _cameraY = -centerY - dy / PixelsPerUnit;
            Camera.orthographicSize = ScreenHeight / (2f * PixelsPerUnit);
            Camera.transform.position = new Vector3(0f, _cameraY, -10f);
            WorldTop = -(_cameraY + (ScreenHeight - topPixels - ScreenHeight * 0.5f) / PixelsPerUnit);
            WorldBottom = -(_cameraY + (bottomPixels - ScreenHeight * 0.5f) / PixelsPerUnit);
        }

        /// <summary>화면 픽셀(왼쪽 아래 원점)을 월드(y 아래 방향) 좌표로 바꿉니다.</summary>
        public Vec2 ScreenToWorld(Vector2 screenPosition)
        {
            float worldX = (screenPosition.x - ScreenWidth * 0.5f) / PixelsPerUnit;
            float upwardY = (screenPosition.y - ScreenHeight * 0.5f) / PixelsPerUnit + _cameraY;
            return new Vec2(worldX, -upwardY);
        }

        /// <summary>월드(y 아래 방향) 좌표를 화면 픽셀로 바꿉니다.</summary>
        public Vector2 WorldToScreen(float x, float y)
        {
            return new Vector2(x * PixelsPerUnit + ScreenWidth * 0.5f, (-y - _cameraY) * PixelsPerUnit + ScreenHeight * 0.5f);
        }
    }
}
