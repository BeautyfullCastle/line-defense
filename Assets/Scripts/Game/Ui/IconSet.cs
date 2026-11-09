using System;
using System.Collections.Generic;
using UnityEngine;

namespace LineDefense.View.Ui
{
    /// <summary>UI 아이콘 스프라이트 모음입니다. 보급·자원 키로 찾고, 없는 키는 Fallback을 돌려줍니다.</summary>
    [CreateAssetMenu(menuName = "Line Defense/Icon Set")]
    public sealed class IconSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Key;
            public Sprite Sprite;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        [SerializeField] private Sprite _fallback;

        private Dictionary<string, Sprite> _spriteByKey;

        /// <summary>키에 맞는 흰색 아이콘입니다. 색은 Image.color로 입힙니다.</summary>
        public Sprite Get(string key)
        {
            if (_spriteByKey == null)
            {
                _spriteByKey = new Dictionary<string, Sprite>();
                foreach (var entry in _entries)
                {
                    _spriteByKey[entry.Key] = entry.Sprite;
                }
            }
            return key != null && _spriteByKey.TryGetValue(key, out var sprite) ? sprite : _fallback;
        }
    }
}
