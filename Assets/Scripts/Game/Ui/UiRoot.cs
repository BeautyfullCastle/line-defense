using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View.Ui
{
    /// <summary>
    /// UI 뷰들을 묶고 화면 크기에 맞춰 배치합니다. 프리팹 Prefabs/UI/Ui에 붙습니다.
    /// 캔버스는 바뀌는 빈도로 나눕니다. 월드 글자 캔버스와 UI 캔버스가 있고, UI 캔버스 아래에
    /// 머리말·꼬리말·패널·팝업·연출 하위 캔버스를 둬서 한 곳이 바뀌어도 그 하위 캔버스만 다시 그립니다.
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private HudView _hud;
        [SerializeField] private FooterView _footer;
        [SerializeField] private ConfigPanel _config;
        [SerializeField] private CheatPanel _cheat;
        [SerializeField] private DialogueView _dialogue;
        [SerializeField] private ToastView _toast;
        [SerializeField] private RewardView _reward;
        [SerializeField] private RewardBannerView _banner;
        [SerializeField] private HelpView _help;
        [SerializeField] private OverView _over;
        [SerializeField] private FlyLayer _fly;
        [SerializeField] private WorldLabels _labels;

        private float _width, _height, _scaleFactor;

        public HudView Hud => _hud;
        public FooterView Footer => _footer;
        public ConfigPanel Config => _config;
        public CheatPanel Cheat => _cheat;
        public DialogueView Dialogue => _dialogue;
        public ToastView Toast => _toast;
        public RewardView Reward => _reward;
        public RewardBannerView RewardBanner => _banner;
        public HelpView Help => _help;
        public OverView Over => _over;
        public FlyLayer Fly => _fly;
        public WorldLabels Labels => _labels;

        /// <summary>캔버스 가로 크기(캔버스 단위)입니다.</summary>
        public float CanvasWidth { get; private set; } = 1280f;

        /// <summary>캔버스 세로 크기(캔버스 단위)입니다.</summary>
        public float CanvasHeight { get; private set; } = 720f;

        /// <summary>머리말이 차지하는 화면 픽셀 높이입니다.</summary>
        public float HeaderPixels
        {
            get; private set;
        }

        /// <summary>꼬리말이 차지하는 화면 픽셀 높이입니다.</summary>
        public float FooterPixels
        {
            get; private set;
        }

        /// <summary>머리말 높이(캔버스 단위)입니다.</summary>
        public float HeaderUnits
        {
            get; private set;
        }

        /// <summary>캔버스 단위 하나가 차지하는 화면 픽셀 수입니다.</summary>
        public float ScaleFactor => _canvas.scaleFactor;

        /// <summary>뷰 사이 연결을 맺습니다. 다른 컴포넌트의 Awake 순서에 기대지 않도록 GameController가 먼저 부릅니다.</summary>
        public void Init()
        {
            _fly.Init(Resolve);
            _footer.Pending = kind => _fly.Pending(kind == BuildKind.Stable ? FlyTarget.Stable
                : kind == BuildKind.Fort ? FlyTarget.Fort : FlyTarget.Warehouse);
        }

        private RectTransform Resolve(FlyTarget target)
        {
            switch (target)
            {
                case FlyTarget.Gold:
                    return _hud.GoldRect;
                case FlyTarget.Wall:
                    return _hud.WallRect;
                case FlyTarget.Stable:
                    return _footer.CountRect(BuildKind.Stable);
                case FlyTarget.Fort:
                    return _footer.CountRect(BuildKind.Fort);
                case FlyTarget.Warehouse:
                    return _footer.CountRect(BuildKind.Warehouse);
                default:
                    return _footer.InfoRect;
            }
        }

        /// <summary>화면 크기나 꼬리말 내용이 바뀌었을 때만 머리말, 꼬리말과 떠 있는 패널을 다시 배치합니다.</summary>
        public void UpdateLayout()
        {
            var rectTransform = (RectTransform)_canvas.transform;
            float width = rectTransform.rect.width, height = rectTransform.rect.height, scaleFactor = _canvas.scaleFactor;
            if (width < 10f || height < 10f)
            {
                return;
            }

            bool resized = !Mathf.Approximately(width, _width) || !Mathf.Approximately(height, _height) || !Mathf.Approximately(scaleFactor, _scaleFactor);
            if (!resized && !_footer.NeedsLayout)
            {
                return;
            }

            _width = width;
            _height = height;
            _scaleFactor = scaleFactor;
            CanvasWidth = width;
            CanvasHeight = height;
            HeaderUnits = _hud.Layout(width);
            float footerHeight = _footer.Layout(width);
            HeaderPixels = HeaderUnits * scaleFactor;
            FooterPixels = footerHeight * scaleFactor;
            _config.Relayout(width, HeaderUnits);
            _dialogue.Relayout(width, footerHeight);
            _toast.SetTop(HeaderUnits);
            _cheat.SetTop(HeaderUnits);
        }
    }
}
