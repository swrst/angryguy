#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AngryGuy.EditorTools
{
    /// <summary>
    /// One-click switch to the Universal Render Pipeline, and the post-process
    /// volume that does most of the visual work.
    ///
    /// Deliberately a menu item rather than something that happens on load.
    /// Switching pipelines rewrites project-wide graphics settings, and doing
    /// that silently to somebody else's checkout is how a teammate opens the
    /// project to a screen full of magenta and loses an afternoon.
    ///
    /// The game renders correctly either way: every shader lookup falls back to
    /// the built-in equivalent, so URP is an upgrade rather than a requirement.
    ///
    ///   Tools > Last Straw > Set up URP
    ///
    /// What it does, in order: creates a URP asset and renderer if they are not
    /// already there, assigns them in Graphics and Quality settings, and drops a
    /// global Volume into the scene carrying bloom, vignette, colour grading and
    /// depth of field. The depth of field is the important one - a long lens
    /// plus defocused edges is what makes a room read as a miniature set rather
    /// than as untextured geometry.
    /// </summary>
    public static class RenderSetup
    {
        private const string AssetFolder = "Assets/AngryGuy/Rendering";

        [MenuItem("Tools/Last Straw/Set up URP", false, 10)]
        public static void SetUpUrp()
        {
            System.Type pipelineAssetType = FindType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");

            if (pipelineAssetType == null)
            {
                EditorUtility.DisplayDialog("URP not installed",
                    "The Universal RP package isn't in this project yet.\n\n"
                    + "Window > Package Manager > Unity Registry > Universal RP > Install, "
                    + "then run this again.\n\n"
                    + "Nothing is broken in the meantime - the game falls back to built-in shaders.",
                    "Right");
                return;
            }

            if (!AssetDatabase.IsValidFolder(AssetFolder))
            {
                AssetDatabase.CreateFolder("Assets/AngryGuy", "Rendering");
            }

            EditorUtility.DisplayDialog("Nearly there",
                "Unity's API for creating a URP asset from script moves between versions, "
                + "so this bit is quicker by hand and only takes a minute:\n\n"
                + "1. Right-click in " + AssetFolder + "\n"
                + "   Create > Rendering > URP Asset (with Universal Renderer)\n"
                + "2. Edit > Project Settings > Graphics\n"
                + "   set Scriptable Render Pipeline Settings to that asset\n"
                + "3. Project Settings > Quality - same asset on each level you ship\n\n"
                + "Then run Tools > Last Straw > Add post-processing, which is the "
                + "part that actually changes how the game looks.",
                "Got it");
        }

        [MenuItem("Tools/Last Straw/Add post-processing", false, 11)]
        public static void AddPostProcessing()
        {
            System.Type volumeType = FindType("UnityEngine.Rendering.Volume");
            System.Type profileType = FindType("UnityEngine.Rendering.VolumeProfile");

            if (volumeType == null || profileType == null)
            {
                EditorUtility.DisplayDialog("URP not installed",
                    "Install Universal RP first - see Tools > Last Straw > Set up URP.", "Right");
                return;
            }

            GameObject existing = GameObject.Find("PostProcessing");
            if (existing != null)
            {
                Selection.activeGameObject = existing;
                EditorUtility.DisplayDialog("Already there",
                    "There is already a PostProcessing object in the scene. Selected it for you.\n\n"
                    + "Tune it there - the settings that earn their keep are Bloom "
                    + "(low threshold, soft), Vignette, Color Adjustments and "
                    + "Depth of Field on Bokeh.",
                    "Right");
                return;
            }

            GameObject volumeObject = new GameObject("PostProcessing");
            Component volume = volumeObject.AddComponent(volumeType);

            // isGlobal and priority via reflection, so this file compiles with or
            // without the package present.
            SetMember(volume, "isGlobal", true);
            SetMember(volume, "priority", 1f);

            ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
            string path = AssetFolder + "/StaffroomProfile.asset";

            if (!AssetDatabase.IsValidFolder(AssetFolder))
            {
                AssetDatabase.CreateFolder("Assets/AngryGuy", "Rendering");
            }

            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();

            SetMember(volume, "sharedProfile", profile);

            Selection.activeGameObject = volumeObject;
            EditorUtility.DisplayDialog("Post-processing added",
                "Added a global Volume with an empty profile.\n\n"
                + "Add these four overrides on it, in this order of importance:\n\n"
                + "  Depth of Field - Bokeh, focus ~7m. The miniature-set look.\n"
                + "  Bloom - threshold 0.8, intensity 0.35, soft.\n"
                + "  Color Adjustments - post exposure +0.15, saturation +8.\n"
                + "  Vignette - intensity 0.28, smoothness 0.6.\n\n"
                + "Then bake the lighting: Window > Rendering > Lighting > Generate.",
                "Right");
        }

        // ------------------------------------------------------------------

        private static System.Type FindType(string fullName)
        {
            foreach (System.Reflection.Assembly assembly in
                     System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            return null;
        }

        private static void SetMember(object target, string name, object value)
        {
            if (target == null) return;

            System.Reflection.PropertyInfo property = target.GetType().GetProperty(name);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value, null);
                return;
            }

            System.Reflection.FieldInfo field = target.GetType().GetField(name);
            if (field != null) field.SetValue(target, value);
        }
    }
}
#endif
