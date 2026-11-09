using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LineDefense.View.Ui
{
    /// <summary>캔버스, 이벤트 시스템, 모든 UI 뷰를 만들고 화면 크기에 맞춰 배치합니다.</summary>
    public sealed class UiRoot
    {
        float _w, _h, _sf;

        public Canvas Canvas { get; }
        public Canvas LabelCanvas { get; }
        public HudView Hud { get; }
        public FooterView Footer { get; }
        public ConfigPanel Config { get; }
        public CheatPanel Cheat { get; }
        public DialogueView Dialogue { get; }
        public ToastView Toast { get; }
        public RewardView Reward { get; }
        public RewardBannerView RewardBanner { get; }
        public HelpView Help { get; }
        public OverView Over { get; }
        public FlyLayer Fly { get; }

        /// <summary>캔버스 가로 크기(캔버스 단위)입니다.</summary>
        public float CanvasW { get; private set; } = 1280f;

        /// <summary>캔버스 세로 크기(캔버스 단위)입니다.</summary>
        public float CanvasH { get; private set; } = 720f;

        /// <summary>머리말이 차지하는 화면 픽셀 높이입니다.</summary>
        public float HeaderPx { get; private set; }

        /// <summary>꼬리말이 차지하는 화면 픽셀 높이입니다.</summary>
        public float FooterPx { get; private set; }

        /// <summary>머리말 높이(캔버스 단위)입니다.</summary>
        public float HeaderUnits { get; private set; }

        /// <summary>캔버스 단위 하나가 차지하는 화면 픽셀 수입니다.</summary>
        public float ScaleFactor => Canvas.scaleFactor;

        /// <summary>UI 전체를 만듭니다.</summary>
        public UiRoot(Transform parent)
        {
            LabelCanvas = MakeCanvas(parent, "LabelCanvas", 0, false);
            Canvas = MakeCanvas(parent, "UiCanvas", 10, true);
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(parent, false);

            var t = Canvas.transform;
            Hud = new HudView(t);
            Footer = new FooterView(t);
            Config = new ConfigPanel(t);
            Cheat = new CheatPanel(t);
            Dialogue = new DialogueView(t);
            Toast = new ToastView(t);
            Reward = new RewardView(t);
            RewardBanner = new RewardBannerView(t);
            Help = new HelpView(t);
            Over = new OverView(t);
            Fly = new FlyLayer(t, Resolve);
            Footer.Pending = k => Fly.Pending(k == LineDefense.Core.BuildKind.Stable ? FlyTarget.Stable
                : k == LineDefense.Core.BuildKind.Fort ? FlyTarget.Fort : FlyTarget.Warehouse);
        }

        RectTransform Resolve(FlyTarget t)
        {
            switch (t)
            {
                case FlyTarget.Gold: return Hud.GoldRect;
                case FlyTarget.Wall: return Hud.WallRect;
                case FlyTarget.Stable: return Footer.CountRect(LineDefense.Core.BuildKind.Stable);
                case FlyTarget.Fort: return Footer.CountRect(LineDefense.Core.BuildKind.Fort);
                case FlyTarget.Warehouse: return Footer.CountRect(LineDefense.Core.BuildKind.Warehouse);
                default: return Footer.InfoRect;
            }
        }

        static Canvas MakeCanvas(Transform parent, string name, int order, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1280f, 720f);
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight = 0.5f;
            if (raycast) go.AddComponent<GraphicRaycaster>();
            return c;
        }

        /// <summary>화면 크기나 꼬리말 내용이 바뀌었을 때만 머리말, 꼬리말과 떠 있는 패널을 다시 배치합니다.</summary>
        public void UpdateLayout()
        {
            var rt = (RectTransform)Canvas.transform;
            float w = rt.rect.width, h = rt.rect.height, sf = Canvas.scaleFactor;
            if (w < 10f || h < 10f) return;
            bool resized = !Mathf.Approximately(w, _w) || !Mathf.Approximately(h, _h) || !Mathf.Approximately(sf, _sf);
            if (!resized && !Footer.NeedsLayout) return;
            _w = w; _h = h; _sf = sf;
            CanvasW = w; CanvasH = h;
            HeaderUnits = Hud.Layout(w);
            float fh = Footer.Layout(w);
            HeaderPx = HeaderUnits * sf;
            FooterPx = fh * sf;
            Config.Relayout(w, HeaderUnits);
            Dialogue.Relayout(w, fh);
            Toast.SetTop(HeaderUnits);
            Cheat.SetTop(HeaderUnits);
        }
    }
}
