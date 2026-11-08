using UnityEngine;

namespace LineDefense.View
{
    /// <summary>빈 씬이 로드된 뒤 게임 컨트롤러를 코드로 만드는 진입점입니다.</summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindFirstObjectByType<GameController>() != null) return;
            var go = new GameObject("LineDefense");
            go.AddComponent<GameController>();
        }
    }
}
