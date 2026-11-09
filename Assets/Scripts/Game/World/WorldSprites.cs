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
        readonly WorldArt _art;
        readonly Transform _root;
        readonly Dictionary<Node, NodeView> _nodes = new Dictionary<Node, NodeView>();
        readonly Dictionary<Cart, CartView> _carts = new Dictionary<Cart, CartView>();
        readonly Dictionary<Monster, MonsterView> _mons = new Dictionary<Monster, MonsterView>();
        readonly List<DenView> _dens = new List<DenView>();
        readonly Stack<CartView> _freeCarts = new Stack<CartView>();
        readonly Stack<MonsterView> _freeMons = new Stack<MonsterView>();
        readonly List<Node> _goneNodes = new List<Node>();
        readonly List<Cart> _goneCarts = new List<Cart>();
        readonly List<Monster> _goneMons = new List<Monster>();
        readonly GhostView _ghost;
        int _frame;

        public WorldSprites(WorldArt art, Transform parent)
        {
            _art = art;
            _root = new GameObject("Sprites").transform;
            _root.SetParent(parent, false);
            _ghost = Object.Instantiate(art.Ghost, _root, false);
            _ghost.Hide();
        }

        /// <summary>한 프레임 분량으로 맞춥니다.</summary>
        public void Sync(Game g, ViewState v)
        {
            _frame++;
            SyncNodes(g, v);
            SyncCarts(g);
            SyncMonsters(g);
            SyncDens(g);
            SyncGhost(g, v);
        }

        /// <summary>새 판을 시작할 때 이전 판의 그림을 모두 지웁니다.</summary>
        public void Clear()
        {
            foreach (var nv in _nodes.Values) Object.Destroy(nv.gameObject);
            _nodes.Clear();
            foreach (var cv in _carts.Values) Release(cv);
            _carts.Clear();
            foreach (var mv in _mons.Values) Release(mv);
            _mons.Clear();
        }

        void SyncNodes(Game g, ViewState v)
        {
            float t = g.T;
            foreach (var n in g.Nodes)
            {
                if (!_nodes.TryGetValue(n, out var view))
                {
                    view = Object.Instantiate(PrefabOf(n.Type), _root, false);
                    view.Bind(n);
                    _nodes[n] = view;
                }
                view.Stamp = _frame;
                float pop = Mathf.Min(1f, (t - n.Born) / 0.35f);
                float sc = pop < 1f ? 0.4f + 0.6f * pop + Mathf.Sin(pop * Mathf.PI) * 0.25f : 1f;
                var tr = view.transform;
                tr.localPosition = new Vector3(n.Pos.x, -n.Pos.y, 0f);
                tr.localScale = new Vector3(sc, sc, 1f);
                view.Sync(g, v, t);
            }
            _goneNodes.Clear();
            foreach (var kv in _nodes) if (kv.Value.Stamp != _frame) _goneNodes.Add(kv.Key);
            foreach (var n in _goneNodes)
            {
                Object.Destroy(_nodes[n].gameObject);
                _nodes.Remove(n);
            }
        }

        NodeView PrefabOf(NodeType t)
        {
            switch (t)
            {
                case NodeType.Stable: return _art.Stable;
                case NodeType.Farm: return _art.Farm;
                case NodeType.Forge: return _art.Forge;
                case NodeType.Warehouse: return _art.Warehouse;
                default: return _art.Fort;
            }
        }

        void SyncCarts(Game g)
        {
            int cap = g.CartCap();
            foreach (var l in g.Lines)
            {
                if (l.Points.Count < 2) continue;
                var col = Palette.LineColor(l.ColorIndex);
                foreach (var c in l.Carts)
                {
                    if (!_carts.TryGetValue(c, out var view))
                    {
                        view = _freeCarts.Count > 0 ? _freeCarts.Pop() : Object.Instantiate(_art.Cart, _root, false);
                        view.gameObject.SetActive(true);
                        _carts[c] = view;
                    }
                    view.Stamp = _frame;
                    view.Sync(c, col, cap);
                }
            }
            _goneCarts.Clear();
            foreach (var kv in _carts) if (kv.Value.Stamp != _frame) _goneCarts.Add(kv.Key);
            foreach (var c in _goneCarts)
            {
                Release(_carts[c]);
                _carts.Remove(c);
            }
        }

        void SyncMonsters(Game g)
        {
            foreach (var m in g.Mons)
            {
                if (!_mons.TryGetValue(m, out var view))
                {
                    view = _freeMons.Count > 0 ? _freeMons.Pop() : Object.Instantiate(_art.Monster, _root, false);
                    view.gameObject.SetActive(true);
                    _mons[m] = view;
                }
                view.Stamp = _frame;
                view.Sync(m);
            }
            _goneMons.Clear();
            foreach (var kv in _mons) if (kv.Value.Stamp != _frame) _goneMons.Add(kv.Key);
            foreach (var m in _goneMons)
            {
                Release(_mons[m]);
                _mons.Remove(m);
            }
        }

        void SyncDens(Game g)
        {
            bool countdown = g.Phase == Phase.Countdown && !g.Over;
            while (_dens.Count < g.Routes.Count) _dens.Add(Object.Instantiate(_art.Den, _root, false));
            for (int i = 0; i < _dens.Count; i++)
            {
                bool on = i < g.Routes.Count;
                if (_dens[i].gameObject.activeSelf != on) _dens[i].gameObject.SetActive(on);
                if (on) _dens[i].Sync(g.Routes[i], g.T, countdown);
            }
        }

        void SyncGhost(Game g, ViewState v)
        {
            if (!v.HasMode || !v.HoverInside) { _ghost.Hide(); return; }
            bool ok = g.CanPlace(v.Mode, v.Hover);
            float x = v.Hover.x, y = v.Hover.y;
            if (v.Mode == BuildKind.Fort)
            {
                var q = g.SnapWall(v.Hover, out float off);
                if (off < Game.WALL_SNAP * 2.5f) { x = q.x; y = q.y; }
            }
            Color col;
            if (v.Mode == BuildKind.Stable) col = ok ? Palette.LineColor(WorldRenderer.FreeColorIndex(g)) : Palette.Danger;
            else col = ok ? Palette.Ink : Palette.Danger;
            _ghost.Show(v.Mode, x, y, col);
        }

        void Release(CartView v)
        {
            v.gameObject.SetActive(false);
            _freeCarts.Push(v);
        }

        void Release(MonsterView v)
        {
            v.gameObject.SetActive(false);
            _freeMons.Push(v);
        }
    }
}
