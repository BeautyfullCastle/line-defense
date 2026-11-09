using UnityEngine;

namespace LineDefense.View
{
    /// <summary>
    /// 월드 그림에 쓰는 프리팹과 스프라이트 모음입니다. 아트를 바꿀 때는 이 에셋이 가리키는 파일을 바꿉니다.
    /// 스프라이트는 흰색으로 그려 두고 SpriteRenderer.color로 색을 입힙니다.
    /// </summary>
    [CreateAssetMenu(menuName = "Line Defense/World Art")]
    public sealed class WorldArt : ScriptableObject
    {
        [Header("프리팹")]
        public StableView Stable;
        public ProducerView Farm;
        public ProducerView Forge;
        public WarehouseView Warehouse;
        public FortView Fort;
        public CartView Cart;
        public MonsterView Monster;
        public DenView Den;
        public GhostView Ghost;
        public SpriteRenderer Pip;

        [Header("자원 칸 (반지름 16 원·삼각형, 반지름 3.3 테두리)")]
        public Sprite Disc;
        public Sprite Triangle;
        public Sprite PipRingCircle;
        public Sprite PipRingTriangle;
        public Sprite PipRequestCircle;
        public Sprite PipRequestTriangle;
        public Sprite PipOverCircle;
        public Sprite PipOverTriangle;

        [Header("기타 도형")]
        public Sprite Square;
        public Sprite Diamond;
        public Sprite Chevrons;
        public Sprite Hex;
        public Sprite Round3;
        public Sprite WarehouseGhost;

        [Header("메시")]
        public Material MeshMaterial;
    }
}
