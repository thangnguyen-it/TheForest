using UnityEngine;

namespace Companion.Navigation
{
    /// <summary>
    /// Resolves Cmd_Clear radius via OverlapSphere against a Resource/Tree layer mask,
    /// not a hardcoded grid (§5).
    /// </summary>
    public class CompanionClearScanner : MonoBehaviour
    {
        [SerializeField] private LayerMask resourceTreeMask;

        private static readonly Collider[] _buffer = new Collider[64];

        public int FindClearTargets(Vector3 origin, float radius, System.Collections.Generic.List<Collider> outResults)
        {
            outResults.Clear();
            int count = Physics.OverlapSphereNonAlloc(origin, radius, _buffer, resourceTreeMask);
            for (int i = 0; i < count; i++) outResults.Add(_buffer[i]);
            return count;
        }
    }
}
