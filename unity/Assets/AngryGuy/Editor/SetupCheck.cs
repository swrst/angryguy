using UnityEditor;
using UnityEngine;

namespace AngryGuy.EditorTools
{
    /// <summary>
    /// The prototype uses the legacy Input Manager (Input.GetAxisRaw), because it
    /// needs no assets or action maps. If a project is set to the new Input System
    /// only, that call throws at runtime and the game appears frozen - which is a
    /// confusing first five minutes for anyone opening this repo.
    ///
    /// This check says so plainly instead.
    /// </summary>
    [InitializeOnLoad]
    public static class SetupCheck
    {
        private const string SeenKey = "AngryGuy.SetupCheck.Seen";

        static SetupCheck()
        {
            EditorApplication.delayCall += Run;
        }

        [MenuItem("Angry Guy/Check project setup")]
        public static void Run()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            Debug.LogError(
                "Angry Guy: this project is set to the new Input System only, and the prototype " +
                "uses the legacy Input Manager. Set Edit > Project Settings > Player > " +
                "Active Input Handling to 'Input Manager (Old)' or 'Both', then restart Unity.");
#else
            if (!SessionState.GetBool(SeenKey, false))
            {
                SessionState.SetBool(SeenKey, true);
                Debug.Log("Angry Guy: input handling is fine. Press Play in any empty scene - " +
                          "the level builds itself.");
            }
#endif
        }

        [MenuItem("Angry Guy/How to play")]
        public static void HowToPlay()
        {
            EditorUtility.DisplayDialog(
                "Angry Guy - kitchen prototype",
                "Objective: make Gordon the chef furious without anyone deciding it was you, " +
                "then leave by the back door (the green circle).\n\n" +
                "WASD  move\n" +
                "Shift  sneak (quiet, but looks shifty)\n" +
                "Mouse  look,  V  first/third person\n" +
                "E or 1-9  interact with what's in reach\n" +
                "Q  cancel an interaction\n" +
                "Tab  AI debug overlay\n\n" +
                "Sabotage is shown in orange. Most of it pays off on a delay - set it up, " +
                "then make sure you are somewhere else when it lands.",
                "Got it");
        }
    }
}
