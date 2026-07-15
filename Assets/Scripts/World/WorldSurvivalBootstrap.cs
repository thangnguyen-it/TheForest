using UnityEngine;

namespace TheForest.World
{
    /// <summary>
    /// Ensures the first survival-world slice exists in scenes that have not been wired by hand yet.
    /// Scene-authored components always win; this only fills missing runtime services.
    /// </summary>
    public static class WorldSurvivalBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntime()
        {
            if (Object.FindFirstObjectByType<SeasonSystem>() != null &&
                Object.FindFirstObjectByType<WeatherSystem>() != null &&
                Object.FindFirstObjectByType<WorldSurvivalEnvironment>() != null)
            {
                return;
            }

            var root = new GameObject("World Survival Runtime");
            Object.DontDestroyOnLoad(root);

            if (Object.FindFirstObjectByType<SeasonSystem>() == null)
                root.AddComponent<SeasonSystem>();

            if (Object.FindFirstObjectByType<WeatherSystem>() == null)
                root.AddComponent<WeatherSystem>();

            if (Object.FindFirstObjectByType<WorldSurvivalEnvironment>() == null)
                root.AddComponent<WorldSurvivalEnvironment>();
        }
    }
}
