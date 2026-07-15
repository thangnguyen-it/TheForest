using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheForest.Persistence
{
    public interface IPersistentStateParticipant
    {
        string PersistenceId { get; }
        string CapturePersistenceState();
        void RestorePersistenceState(string json);
    }

    public static class PersistentStateId
    {
        public static string For(Component component)
        {
            if (component == null) return string.Empty;

            var builder = new StringBuilder(128);
            Scene scene = component.gameObject.scene;
            builder.Append(scene.IsValid() ? scene.name : "runtime");
            builder.Append('|');
            AppendPath(builder, component.transform);
            builder.Append('|').Append(component.GetType().FullName);
            return Hash128.Compute(builder.ToString()).ToString();
        }

        private static void AppendPath(StringBuilder builder, Transform current)
        {
            if (current.parent != null)
            {
                AppendPath(builder, current.parent);
                builder.Append('/');
            }
            builder.Append(current.GetSiblingIndex()).Append(':').Append(current.name);
        }
    }
}
