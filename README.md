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

Please add TextMeshPro resources to your project, as it references ``TMP_Settings.defaultFontAsset``.

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
	…
}
```

1. Call ``OnDrawGizmos`` in the ``Update`` method.
2. Inside ``OnDrawGizmos``, set ``RuntimeGizmos.Context = this;`` before drawing Gizmos.

### API

```csharp
DrawText       (Vector3 position, string text, TextAnchor anchor = TextAnchor.MiddleCenter) 
DrawRect       (Vector3 position, Vector2 size)                                             
DrawWireRect   (Vector3 position, Vector2 size)                                             
DrawCross      (Vector3 position, float size)                                               
DrawLine       (Vector3 from, Vector3 to)                                                   
DrawLineList   (ReadOnlySpan<Vector3> points)                                               
DrawLineStrip  (ReadOnlySpan<Vector3> points, bool looped = false)                          
DrawArrow      (Vector3 from, Vector3 to, float headSize = 24f, int segments = 12)          
DrawCircle     (Vector3 center, float radius, int segments = 32)                            
DrawWireCircle (Vector3 center, float radius, int segments = 32)                            
DrawCube       (Vector3 center, Vector3 size)                    
DrawWireCube   (Vector3 center, Vector3 size)                    
DrawSphere     (Vector3 center, float radius, int segments = 16) 
DrawWireSphere (Vector3 center, float radius, int segments = 32)
```

```csharp
Thickness   // Line thickness in pixels.
FontSize    // Text font size.
MaxDistance // Max distance from the camera to draw. 0 means no limit.
Color       // Draw color.
Matrix      // Transform applied to drawn positions.
```

## NOTE
I decided not to use IL Weaving (Mono.Cecil).
It introduces issues such as making debugging difficult and complicating the handling of inherited classes.
Potential conflicts with other IL weaving tools are also a difficult challenge.
For these reasons, I prioritized keeping the implementation as simple as possible."
