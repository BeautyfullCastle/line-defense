using LineDefense.Core;

namespace LineDefense.View
{
    /// <summary>시뮬레이션에는 없고 화면에만 있는 상태(모드, 드래그, 호버, 카메라 보간)입니다.</summary>
    public sealed class ViewState
    {
        /// <summary>보간 중인 개척지 반폭입니다.</summary>
        public float DisplayHalfWidth;

        /// <summary>보간 중인 개척지 깊이입니다.</summary>
        public float DisplayDepth;

        /// <summary>건설 모드 여부입니다.</summary>
        public bool HasMode;

        /// <summary>선택한 건설 종류입니다.</summary>
        public BuildKind Mode;

        /// <summary>노선 끝을 끌고 있는지 여부입니다.</summary>
        public bool Dragging;

        public Line DragLine;
        public LineEnd DragEnd;
        public Vec2 DragPosition;

        /// <summary>노선 구간을 끌어 정거장을 끼워 넣는 중이면 그 구간 번호입니다. 끝을 끌 때는 -1입니다.</summary>
        public int DragSegment = -1;

        /// <summary>끌고 있는 위치 아래의 노드입니다.</summary>
        public Node DragTarget;

        /// <summary>포인터가 캔버스 위에 있는지 여부입니다.</summary>
        public bool HoverInside;

        public Vec2 Hover;

        /// <summary>설정창이 열린 노드입니다.</summary>
        public Node ConfigNode;

        /// <summary>건설 모드와 드래그와 설정창 선택을 모두 해제합니다.</summary>
        public void ClearSelection()
        {
            HasMode = false;
            Dragging = false;
            DragLine = null;
            DragSegment = -1;
            DragTarget = null;
            ConfigNode = null;
        }
    }
}
