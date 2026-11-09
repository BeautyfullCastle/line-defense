namespace LineDefense.Core
{
    public enum GameEventType { Shot, Kill, Burst, Float, Bark, Say, Toast, WaveStart, RewardReady, GameOver, NodePlaced, WallHit }

    /// <summary>표현 계층이 소비하는 일회성 이벤트입니다.</summary>
    public struct GameEvent
    {
        public GameEventType Type;
        public Vec2 A, B;
        public string Text;
        public string Key;
        /// <summary>[화자, 대사] 쌍의 배열입니다. 화자는 gen, aide, msg, narr 중 하나입니다.</summary>
        public string[][] Lines;
        /// <summary>true면 진행을 막는 대화입니다.</summary>
        public bool Block;
        public int NodeId;
        /// <summary>Kill이면 받은 골드, WallHit이면 성벽 피해량입니다.</summary>
        public float Amount;
    }
}
