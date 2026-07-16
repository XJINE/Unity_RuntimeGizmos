using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class RuntimeGizmos
{
    private enum CommandType : byte
    {
        Text,
        Rect,
        WireRect,
        Cross,
        Line,
        Arrow,
        Circle,
        WireCircle,
        Cube,
        WireCube,
        Sphere,
        WireSphere,
        Frustum
    }

    private struct DrawCommand
    {
        public CommandType Type;
        public Vector3     Position;
        public Vector3     EndPosition;
        public Vector3     Size;
        public Color       Color;
        public string      Text;
        public float       FontSize;
        public float       Thickness;
        public float       HeadSize;
        public float       MaxDistance;
        public int         Segments;
        public TextAnchor  Anchor;
        public Matrix4x4   Matrix;
    }

    private static Material                   _solidColorMaterial;
    private static Material                   _sdfFontMaterial;
    private static TMP_FontAsset              _fontAsset;
    private static int                        _commandsFrame = -1;
    private static int                        _editorFrameCount;
    private static readonly List<DrawCommand> _drawCommands  = new ();

    private static int FrameCount =>
    #if UNITY_EDITOR
    Application.isPlaying ? Time.frameCount : _editorFrameCount;
    #else
    Time.frameCount;
    #endif

    // NOTE:
    // Called when entering Play Mode or running the built app.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterRuntime()
    {
        // SRP (URP / HDRP)
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;

        // Built-in pipeline
        Camera.onPostRender -= OnPostRenderCamera;
        Camera.onPostRender += OnPostRenderCamera;

        Application.quitting -= ReleaseResources;
        Application.quitting += ReleaseResources;
    }

    // NOTE:
    // Called in editor.
    #if UNITY_EDITOR

    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterEditor()
    {
        // SRP (URP / HDRP)
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;

        // Built-in pipeline
        Camera.onPostRender -= OnPostRenderCamera;
        Camera.onPostRender += OnPostRenderCamera;

        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseResources;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseResources;
    }

    #endif

    private static void InitializeResources()
    {
        if (_solidColorMaterial == null)
        {
            var shader = Resources.Load<Shader>("SolidColor");

            if (shader != null)
            {
                _solidColorMaterial = new Material(shader);
            }
        }

        if (_sdfFontMaterial == null)
        {
            _fontAsset ??= TMP_Settings.defaultFontAsset;

            var shader = Resources.Load<Shader>("SDFFont");

            if (_fontAsset != null && shader != null)
            {
                _sdfFontMaterial = new Material(shader)
                {
                    mainTexture = _fontAsset.atlasTexture
                };
            }
        }
    }

    private static void ReleaseResources()
    {
        DestroyMaterial(_solidColorMaterial);
        DestroyMaterial(_sdfFontMaterial);

        _solidColorMaterial = null;
        _sdfFontMaterial    = null;

        return;

        static void DestroyMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(material);
            }
            else
            {
                Object.DestroyImmediate(material);
            }
        }
    }

    private static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        // SRP (URP / HDRP) entry point.
        RenderAll(camera);
    }

    private static void OnPostRenderCamera(Camera camera)
    {
        // Built-in pipeline entry point.
        RenderAll(camera);
    }

    private static void RenderAll(Camera camera)
    {
        if (camera.cameraType is not (CameraType.Game or CameraType.SceneView))
        {
            return;
        }

        #if UNITY_EDITOR

        if (!Application.isPlaying)
        {
            _editorFrameCount++;
        }

        if (!UnityEditor.Handles.ShouldRenderGizmos()) // Toggle Gizmos button in editor.
        {
            return;
        }

        #endif

        if (_drawCommands.Count <= 0 || 1 < FrameCount - _commandsFrame)
        {
            _drawCommands.Clear();
            return;
        }

        InitializeResources();

        if (_solidColorMaterial == null)
        {
            return;
        }

        var camTransform = camera.transform;
        var camPos       = camTransform.position;
        var camRight     = camTransform.right;
        var camUp        = camTransform.up;
        var camForward   = camTransform.forward;

        GL.PushMatrix();
        GL.LoadProjectionMatrix(camera.projectionMatrix);
        GL.LoadIdentity();
        GL.MultMatrix(camera.worldToCameraMatrix);

        RenderShapes(camera, camPos, camRight, camUp, camForward);

        if (_sdfFontMaterial != null)
        {
            RenderTexts(camera, camPos, camRight, camUp);
        }

        GL.PopMatrix();
    }

    private static void RenderShapes(Camera camera, Vector3 camPos, Vector3 camRight, Vector3 camUp, Vector3 camForward)
    {
        var screenScale = camera.orthographic ? 
        new Vector2(0f, camera.orthographicSize * 2f / camera.pixelHeight) :
        new Vector2(2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / camera.pixelHeight, 0f);

        _solidColorMaterial.SetPass(0); // Render occluded color.
        EmitShapes(camPos, camRight, camUp, camForward, screenScale);
        _solidColorMaterial.SetPass(1); // Render visible color.
        EmitShapes(camPos, camRight, camUp, camForward, screenScale);
    }

    private static void RenderTexts(Camera camera, Vector3 camPos, Vector3 camRight, Vector3 camUp)
    {
        _sdfFontMaterial.SetPass(0);

        GL.Begin(GL.QUADS);

        foreach (var cmd in _drawCommands.Where(cmd => cmd.Type == CommandType.Text))
        {
            if (IsCulled(cmd, camPos))
            {
                continue;
            }

            EmitText(cmd, camera, camPos, camRight, camUp);
        }

        GL.End();
    }
}