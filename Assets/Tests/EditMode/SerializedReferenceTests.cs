using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LineDefense.Tests
{
    /// <summary>
    /// 프리팹과 에셋의 직렬화 참조가 비지 않았는지 확인합니다.
    /// 필드 이름을 바꾸고 프리팹의 키를 옮기지 않으면 참조가 비므로 이 테스트가 잡습니다.
    /// </summary>
    public sealed class SerializedReferenceTests
    {
        private const string GamePrefabPath = "Assets/Game/Contents/Game.prefab";
        private const string WorldArtPath = "Assets/Art/WorldArt.asset";

        // 게임 루트에 중첩되지 않고 실행 중에 따로 만들어지는 프리팹
        private static IEnumerable<string> RuntimePrefabPaths() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/World", "Assets/Prefabs/UI/Items" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path);

        [Test]
        public void GamePrefabHasNoEmptyReferences()
        {
            AssertNoEmptyReferencesInPrefab(GamePrefabPath);
        }

        [TestCaseSource(nameof(RuntimePrefabPaths))]
        public void RuntimePrefabHasNoEmptyReferences(string path)
        {
            AssertNoEmptyReferencesInPrefab(path);
        }

        [Test]
        public void WorldArtHasNoEmptyReferences()
        {
            var worldArt = AssetDatabase.LoadAssetAtPath<ScriptableObject>(WorldArtPath);
            Assert.That(worldArt, Is.Not.Null, WorldArtPath);
            Assert.That(EmptyReferences(worldArt), Is.Empty);
        }

        private static void AssertNoEmptyReferencesInPrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var empty = root.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(component => component != null && IsProjectType(component))
                    .SelectMany(EmptyReferences)
                    .ToList();
                Assert.That(empty, Is.Empty, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool IsProjectType(Object target) => target.GetType().Namespace?.StartsWith("LineDefense") == true;

        private static IEnumerable<string> EmptyReferences(Object target)
        {
            var property = new SerializedObject(target).GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference
                    && property.objectReferenceValue == null
                    && !property.propertyPath.StartsWith("m_"))
                {
                    yield return $"{target.GetType().Name}.{property.propertyPath} on {target.name}";
                }
            }
        }
    }
}
