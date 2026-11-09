using System.Collections.Generic;
using LineDefense.Core;
using UnityEngine;

namespace LineDefense.View
{
    /// <summary>
    /// 정거장·마차·몬스터·굴을 프리팹 인스턴스로 그리고 게임 상태와 맞춥니다.
    /// 사라진 대상의 인스턴스는 지우고, 자주 생기고 사라지는 몬스터·마차는 재사용합니다.
    /// </summary>
    public sealed class WorldSprites
    {
        private readonly WorldArt _art;
        private readonly Transform _root;
        private readonly Dictionary<Node, NodeView> _nodes = new Dictionary<Node, NodeView>();
        private readonly Dictionary<Cart, CartView> _carts = new Dictionary<Cart, CartView>();
        private readonly Dictionary<Monster, MonsterView> _mons = new Dictionary<Monster, MonsterView>();
        private readonly List<DenView> _dens = new List<DenView>();
        private readonly Stack<CartView> _freeCarts = new Stack<CartView>();
        private readonly Stack<MonsterView> _freeMons = new Stack<MonsterView>();
        private readonly List<Node> _goneNodes = new List<Node>();
        private readonly List<Cart> _goneCarts = new List<Cart>();
        private readonly List<Monster> _goneMons = new List<Monster>();
        private readonly GhostView _ghost;
        private int _frame;

        public WorldSprites(WorldArt worldArt, Transform parent)
        {
            _art = worldArt;
            _root = new GameObject("Sprites").transform;
            _root.SetParent(parent, false);
            _ghost = Object.Instantiate(worldArt.Ghost, _root, false);
            _ghost.Hide();
        }

        /// <summary>한 프레임 분량으로 맞춥니다.</summary>
        public void Sync(Game game, ViewState view)
        {
            _frame++;
            SyncNodes(game, view);
            SyncCarts(game);
            SyncMonsters(game);
            SyncDens(game);
            SyncGhost(game, view);
        }

        /// <summary>새 판을 시작할 때 이전 판의 그림을 모두 지웁니다.</summary>
        public void Clear()
        {
            foreach (var nodeView in _nodes.Values)
            {
                Object.Destroy(nodeView.gameObject);
            }

            _nodes.Clear();
            foreach (var cartView in _carts.Values)
            {
                Release(cartView);
            }

            _carts.Clear();
            foreach (var monsterView in _mons.Values)
            {
                Release(monsterView);
            }

            _mons.Clear();
        }

        private void SyncNodes(Game game, ViewState viewState)
        {
            float t = game.ElapsedTime;
            foreach (var node in game.Nodes)
            {
                if (!_nodes.TryGetValue(node, out var view))
                {
                    view = Object.Instantiate(PrefabOf(node.Type), _root, false);
                    view.Bind(node);
                    _nodes[node] = view;
                }
                view.Stamp = _frame;
                float popProgress = Mathf.Min(1f, (t - node.Born) / 0.35f);
                float popScale = popProgress < 1f ? 0.4f + 0.6f * popProgress + Mathf.Sin(popProgress * Mathf.PI) * 0.25f : 1f;
                var nodeTransform = view.transform;
                nodeTransform.localPosition = new Vector3(node.Position.X, -node.Position.Y, 0f);
                nodeTransform.localScale = new Vector3(popScale, popScale, 1f);
                view.Sync(game, viewState, t);
            }
            _goneNodes.Clear();
            foreach (var entry in _nodes)
            {
                if (entry.Value.Stamp != _frame)
                {
                    _goneNodes.Add(entry.Key);
                }
            }

            foreach (var node in _goneNodes)
            {
                Object.Destroy(_nodes[node].gameObject);
                _nodes.Remove(node);
            }
        }

        private NodeView PrefabOf(NodeType nodeType)
        {
            switch (nodeType)
            {
                case NodeType.Stable:
                    return _art.Stable;
                case NodeType.Farm:
                    return _art.Farm;
                case NodeType.Forge:
                    return _art.Forge;
                case NodeType.Warehouse:
                    return _art.Warehouse;
                default:
                    return _art.Fort;
            }
        }

        private void SyncCarts(Game game)
        {
            int capacity = game.CartCapacity();
            foreach (var line in game.Lines)
            {
                if (line.Points.Count < 2)
                {
                    continue;
                }

                var color = Palette.LineColor(line.ColorIndex);
                foreach (var cart in line.Carts)
                {
                    if (!_carts.TryGetValue(cart, out var view))
                    {
                        view = _freeCarts.Count > 0 ? _freeCarts.Pop() : Object.Instantiate(_art.Cart, _root, false);
                        view.gameObject.SetActive(true);
                        _carts[cart] = view;
                    }
                    view.Stamp = _frame;
                    view.Sync(cart, color, capacity);
                }
            }
            _goneCarts.Clear();
            foreach (var entry in _carts)
            {
                if (entry.Value.Stamp != _frame)
                {
                    _goneCarts.Add(entry.Key);
                }
            }

            foreach (var cart in _goneCarts)
            {
                Release(_carts[cart]);
                _carts.Remove(cart);
            }
        }

        private void SyncMonsters(Game game)
        {
            foreach (var monster in game.Monsters)
            {
                if (!_mons.TryGetValue(monster, out var view))
                {
                    view = _freeMons.Count > 0 ? _freeMons.Pop() : Object.Instantiate(_art.Monster, _root, false);
                    view.gameObject.SetActive(true);
                    _mons[monster] = view;
                }
                view.Stamp = _frame;
                view.Sync(monster);
            }
            _goneMons.Clear();
            foreach (var entry in _mons)
            {
                if (entry.Value.Stamp != _frame)
                {
                    _goneMons.Add(entry.Key);
                }
            }

            foreach (var monster in _goneMons)
            {
                Release(_mons[monster]);
                _mons.Remove(monster);
            }
        }

        private void SyncDens(Game game)
        {
            bool countdown = game.Phase == Phase.Countdown && !game.Over;
            while (_dens.Count < game.Routes.Count)
            {
                _dens.Add(Object.Instantiate(_art.Den, _root, false));
            }

            for (int i = 0; i < _dens.Count; i++)
            {
                bool on = i < game.Routes.Count;
                if (_dens[i].gameObject.activeSelf != on)
                {
                    _dens[i].gameObject.SetActive(on);
                }

                if (on)
                {
                    _dens[i].Sync(game.Routes[i], game.ElapsedTime, countdown);
                }
            }
        }

        private void SyncGhost(Game game, ViewState view)
        {
            if (!view.HasMode || !view.HoverInside)
            {
                _ghost.Hide();
                return;
            }
            bool canPlace = game.CanPlace(view.Mode, view.Hover);
            float x = view.Hover.X, y = view.Hover.Y;
            if (view.Mode == BuildKind.Fort)
            {
                var snappedPosition = game.SnapWall(view.Hover, out float off);
                if (off < Game.WallSnap * 2.5f)
                {
                    x = snappedPosition.X;
                    y = snappedPosition.Y;
                }
            }
            Color color;
            if (view.Mode == BuildKind.Stable)
            {
                color = canPlace ? Palette.LineColor(WorldRenderer.FreeColorIndex(game)) : Palette.Danger;
            }
            else
            {
                color = canPlace ? Palette.Ink : Palette.Danger;
            }

            _ghost.Show(view.Mode, x, y, color);
        }

        private void Release(CartView cartView)
        {
            cartView.gameObject.SetActive(false);
            _freeCarts.Push(cartView);
        }

        private void Release(MonsterView monsterView)
        {
            monsterView.gameObject.SetActive(false);
            _freeMons.Push(monsterView);
        }
    }
}
