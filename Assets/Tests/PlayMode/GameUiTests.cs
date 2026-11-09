using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LineDefense.View;
using LineDefense.View.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace LineDefense.Tests
{
    /// <summary>
    /// Boot로 게임을 띄운 뒤, 화면의 버튼이 포인터를 받는지와 누르면 동작하는지 확인합니다.
    /// 클릭은 EventSystem 레이캐스트로 맨 위에 맞은 물체에 보내므로 실제 입력과 같은 길을 지납니다.
    /// </summary>
    public sealed class GameUiTests
    {
        private const float BootTimeoutSeconds = 15f;
        private const string HelpButtonText = "도움말";

        private GameObject _boot;
        private UiRoot _ui;

        [UnitySetUp]
        public IEnumerator StartGame()
        {
            _boot = new GameObject(nameof(Boot), typeof(Boot));
            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while ((_ui = Object.FindAnyObjectByType<UiRoot>()) == null || EventSystem.current == null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Game prefab did not load.");
                yield return null;
            }

            // 첫 프레임의 배치가 끝난 뒤 확인함
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator DestroyGame()
        {
            Object.Destroy(_boot);
            if (_ui != null)
            {
                Object.Destroy(_ui.transform.root.gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator VisibleButtonsReceivePointer()
        {
            var buttons = Object.FindObjectsByType<UiButton>(FindObjectsSortMode.None)
                .Where(button => button.isActiveAndEnabled)
                .ToList();
            Assert.That(buttons, Is.Not.Empty);

            var blocked = new List<string>();
            foreach (var button in buttons)
            {
                var top = TopHitAt(CenterOnScreen(button.RectTransform));
                if (top == null || !top.transform.IsChildOf(button.transform))
                {
                    blocked.Add($"{button.name} -> {(top == null ? "nothing" : top.name)}");
                }
            }

            Assert.That(blocked, Is.Empty);
            yield break;
        }

        [UnityTest]
        public IEnumerator HelpButtonOpensHelp()
        {
            var helpButton = Object.FindObjectsByType<UiButton>(FindObjectsSortMode.None)
                .FirstOrDefault(button => button.isActiveAndEnabled && button.Label != null && button.Label.text == HelpButtonText);
            Assert.That(helpButton, Is.Not.Null, "Help button not found.");
            Assert.That(_ui.Help.IsOpen, Is.False);

            ClickAt(CenterOnScreen(helpButton.RectTransform));
            yield return null;

            Assert.That(_ui.Help.IsOpen, Is.True);
        }

        private static Vector2 CenterOnScreen(RectTransform rectTransform) =>
            RectTransformUtility.WorldToScreenPoint(null, rectTransform.TransformPoint(rectTransform.rect.center));

        private static GameObject TopHitAt(Vector2 screenPosition)
        {
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPosition }, results);
            return results.Count > 0 ? results[0].gameObject : null;
        }

        private static void ClickAt(Vector2 screenPosition)
        {
            var target = TopHitAt(screenPosition);
            Assert.That(target, Is.Not.Null, $"Nothing receives the pointer at {screenPosition}.");
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = screenPosition,
                button = PointerEventData.InputButton.Left,
            };
            ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
