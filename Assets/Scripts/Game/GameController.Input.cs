using System.Collections.Generic;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LineDefense.View
{
    public sealed partial class GameController
    {
        bool _pressFromUi;
        bool _pressActive;
        Vector2 _pressScreen;
        Node _pressNode;
        readonly List<(Vec2 pos, Vec2 endPos, Vec2 dir, LineEnd end, Line line)> _handles = new List<(Vec2 pos, Vec2 endPos, Vec2 dir, LineEnd end, Line line)>(2);

        float UiScale => Mathf.Max(1f, _ui.ScaleFactor);

        // 노드 히트 반경: 월드 단위 22 이상, 화면 20픽셀 이상
        float HitR => Mathf.Max(22f, 20f * UiScale / _cam.S);

        bool IsOverUi()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return Input.touchCount > 0 ? es.IsPointerOverGameObject(Input.GetTouch(0).fingerId) : es.IsPointerOverGameObject();
        }

        Node HitNode(Vec2 p)
        {
            float best = HitR;
            Node hit = null;
            foreach (var n in _g.Nodes)
            {
                float d = Vec2.Dist(n.Pos, p);
                if (d < best) { best = d; hit = n; }
            }
            return hit;
        }

        bool HitHandle(Vec2 p, out Line line, out LineEnd end)
        {
            float r = HitR * 0.8f;
            foreach (var l in _g.Lines)
            {
                _g.Handles(l, _handles);
                foreach (var h in _handles)
                {
                    if (Vec2.Dist(h.pos, p) < r)
                    {
                        line = h.line;
                        end = h.end;
                        return true;
                    }
                }
            }
            line = null;
            end = LineEnd.Tail;
            return false;
        }

        static bool IsConfigurable(Node n) => n.Type == NodeType.Stable || n.Type == NodeType.Warehouse || n.Type == NodeType.Fort;

        void OpenConfig(Node n) => _ui.Config.Open(_g, _v, n, _ui.CanvasW, _ui.HeaderUnits);

        void HandlePointer()
        {
            if (Input.GetMouseButtonDown(1)) CancelSelection();

            Vector2 mp = Input.mousePosition;
            bool overUi = IsOverUi();
            var wp = _cam.ScreenToWorld(mp);
            bool onScreen = mp.x >= 0f && mp.y >= 0f && mp.x <= _cam.W && mp.y <= _cam.H;
            _v.HoverInside = onScreen && !overUi;
            if (_v.HoverInside) _v.Hover = wp;

            if (Input.GetMouseButtonDown(0)) PointerDown(mp, wp, overUi);
            if (Input.GetMouseButton(0) && _v.Dragging && !_pressFromUi)
            {
                _v.DragPos = wp;
                _v.DragTarget = HitNode(wp);
            }
            if (Input.GetMouseButtonUp(0)) PointerUp(mp, wp);
        }

        void PointerDown(Vector2 mp, Vec2 wp, bool overUi)
        {
            _pressFromUi = overUi;
            _pressActive = false;
            if (overUi || IsBlocked()) return;
            _v.Hover = wp;

            if (_v.HasMode)
            {
                // 건설 중에 기존 마굿간·창고·요새를 누르면 건설을 멈추고 설정창을 연다
                var hn = HitNode(wp);
                if (hn != null && IsConfigurable(hn) && !_g.CanPlace(_v.Mode, wp))
                {
                    _v.HasMode = false;
                    OpenConfig(hn);
                    return;
                }
                var placed = _g.Place(_v.Mode, wp);
                if (placed != null)
                {
                    _v.HasMode = false;
                    OpenConfig(placed);
                }
                ProcessEvents();
                return;
            }

            var pn = HitNode(wp);
            _pressActive = true;
            _pressScreen = mp;
            _pressNode = pn != null && IsConfigurable(pn) ? pn : null;
            if (_pressNode == null) _ui.Config.Close();

            Line line = null;
            LineEnd end = LineEnd.Tail;
            int seg = -1;
            bool start = HitHandle(wp, out line, out end);
            if (!start && pn != null) start = _g.TryEndForNode(pn, out line, out end);
            // 정거장이 아닌 선 위를 누르면 그 구간을 끌어 새 정거장을 끼워 넣음
            if (!start && pn == null) start = _g.HitSegment(wp, HitR * 0.6f, out line, out seg);
            if (start)
            {
                _v.Dragging = true;
                _v.DragLine = line;
                _v.DragEnd = end;
                _v.DragSeg = seg;
                _v.DragPos = wp;
                _v.DragTarget = HitNode(wp);
            }
        }

        void PointerUp(Vector2 mp, Vec2 wp)
        {
            if (_pressFromUi) { _pressFromUi = false; return; }
            bool tap = _pressActive && _pressNode != null && Vector2.Distance(mp, _pressScreen) < 6f * UiScale;
            var pressed = _pressNode;
            _pressActive = false;
            _pressNode = null;
            if (tap)
            {
                _v.Dragging = false;
                _v.DragLine = null;
                _v.DragSeg = -1;
                OpenConfig(pressed);
                return;
            }
            if (!_v.Dragging) return;
            var t = HitNode(wp);
            if (t != null && _v.DragLine != null)
            {
                string msg = _v.DragSeg >= 0 ? _g.InsertStop(_v.DragLine, _v.DragSeg, t) : _g.DragTo(_v.DragLine, _v.DragEnd, t);
                if (!string.IsNullOrEmpty(msg)) _ui.Toast.Show(msg, _ui.CanvasW);
            }
            _v.Dragging = false;
            _v.DragLine = null;
            _v.DragSeg = -1;
            _v.DragTarget = null;
            ProcessEvents();
        }

        void WireCheats()
        {
            var c = _ui.Cheat;
            c.OnGold = () => { _g.CheatAddGold(100f); ProcessEvents(); };
            c.OnTokens = () => { _g.CheatAddTokens(); ProcessEvents(); };
            c.OnHeal = () => { _g.CheatHealWall(); ProcessEvents(); };
            c.OnInvincible = () => _g.CheatInvincible = !_g.CheatInvincible;
            c.OnSkipCountdown = () => { _g.CheatSkipCountdown(); ProcessEvents(); };
            c.OnKillAll = () => { _g.CheatKillAll(); ProcessEvents(); };
            c.OnWinWave = () => { _g.CheatWinWave(); ProcessEvents(); };
            c.OnWavePlus5 = () => { _g.CheatSetWave(_g.Wave + 5); ProcessEvents(); };
            c.OnSpeed = s => _speed = s;
            c.OnAuto = () => _auto = !_auto;
            c.OnNewGame = StartNewGame;
            c.OnBalanceLoad = PickBalance;
            c.OnBalanceReset = ResetBalance;
        }
    }
}
