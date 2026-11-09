using System.Collections.Generic;
using LineDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LineDefense.View
{
    public sealed partial class GameController
    {
        private bool _pressFromUi;
        private bool _pressActive;
        private Vector2 _pressScreen;
        private Node _pressNode;
        private readonly List<(Vec2 Position, Vec2 EndPosition, Vec2 Direction, LineEnd End, Line Line)> _handles = new List<(Vec2 Position, Vec2 EndPosition, Vec2 Direction, LineEnd End, Line Line)>(2);

        private float UiScale => Mathf.Max(1f, _ui.ScaleFactor);

        // 노드 히트 반경: 월드 단위 22 이상, 화면 20픽셀 이상
        private float HitRadius => Mathf.Max(22f, 20f * UiScale / _cameraFit.PixelsPerUnit);

        private bool IsOverUi()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            return Input.touchCount > 0 ? eventSystem.IsPointerOverGameObject(Input.GetTouch(0).fingerId) : eventSystem.IsPointerOverGameObject();
        }

        private Node HitNode(Vec2 worldPosition)
        {
            float best = HitRadius;
            Node node = null;
            foreach (var candidateNode in _game.Nodes)
            {
                float distance = Vec2.Distance(candidateNode.Position, worldPosition);
                if (distance < best)
                {
                    best = distance;
                    node = candidateNode;
                }
            }
            return node;
        }

        private bool HitHandle(Vec2 worldPosition, out Line line, out LineEnd end)
        {
            float handleRadius = HitRadius * 0.8f;
            foreach (var currentLine in _game.Lines)
            {
                _game.Handles(currentLine, _handles);
                foreach (var handle in _handles)
                {
                    if (Vec2.Distance(handle.Position, worldPosition) < handleRadius)
                    {
                        line = handle.Line;
                        end = handle.End;
                        return true;
                    }
                }
            }
            line = null;
            end = LineEnd.Tail;
            return false;
        }

        private static bool IsConfigurable(Node node) => node.Type == NodeType.Stable || node.Type == NodeType.Warehouse || node.Type == NodeType.Fort;

        private void OpenConfig(Node node) => _ui.Config.Open(_game, _view, node, _ui.CanvasWidth, _ui.HeaderUnits);

        private void HandlePointer()
        {
            if (Input.GetMouseButtonDown(1))
            {
                CancelSelection();
            }

            Vector2 mousePosition = Input.mousePosition;
            bool overUi = IsOverUi();
            var worldPosition = _cameraFit.ScreenToWorld(mousePosition);
            bool onScreen = mousePosition.x >= 0f && mousePosition.y >= 0f && mousePosition.x <= _cameraFit.ScreenWidth && mousePosition.y <= _cameraFit.ScreenHeight;
            _view.HoverInside = onScreen && !overUi;
            if (_view.HoverInside)
            {
                _view.Hover = worldPosition;
            }

            if (Input.GetMouseButtonDown(0))
            {
                PointerDown(mousePosition, worldPosition, overUi);
            }

            if (Input.GetMouseButton(0) && _view.Dragging && !_pressFromUi)
            {
                _view.DragPosition = worldPosition;
                _view.DragTarget = HitNode(worldPosition);
            }
            if (Input.GetMouseButtonUp(0))
            {
                PointerUp(mousePosition, worldPosition);
            }
        }

        private void PointerDown(Vector2 mousePosition, Vec2 worldPosition, bool overUi)
        {
            _pressFromUi = overUi;
            _pressActive = false;
            if (overUi || IsBlocked())
            {
                return;
            }

            _view.Hover = worldPosition;

            if (_view.HasMode)
            {
                // 건설 중에 기존 마굿간·창고·요새를 누르면 건설을 멈추고 설정창을 연다
                var hitNode = HitNode(worldPosition);
                if (hitNode != null && IsConfigurable(hitNode) && !_game.CanPlace(_view.Mode, worldPosition))
                {
                    _view.HasMode = false;
                    OpenConfig(hitNode);
                    return;
                }
                var placed = _game.Place(_view.Mode, worldPosition);
                if (placed != null)
                {
                    _view.HasMode = false;
                    OpenConfig(placed);
                }
                ProcessEvents();
                return;
            }

            var pressedNode = HitNode(worldPosition);
            _pressActive = true;
            _pressScreen = mousePosition;
            _pressNode = pressedNode != null && IsConfigurable(pressedNode) ? pressedNode : null;
            if (_pressNode == null)
            {
                _ui.Config.Close();
            }

            Line line = null;
            LineEnd end = LineEnd.Tail;
            int segmentIndex = -1;
            bool start = HitHandle(worldPosition, out line, out end);
            if (!start && pressedNode != null)
            {
                start = _game.TryEndForNode(pressedNode, out line, out end);
            }
            // 정거장이 아닌 선 위를 누르면 그 구간을 끌어 새 정거장을 끼워 넣음
            if (!start && pressedNode == null)
            {
                start = _game.HitSegment(worldPosition, HitRadius * 0.6f, out line, out segmentIndex);
            }

            if (start)
            {
                _view.Dragging = true;
                _view.DragLine = line;
                _view.DragEnd = end;
                _view.DragSegment = segmentIndex;
                _view.DragPosition = worldPosition;
                _view.DragTarget = HitNode(worldPosition);
            }
        }

        private void PointerUp(Vector2 mousePosition, Vec2 worldPosition)
        {
            if (_pressFromUi)
            {
                _pressFromUi = false;
                return;
            }
            bool tap = _pressActive && _pressNode != null && Vector2.Distance(mousePosition, _pressScreen) < 6f * UiScale;
            var pressed = _pressNode;
            _pressActive = false;
            _pressNode = null;
            if (tap)
            {
                _view.Dragging = false;
                _view.DragLine = null;
                _view.DragSegment = -1;
                OpenConfig(pressed);
                return;
            }
            if (!_view.Dragging)
            {
                return;
            }

            var releasedNode = HitNode(worldPosition);
            if (releasedNode != null && _view.DragLine != null)
            {
                string message = _view.DragSegment >= 0 ? _game.InsertStop(_view.DragLine, _view.DragSegment, releasedNode) : _game.DragTo(_view.DragLine, _view.DragEnd, releasedNode);
                if (!string.IsNullOrEmpty(message))
                {
                    _ui.Toast.Show(message, _ui.CanvasWidth);
                }
            }
            _view.Dragging = false;
            _view.DragLine = null;
            _view.DragSegment = -1;
            _view.DragTarget = null;
            ProcessEvents();
        }

        private void WireCheats()
        {
            var cheatPanel = _ui.Cheat;
            cheatPanel.OnGold = () => { _game.CheatAddGold(100f); ProcessEvents(); };
            cheatPanel.OnTokens = () => { _game.CheatAddTokens(); ProcessEvents(); };
            cheatPanel.OnHeal = () => { _game.CheatHealWall(); ProcessEvents(); };
            cheatPanel.OnInvincible = () => _game.CheatInvincible = !_game.CheatInvincible;
            cheatPanel.OnSkipCountdown = () => { _game.CheatSkipCountdown(); ProcessEvents(); };
            cheatPanel.OnKillAll = () => { _game.CheatKillAll(); ProcessEvents(); };
            cheatPanel.OnWinWave = () => { _game.CheatWinWave(); ProcessEvents(); };
            cheatPanel.OnWavePlus5 = () => { _game.CheatSetWave(_game.Wave + 5); ProcessEvents(); };
            cheatPanel.OnSpeed = speed => _speed = speed;
            cheatPanel.OnAuto = () => _auto = !_auto;
            cheatPanel.OnNewGame = StartNewGame;
            cheatPanel.OnBalanceLoad = PickBalance;
            cheatPanel.OnBalanceReset = ResetBalance;
        }
    }
}
