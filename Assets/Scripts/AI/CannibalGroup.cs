using System.Collections.Generic;
using UnityEngine;

namespace TheForest.AI
{
    /// <summary>
    /// Một nhóm cannibal patrol chung. Quản lý thành viên, Leader,
    /// gọi viện binh (alert lan trong nhóm) và bao vây player (chia hướng).
    /// </summary>
    public class CannibalGroup : MonoBehaviour
    {
        private readonly List<CannibalAI> _members = new List<CannibalAI>();
        public CannibalAI Leader { get; private set; }
        public int ZoneId { get; set; }

        public IReadOnlyList<CannibalAI> Members => _members;

        public void AddMember(CannibalAI ai, bool isLeader)
        {
            if (ai == null || _members.Contains(ai)) return;
            _members.Add(ai);
            ai.SetGroup(this);
            if (isLeader) Leader = ai;
        }

        public void RemoveMember(CannibalAI ai)
        {
            _members.Remove(ai);
            if (Leader == ai) Leader = _members.Count > 0 ? _members[0] : null;
        }

        /// <summary>Một thành viên phát hiện player -> báo cả nhóm (viện binh).</summary>
        public void AlertGroup(Transform player, CannibalAI source)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i] == null || _members[i] == source) continue;
                _members[i].OnGroupAlert(player);
            }
        }

        /// <summary>
        /// Trả vị trí bao vây cho 1 thành viên: chia đều quanh player để không dồn 1 chỗ.
        /// </summary>
        public Vector3 GetSurroundPosition(CannibalAI member, Transform player, float radius)
        {
            int idx = Mathf.Max(0, _members.IndexOf(member));
            int count = Mathf.Max(1, _members.Count);
            float angle = (360f / count) * idx;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
            return player.position + offset;
        }

        /// <summary>Tìm đồng đội đang knockdown gần vị trí để đỡ dậy.</summary>
        public CannibalAI FindDownedAllyNear(Vector3 pos, float maxDist)
        {
            CannibalAI best = null; float bestSqr = maxDist * maxDist;
            foreach (var m in _members)
            {
                if (m == null || m.State != CannibalState.Knockdown) continue;
                float d = (m.transform.position - pos).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = m; }
            }
            return best;
        }

        public bool IsEmpty => _members.Count == 0;
    }
}
