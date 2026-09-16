// Minimal stand-ins for the Unity APIs this project uses.
//
// These are NOT a Unity implementation and are never shipped or run. They exist
// so `dotnet build` can type-check the Unity layer - catching typos, wrong
// argument counts and wrong member names - on a machine with no Unity installed.
//
// If Unity rejects something this accepts, the stub signature below is wrong and
// should be corrected to match the real API.

using System;

namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; }
        public static void Destroy(Object target) { }
        public static T FindAnyObjectByType<T>() where T : Object { return null; }
        public static bool operator ==(Object a, Object b) { return ReferenceEquals(a, b); }
        public static bool operator !=(Object a, Object b) { return !ReferenceEquals(a, b); }
        public override bool Equals(object other) { return ReferenceEquals(this, other); }
        public override int GetHashCode() { return 0; }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return new Vector3(); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }
        public static float Dot(Vector3 a, Vector3 b) { return 0f; }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 forward { get { return new Vector3(0, 0, 1); } }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public float magnitude { get { return 0f; } }
        public Vector3 normalized { get { return this; } }
        public void Normalize() { }
        public static float Distance(Vector3 a, Vector3 b) { return 0f; }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return a; }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return a; }
        public static Vector3 operator *(Vector3 a, float s) { return a; }
        public static Vector3 operator *(float s, Vector3 a) { return a; }
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 one { get { return new Vector2(1, 1); } }
    }

    public struct Quaternion
    {
        public static Quaternion identity { get { return new Quaternion(); } }
        public static Quaternion Euler(float x, float y, float z) { return new Quaternion(); }
        public static Quaternion LookRotation(Vector3 forward, Vector3 up) { return new Quaternion(); }
        public static Quaternion LookRotation(Vector3 forward) { return new Quaternion(); }
        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegrees) { return from; }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) { return a; }
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) { return a; }
        public static Vector3 operator *(Quaternion q, Vector3 v) { return v; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white { get { return new Color(1, 1, 1); } }
        public static Color green { get { return new Color(0, 1, 0); } }
        public static Color Lerp(Color a, Color b, float t) { return a; }
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height)
        { this.x = x; this.y = y; this.width = width; this.height = height; }
    }

    public static class Mathf
    {
        public const float Rad2Deg = 57.29578f;
        public const float Deg2Rad = 0.0174532924f;
        public const float PI = 3.14159274f;

        public static float Clamp(float v, float min, float max) { return v; }
        public static float Clamp01(float v) { return v; }
        public static float Lerp(float a, float b, float t) { return a; }
        public static float LerpAngle(float a, float b, float t) { return a; }
        public static float MoveTowards(float a, float b, float maxDelta) { return b; }
        public static float Max(float a, float b) { return a; }
        public static float Min(float a, float b) { return a; }
        public static int Min(int a, int b) { return a; }
        public static int RoundToInt(float v) { return 0; }
        public static float Sin(float v) { return 0f; }
        public static float Cos(float v) { return 0f; }
        public static float Abs(float v) { return v; }
        public static float Atan2(float y, float x) { return 0f; }
        public static float Exp(float v) { return 0f; }
        public static float Sqrt(float v) { return 0f; }
    }

    public class Transform : Component
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 localScale { get; set; }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 forward { get { return Vector3.forward; } }
        public Transform parent { get; set; }
        public void SetParent(Transform parent, bool worldPositionStays) { }
    }

    public class Component : Object
    {
        public Transform transform { get; set; }
        public GameObject gameObject { get; set; }
        public T GetComponent<T>() where T : class { return null; }
        public string tag { get; set; }
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }
    }

    public class MonoBehaviour : Behaviour
    {
        public static void Destroy(Object target) { }
    }

    public enum PrimitiveType { Cube, Capsule, Plane, Cylinder, Sphere, Quad }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { this.name = name; }
        public Transform transform { get; set; }
        public bool activeSelf { get { return true; } }
        public void SetActive(bool value) { }
        public T AddComponent<T>() where T : Component, new() { return new T(); }
        public T GetComponent<T>() where T : class { return null; }
        public string tag { get; set; }
        public static GameObject CreatePrimitive(PrimitiveType type) { return new GameObject(); }
    }

    public class Renderer : Component
    {
        public Material sharedMaterial { get; set; }
        public Material material { get; set; }
        public bool receiveShadows { get; set; }
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; }
    }

    public class Collider : Component { }
    public class CharacterController : Collider
    {
        public float height { get; set; }
        public float radius { get; set; }
        public Vector3 center { get; set; }
        public float slopeLimit { get; set; }
        public float stepOffset { get; set; }
        public bool isGrounded { get { return true; } }
        public bool enabled { get; set; }
        public void Move(Vector3 motion) { }
    }

    public class Shader : Object
    {
        public static Shader Find(string name) { return null; }
    }

    public class Material : Object
    {
        public Material(Shader shader) { }
        public Color color { get; set; }
        public Texture mainTexture { get; set; }
        public Vector2 mainTextureScale { get; set; }
        public bool HasProperty(string name) { return true; }
        public void SetColor(string name, Color value) { }
        public void SetTexture(string name, Texture value) { }
        public void SetTextureScale(string name, Vector2 value) { }
    }

    public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }

    public class Texture : Object
    {
        public TextureWrapMode wrapMode { get; set; }
    }

    public class Texture2D : Texture
    {
        public static Texture2D whiteTexture { get { return null; } }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object { return null; }
    }

    public class AudioClip : Object { }

    public class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }
        public bool loop { get; set; }
        public bool playOnAwake { get; set; }
        public float volume { get; set; }
        public float spatialBlend { get; set; }
        public void Play() { }
        public void PlayOneShot(AudioClip clip, float volumeScale) { }
        public static void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume) { }
    }

    public enum LightType { Directional, Point, Spot }
    public enum LightShadows { None, Hard, Soft }

    public class Light : Behaviour
    {
        public LightType type { get; set; }
        public float intensity { get; set; }
        public Color color { get; set; }
        public LightShadows shadows { get; set; }
    }

    public enum CameraClearFlags { Skybox, SolidColor, Depth, Nothing }

    public class Camera : Behaviour
    {
        public static Camera main { get { return null; } }
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public float nearClipPlane { get; set; }
        public Vector3 WorldToScreenPoint(Vector3 position) { return Vector3.zero; }
    }

    public class AudioListener : Behaviour { }

    public class LineRenderer : Renderer
    {
        public bool useWorldSpace { get; set; }
        public bool loop { get; set; }
        public float widthMultiplier { get; set; }
        public int positionCount { get; set; }
        public Color startColor { get; set; }
        public Color endColor { get; set; }
        public void SetPosition(int index, Vector3 position) { }
    }

    public struct RaycastHit
    {
        public Vector3 point { get { return Vector3.zero; } }
    }

    public static class Physics
    {
        public static Vector3 gravity { get { return new Vector3(0, -9.81f, 0); } }
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance)
        { hit = new RaycastHit(); return false; }
    }

    public static class Time
    {
        public static float deltaTime { get { return 0.016f; } }
        public static float time { get { return 0f; } }
    }

    public static class Screen
    {
        public static int width { get { return 1920; } }
        public static int height { get { return 1080; } }
    }

    public static class Random
    {
        public static int Range(int min, int max) { return min; }
    }

    public enum CursorLockMode { None, Locked, Confined }

    public static class Cursor
    {
        public static CursorLockMode lockState { get; set; }
        public static bool visible { get; set; }
    }

    public enum KeyCode
    {
        Escape, V, Q, E, R, T, F, H, Tab, LeftShift, RightShift, LeftControl,
        Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9
    }

    public static class Input
    {
        public static bool GetKeyDown(KeyCode key) { return false; }
        public static bool GetKey(KeyCode key) { return false; }
        public static bool GetMouseButtonDown(int button) { return false; }
        public static float GetAxisRaw(string axis) { return 0f; }
    }

    public static class RenderSettings
    {
        public static Rendering.AmbientMode ambientMode { get; set; }
        public static Color ambientLight { get; set; }
    }

    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }

    public enum TextAnchor
    {
        UpperLeft, UpperCenter, UpperRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        LowerLeft, LowerCenter, LowerRight
    }

    public class GUIStyleState
    {
        public Color textColor { get; set; }
    }

    public class GUIStyle
    {
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public bool wordWrap { get; set; }
        public GUIStyleState normal { get; set; } = new GUIStyleState();
    }

    public class GUISkin
    {
        public GUIStyle label { get; set; } = new GUIStyle();
        public GUIStyle box { get; set; } = new GUIStyle();
    }

    public class GUIContent
    {
        public static GUIContent none { get { return new GUIContent(); } }
    }

    public static class GUI
    {
        public static GUISkin skin { get { return new GUISkin(); } }
        public static Color color { get; set; }
        public static void Box(Rect position, GUIContent content) { }
        public static void Label(Rect position, string text, GUIStyle style) { }
        public static void DrawTexture(Rect position, Texture image) { }
    }

    public class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string tooltip) { }
    }

    public class RangeAttribute : Attribute
    {
        public RangeAttribute(float min, float max) { }
    }

    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, SubsystemRegistration }

    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogError(object message) { }
        public static void LogWarning(object message) { }
    }
}

namespace UnityEngine.Rendering
{
    public enum AmbientMode { Skybox, Trilight, Flat, Custom }
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
}

namespace UnityEditor
{
    using System;

    public class InitializeOnLoadAttribute : Attribute { }

    public class MenuItemAttribute : Attribute
    {
        public MenuItemAttribute(string path) { }
    }

    public static class EditorApplication
    {
        public static Action delayCall;
    }

    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok) { return true; }
    }

    public static class SessionState
    {
        public static bool GetBool(string key, bool defaultValue) { return defaultValue; }
        public static void SetBool(string key, bool value) { }
    }
}
