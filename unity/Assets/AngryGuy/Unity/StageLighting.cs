using UnityEngine;

namespace AngryGuy.UnityLayer
{
    /// <summary>
    /// The lighting recipe, lifted almost directly from what shipped on
    /// Untitled Goose Game and adapted for a room rather than a village.
    ///
    ///   - one warm directional key, low in the sky, set for late afternoon
    ///   - shadows kept deliberately weak, so they ground objects without
    ///     carving the flat colour into pieces
    ///   - a cool flat ambient, so shadow reads blue against a warm key and
    ///     the palette has some temperature range without any extra lights
    ///   - a soft fog matched to the wall colour, which is what makes a small
    ///     room read as a built set rather than a box
    ///
    /// None of this needs a render pipeline, an artist, or a single texture.
    /// It is perhaps two hours of work and it is the largest single change to
    /// how the game looks.
    /// </summary>
    public static class StageLighting
    {
        public static void Apply(Transform parent)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.Ambient;
            RenderSettings.ambientEquatorColor = Palette.Shade(Palette.Ambient, -0.25f);
            RenderSettings.ambientGroundColor = Palette.Shade(Palette.Floor, -0.45f);
            RenderSettings.ambientIntensity = 1f;

            // Fog the same hue as the walls. At these distances it is barely
            // visible as fog and reads as air, which is exactly the point.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Palette.Shade(Palette.Shell, -0.08f);
            RenderSettings.fogDensity = 0.021f;

            RenderSettings.defaultReflectionMode =
                UnityEngine.Rendering.DefaultReflectionMode.Custom;

            BuildKey(parent);
            BuildFill(parent);
        }

        /// <summary>
        /// The sun through the window: warm, low, and raking, so verticals catch
        /// the light and horizontals fall away. A light from directly overhead
        /// flattens everything and is the most common way a simple scene ends up
        /// looking like untextured geometry.
        /// </summary>
        private static void BuildKey(Transform parent)
        {
            GameObject sun = new GameObject("KeyLight");
            sun.transform.SetParent(parent, false);
            sun.transform.rotation = Quaternion.Euler(34f, -128f, 0f);

            Light key = sun.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = Palette.KeyLight;
            key.intensity = 1.35f;

            key.shadows = LightShadows.Soft;

            // Weak shadows on purpose. Hard black shadows cut flat-shaded
            // objects into unreadable shapes; these ground a chair leg and
            // otherwise stay out of the way.
            key.shadowStrength = 0.42f;
            key.shadowBias = 0.02f;
            key.shadowNormalBias = 0.35f;
        }

        /// <summary>
        /// A dim, cool, shadowless bounce from the opposite side, so nothing is
        /// ever a silhouette. One light makes drama; two make a readable room,
        /// and readable is what this game needs.
        /// </summary>
        private static void BuildFill(Transform parent)
        {
            GameObject fill = new GameObject("FillLight");
            fill.transform.SetParent(parent, false);
            fill.transform.rotation = Quaternion.Euler(24f, 58f, 0f);

            Light light = fill.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Palette.Ambient;
            light.intensity = 0.34f;
            light.shadows = LightShadows.None;
        }

        /// <summary>
        /// Camera settings that do as much for the look as the lighting does.
        ///
        /// A longer lens flattens perspective and makes a room read as a
        /// miniature set seen from outside - the diorama effect - which is both
        /// the look we want and the framing the comedy needs, because the joke
        /// requires the target, the object and the witness all in shot at once.
        /// </summary>
        public static void ApplyCamera(Camera camera)
        {
            if (camera == null) return;

            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 90f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Shade(Palette.Shell, -0.22f);
            camera.allowHDR = true;
        }
    }
}
