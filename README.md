# Unity_RuntimeGizmos

<img src="https://github.com/XJINE/Unity_RuntimeGizmos/blob/main/Screenshot.png" width="100%" height="auto" />

Runtime Gizmos with text rendering support.

## Importing

You can use Package Manager or import it directly.

```
https://github.com/XJINE/Unity_RuntimeGizmos.git?path=Assets/Packages/KeyEventManager
```

### Dependencies

This project use following resources.

- Unity.TextMeshPro
- https://github.com/XJINE/Unity_SingletonMonoBehaviour

## How to Use

### Add TextMeshPro resources

Please add TextMeshPro resources to your project, as it references ``TMP_Settings.defaultFontAsset``.

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