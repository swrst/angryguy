using UnityEngine;

namespace AngryGuy.UnityLayer
{
    /// <summary>
    /// Builds the entire game at runtime, in whatever scene happens to be open.
    ///
    /// Why: .unity scene files are large, opaque, merge horribly in git, and
    /// cannot be written by hand. Generating the level from code means the
    /// repository holds only readable C#, two people can work on the same level
    /// without conflicts, and pressing Play in an empty scene Just Works.
    ///
    /// When the prototype graduates to real art, replace LevelView's primitives
    /// with prefab instantiation. Nothing else has to change.
    /// </summary>
    public static class Bootstrap
    {
        /// <summary>Set false if you ever build a hand-authored scene instead.</summary>
        public static bool AutoStart = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnGameStart()
        {
            if (!AutoStart) return;
            if (Object.FindAnyObjectByType<SimRunner>() != null) return;

            GameObject root = new GameObject("AngryGuy");
            root.AddComponent<SimRunner>();
        }
    }
}
