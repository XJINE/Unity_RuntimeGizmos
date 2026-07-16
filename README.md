# Unity_RuntimeGizmos

<img src="https://github.com/XJINE/Unity_RuntimeGizmos/blob/main/Screenshot.png" width="100%" height="auto" />

Runtime Gizmos with text rendering support.

## Importing

You can use Package Manager or import it directly.

```
https://github.com/XJINE/Unity_RuntimeGizmos.git?path=Assets/Packages/RuntimeGizmos
```

### Dependencies

This project use following resources.

- Unity.TextMeshPro

## How to Use

### Add TextMeshPro resources

Add TextMeshPro resources to your project, as it references ``TMP_Settings.defaultFontAsset``.

#### Minimum resources

Due to licensing reasons, TextMeshPro resources are not included in this repository.
The following is the minimum set of resources required to use the default TextMeshPro:

```
Resources/TMP Settings.asset
Resources/Fonts & Materials/LiberationSans SDF.asset
Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset
Resources/Style Sheets/Default Style Sheet.asset
Resources/LineBreaking Leading Characters.txt
Resources/LineBreaking Following Characters.txt
Fonts/LiberationSans.ttf
Fonts/LiberationSans - OFL.txt
Shaders/TMP_SDF-Mobile.shader
Shaders/TMPro_Properties.cginc
```

### Implementation

```csharp
private void Update()
{
    OnDrawGizmos();
}

private void OnDrawGizmos()
{
    RuntimeGizmos.Context = this;
    RuntimeGizmos.Draw~…
}
```

1. Call ``OnDrawGizmos`` in the ``Update`` method.
2. Inside ``OnDrawGizmos``, set ``RuntimeGizmos.Context = this;`` before drawing Gizmos.

Note that Camera.current and others will not work if called from Update.

```csharp
// Classic style
var prevColor = RuntimeGizmos.Color;
RuntimeGizmos.Color = Color.white;
~
RuntimeGizmos.Color = prevColor;

// Block style
RuntimeGizmos.SaveColor();
RuntimeGizmos.Color = Color.white;
RuntimeGizmos.Draw~
RuntimeGizmos.Color = Color.yellow;
RuntimeGizmos.Draw~
RuntimeGizmos.RestoreColor();

// Scope style
RuntimeGizmos.TempColor(color);
RuntimeGizmos.Draw~
~

using(RuntimeGizmos.TempColor(color))
{
    RuntimeGizmos.Draw~
    ~
}
```

There are three ways to configure the options.


#### Limitation

Do not call ``RuntimeGizmos.Context = this;`` twice within the same frame and context.
Be careful when calling ``base.OnDrawGizmos()`` and ``OnDrawGizmosSelected()``.

Note that ``OnDrawGizmosSelected()`` is not called in builds (at runtime).

### API

#### Draw Methods

```csharp
DrawText       (Vector3 position, string text, TextAnchor anchor = TextAnchor.MiddleCenter)
DrawRect       (Vector3 position, Vector2 size)
DrawWireRect   (Vector3 position, Vector2 size)
DrawCross      (Vector3 position, float size)
DrawLine       (Vector3 from, Vector3 to)
DrawRay        (Vector3 from, Vector3 direction)
DrawRay        (Ray ray)
DrawLineList   (ReadOnlySpan<Vector3> points)
DrawLineStrip  (ReadOnlySpan<Vector3> points, bool looped = false)
DrawArrow      (Vector3 from, Vector3 to, float headSize = 24f, int segments = 12)
DrawCircle     (Vector3 center, float radius, int segments = 32)
DrawWireCircle (Vector3 center, float radius, int segments = 32)
DrawCube       (Vector3 center, Vector3 size)
DrawWireCube   (Vector3 center, Vector3 size)
DrawSphere     (Vector3 center, float radius, int segments = 16)
DrawWireSphere (Vector3 center, float radius, int segments = 32)
DrawFrustum    (Vector3 center, float fov, float maxRange, float minRange = 0f, float aspect = 1f)
```

#### Options

```csharp
Thickness   // Line thickness in pixels.
FontSize    // Text font size.
MaxDistance // Max distance from the camera to draw. 0 means no limit.
Color       // Draw color.
Matrix      // Transform applied to drawn positions.
```

## Development Notes

This project does not use IL Weaving (Mono.Cecil).
It creates complications, such as making debugging difficult and handling inherited classes more complex.
Another reason is to avoid conflicts with other IL weaving tools.

Only options that apply to all Gizmos drawn at the same time are provided.
For example, a parameter like ``segments`` should not be provided as an option.

DrawGUITexture and Draw(Wire)Mesh are excluded since alternatives exist.
DrawIcon is also excluded for the same reason as textures.
