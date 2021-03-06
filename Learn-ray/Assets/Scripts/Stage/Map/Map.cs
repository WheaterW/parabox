using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Stage.Objects;
using static UnityEngine.Debug;

namespace Stage {
    using MoveData = Dictionary<Item, (Vector3Int, Vector3Int)>;

    public class Map {
        public readonly Dictionary<Vector3Int, Item> items = new();
        public readonly List<Player> players = new();
        public readonly Dictionary<Vector3Int, Goal> goals = new();
        private Views.BaseView m_view;
        public Views.BaseView view {
            get => m_view;
            set { m_view = value; Build(); }
        }

        private readonly Dictionary<Item, Item[]> edges = new();
        private readonly Stack<MoveData> moveHistory = new();

        public Map(List<Item> itemData, List<Goal> goalData, Views.BaseView initView) {
            foreach (var item in itemData) {
                items[item.position] = item;
                if (item is Player player) {
                    players.Add(player);
                }
            }
            foreach (var goal in goalData) {
                goals[goal.position] = goal;
            }
            players.Sort((Player L, Player R) => L.playerId.CompareTo(R.playerId));
            for(int i = 0; i < players.Count; ++i) {
                int j = (i + 1) % players.Count;
                players[i].nextPlayer = players[j];
            }
            view = initView;
        }

        /// <summary> 判断当前状态是否获胜 (每个目标上都有箱子/玩家) </summary>
        public bool IsWin() {
            foreach (var (pos, goal) in goals) {
                if (!items.TryGetValue(pos, out var item)) return false;
                if (goal.isPlayer && item is not Player) return false;
                if (!goal.isPlayer && item is not Box) return false;
            }
            return true;
        }

        private void Build() {
            edges.Clear();
            foreach (var (_, item) in items) {
                edges[item] = new Item[4];
            }
            view.nodes = items;
            view.edges = edges;
            view.Build();
        }

        /// <summary> 尝试移动玩家, 方向为 0~3 </summary>
        /// <returns> 若可以移动返回移动信息, 否则返回 null </returns>
        public MoveData TryMove(Item player, int dir) {
            MoveData ret = new();
            HashSet<Vector3Int> vis = new();
            bool Search(Vector3Int floor) {
                if (vis.Contains(floor)) return true;
                vis.Add(floor);

                // 上方没有箱子
                if (!items.ContainsKey(floor + Vector3Int.up)) return true;

                // 当前方向不合法
                var nextItem = edges[items[floor]][dir];
                if (nextItem == null) return false;

                // 推上方的所有箱子
                var next = nextItem.position;
                for (var pos = floor + Vector3Int.up; ; pos += Vector3Int.up) {
                    if (!items.TryGetValue(pos, out var item)) break;
                    if (item is Floor) break;
                    ret[item] = (pos, next + pos - floor);
                }
                return Search(next);
            }
            if (!Search(player.position + Vector3Int.down)) return null;
            foreach (var (_, (_, newPos)) in ret) {
                if (!items.TryGetValue(newPos, out var item)) continue;
                if (item is Floor) return null;
            }
            return ret;
        }
        /// <summary> 根据移动信息移动玩家 </summary>
        public void Move(MoveData data, bool rollback) {
            foreach (var (item, _) in data) {
                items.Remove(item.position);
            }
            foreach (var (item, (oldPos, newPos)) in data) {
                var pos = rollback ? oldPos : newPos;
                item.position = pos;
                items[pos] = item;
            }
            if (!rollback) moveHistory.Push(data);
            Build();
        }

        /// <summary> 尝试移动玩家, 方向为 0~3 或 4 (回退) </summary>
        /// <returns> 若可以移动返回移动信息, 否则返回 null </returns>
        public MoveData Move(Item player, int dir) {
            MoveData data = null;
            if (dir != Direction.Rollback) {
                data = TryMove(player, dir);
                if (data != null) Move(data, false);
            } else if (moveHistory.Count > 0) {
                data = moveHistory.Pop();
                if (data != null) Move(data, true);
            }
            return data;
        }

        public void Debug() {
            const float d = .2f;
            foreach (var (p, item) in items) {
                Color color = item is Box ? Color.red : Color.yellow;
                DrawLine(p - new Vector3(d, 0, d), p + new Vector3(d, 0, d), color);
                DrawLine(p - new Vector3(-d, 0, d), p + new Vector3(-d, 0, d), color);
            }
            foreach (var (u, ls) in edges) {
                foreach (var v in ls) {
                    if (v == null) continue;
                    DrawLine(u.position + new Vector3(d, 0, d), v.position);
                    DrawLine(u.position + new Vector3(-d, 0, d), v.position);
                    DrawLine(u.position + new Vector3(d, 0, -d), v.position);
                    DrawLine(u.position + new Vector3(-d, 0, -d), v.position);
                }
            }
        }
    }
}
