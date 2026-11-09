using System;
using H.ContentsLoader;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>
    /// Main 씬의 시작점입니다. 콘텐츠 팩을 받아 둔 뒤 Contents의 Game 프리팹을 만듭니다.
    /// WebGL은 팩을 웹 주소에서 받아야 하므로, 모든 플랫폼에서 받은 다음에 동기 로드를 씁니다.
    /// </summary>
    public sealed class Boot : MonoBehaviour
    {
        /// <summary>Contents 폴더 기준 게임 루트 프리팹 경로입니다.</summary>
        public const string GamePath = "Game";

        private async void Start()
        {
            try
            {
                await Content.Download(string.Empty);
                var prefab = Content.Load<GameObject>(GamePath);
                Instantiate(prefab).name = prefab.name;
            }
            catch (Exception e)
            {
                // async void에서 놓친 예외는 사라지므로 직접 남김
                Debug.LogException(e);
            }
        }
    }
}
