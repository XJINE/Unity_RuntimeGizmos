using System;
using System.Collections.Generic;
using UnityEngine;
using UColor = UnityEngine.Color;

public static partial class RuntimeGizmos
{
    public static float     Thickness   = 2f;
    public static float     FontSize    = 16f;
    public static float     MaxDistance = 0f;
    public static UColor    Color       = UColor.white;
    public static Matrix4x4 Matrix      = Matrix4x4.identity;

    private static float     PrevThickness   = 2f;
    private static float     PrevFontSize    = 16f;
    private static float     PrevMaxDistance = 0f;
    private static UColor    PrevColor       = UColor.white;
    private static Matrix4x4 PrevMatrix      = Matrix4x4.identity;

    public static Behaviour Context
    {
        get => _context;
        set
        {
            if (_batchFrame != FrameCount)
            {
                _seenContexts.Clear();
                _batchFrame = FrameCount;
            }

            _context = value;

            _skipBatch = value != null && (!value.isActiveAndEnabled || !_seenContexts.Add(value));
        }
    }

    private static Behaviour                   _context;
    private static int                         _batchFrame = -1;
    private static bool                        _skipBatch;
    private static readonly HashSet<Behaviour> _seenContexts = new ();

    public readonly struct ThicknessScope : IDisposable
    {
        private readonly float _prevThickness;

        public ThicknessScope(float thickness)
        {
            _prevThickness = Thickness;
            Thickness      = thickness;
        }

        public void Dispose()
        {
            Thickness = _prevThickness;
        }
    }

    public readonly struct FontSizeScope : IDisposable
    {
        private readonly float _prevFontSize;

        public FontSizeScope(float fontSize)
        {
            _prevFontSize = FontSize;
            FontSize      = fontSize;
        }

        public void Dispose()
        {
            FontSize = _prevFontSize;
        }
    }

    public readonly struct MaxDistanceScope : IDisposable
    {
        private readonly float _prevMaxDistance;

        public MaxDistanceScope(float maxDistance)
        {
            _prevMaxDistance = MaxDistance;
            MaxDistance      = maxDistance;
        }

        public void Dispose()
        {
            MaxDistance = _prevMaxDistance;
        }
    }

    public readonly struct ColorScope : IDisposable
    {
        private readonly UColor _prevColor;

        public ColorScope(UColor color)
        {
            _prevColor = Color;
            Color      = color;
        }

        public void Dispose()
        {
            Color = _prevColor;
        }
    }

    public readonly struct MatrixScope : IDisposable
    {
        private readonly Matrix4x4 _prevMatrix;

        public MatrixScope(Matrix4x4 matrix)
        {
            _prevMatrix = Matrix;
            Matrix      = matrix;
        }

        public void Dispose()
        {
            Matrix = _prevMatrix;
        }
    }

    public static ThicknessScope TempThickness(float thickness)
    {
        return new ThicknessScope(thickness);
    }

    public static FontSizeScope TempFontSize(float fontSize)
    {
        return new FontSizeScope(fontSize);
    }

    public static MaxDistanceScope TempMaxDistance(float maxDistance)
    {
        return new MaxDistanceScope(maxDistance);
    }

    public static ColorScope TempColor(UColor color)
    {
        return new ColorScope(color);
    }

    public static MatrixScope TempMatrix(Matrix4x4 matrix)
    {
        return new MatrixScope(matrix);
    }

    public static void SaveThickness     () { PrevThickness   = Thickness;       }
    public static void RestoreThickness  () { Thickness       = PrevThickness;   }
    public static void SaveFontSize      () { PrevFontSize    = FontSize;        }
    public static void RestoreFontSize   () { FontSize        = PrevFontSize;    }
    public static void SaveMaxDistance   () { PrevMaxDistance = MaxDistance;     }
    public static void RestoreMaxDistance() { MaxDistance     = PrevMaxDistance; }
    public static void SaveColor         () { PrevColor       = Color;           }
    public static void RestoreColor      () { Color           = PrevColor;       }
    public static void SaveMatrix        () { PrevMatrix      = Matrix;          }
    public static void RestoreMatrix     () { Matrix          = PrevMatrix;      }

    private static bool BeginFrame()
    {
        if (_commandsFrame != FrameCount)
        {
            _drawCommands.Clear();
            _commandsFrame = FrameCount;
        }

        return !_skipBatch;
    }

    public static void DrawText(Vector3 position, string text, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        if (!BeginFrame())
        {
            return;
        }

        _drawCommands.Add(new DrawCommand
        {
            Type        = CommandType.Text,
            Position    = position,
            Color       = Color,
            Text        = text,
            FontSize    = FontSize,
            MaxDistance = MaxDistance,
            Anchor      = anchor,
            Matrix      = Matrix
        });
    }

    public static void DrawRect(Vector3 position, Vector2 size)
    {
        if (!BeginFrame())
        {
            return;
        }

        _drawCommands.Add(new DrawCommand
        {
            Type        = CommandType.Rect,
            Position    = position,
            Size        = size,
            Color       = Color,
            MaxDistance = MaxDistance,
            Matrix      = Matrix
        });
    }

    public static void DrawWireRect(Vector3 position, Vector2 size)
    {
        if (!BeginFrame())
        {
            return;
        }

        _drawCommands.Add(new DrawCommand
        {
            Type        = CommandType.WireRect,
            Position    = position,
            Size        = size,
            Color       = Color,
            Thickness   = Thickness,
            MaxDistance = MaxDistance,
            Matrix      = Matrix
        });
    }

    public static void DrawCross(Vector3 position, float size)
    {
        if (!BeginFrame())
        {
            return;
        }

        _drawCommands.Add(new DrawCommand
        {
            Type        = CommandType.Cross,
            Position    = position,
            Size        = new Vector2(size, Thickness),
            Color       = Color,
            MaxDistance = MaxDistance,
            Matrix      = Matrix
        });
    }

    public static void DrawLine(Vector3 from, Vector3 to)
    {
        if (!BeginFrame())
        {
            return;
        }

        _drawCommands.Add(new DrawCommand
        {
            Type        = CommandType.Line,
            Position    = from,
            EndPosition = to,
            Color       = Color,
            Thickness   = Thickness,
            MaxDistance = MaxDistance,
            Matrix      = Matrix
        });
    }

    public static void DrawRay(Vector3 from, Vector3 direction)
    {
        DrawLine(from, from + direction);
    }

    public static void DrawRay(Ray ray)
    {
        DrawLine(ray.origin, ray.origin + ray.direction);
    }

    public static void DrawLineList(ReadOnlySpan<Vector3> points)
    {
        for (var i = 0; i + 1 < points.Length; i += 2)
        {
            DrawLine(points[i], points[i + 1]);
        }
    }

    public static void DrawLineStrip(ReadOnlySpan<Vector3> points, bool looped = false)
    {
        for (var i = 0; i + 1 < points.Length; i++)
        {
            DrawLine(points[i], points[i + 1]);
        }

        if (looped && 3 <= points.Length)
        {
            DrawLine(points[^1], points[0]);
        }
    }

    public static void DrawArrow(Vector3 from, Vector3 to, float headSize = 24f, int segments = 12)
    {
        if (!BeginFrame())
        {
            return;
        }

        _drawCommands.Add(new DrawCommand
        {
            Type        = CommandType.Arrow,
            Position    = from,
            EndPosition = to,
            Color       = Color,
            Thickness   = Thickness,
            HeadSize    = headSize,
            Segments    = segments,
            MaxDistance = MaxDistance,
            Matrix      = Matrix
        });
    }

    public static void DrawCircle(Vector3 center, float radius, int segments = 32)
    {
        if (!BeginFrame())
        {
            return;
        }

        _drawCommands.Add(new DrawCommand
        {
            Type        = CommandType.Circle,
            Position    = center,
            Size        = new Vector2(radius, 0),
            Color       = Color,
            Segments    = segments,
            MaxDistance = MaxDistance,
            Matrix      = Matrix
        });
    }

    public static void DrawWireCircle(Vector3 center, float radius, int segments = 32)
    {
        if (!BeginFrame())
        {
            return;
        }

        _drawCommands.Add(new DrawCommand
        {
            Type        = CommandType.WireCircle,
            Position    = center,
            Size        = new Vector2(radius, 0),
            Color       = Color,
            Thickness   = Thickness,
            Segments    = segments,
            MaxDistance = MaxDistance,
            Matrix      = Matrix
        });
    }

    public static void DrawCube(Vector3 center, Vector3 size)
    {
        if (!BeginFrame())
        {
            return;
        }

        _drawCommands.Add(new DrawCommand
        {
            Type        = CommandType.Cube,
            Position    = center,
            Size        = size,
            Color       = Color,
            MaxDistance = MaxDistance,
            Matrix      = Matrix
        });
    }

    public static void DrawWireCube(Vector3 center, Vector3 size)
    {
        if (!BeginFrame())
        {
            return;
        }

        _drawCommands.Add(new DrawCommand
        {
            Type        = CommandType.WireCube,
            Position    = center,
            Size        = size,
            Color       = Color,
            Thickness   = Thickness,
            MaxDistance = MaxDistance,
            Matrix      = Matrix
        });
    }

    public static void DrawSphere(Vector3 center, float radius, int segments = 16)
    {
        if (!BeginFrame())
        {
            return;
        }

        _drawCommands.Add(new DrawCommand
        {
            Type        = CommandType.Sphere,
            Position    = center,
            Size        = new Vector3(radius, 0, 0),
            Color       = Color,
            Segments    = segments,
            MaxDistance = MaxDistance,
            Matrix      = Matrix
        });
    }

    public static void DrawWireSphere(Vector3 center, float radius, int segments = 32)
    {
        if (!BeginFrame())
        {
            return;
        }

        _drawCommands.Add(new DrawCommand
        {
            Type        = CommandType.WireSphere,
            Position    = center,
            Size        = new Vector3(radius, 0, 0),
            Color       = Color,
            Thickness   = Thickness,
            Segments    = segments,
            MaxDistance = MaxDistance,
            Matrix      = Matrix
        });
    }

    public static void DrawFrustum(Vector3 center, float fov, float maxRange, float minRange = 0f, float aspect = 1f)
    {
        if (!BeginFrame())
        {
            return;
        }

        _drawCommands.Add(new DrawCommand
        {
            Type        = CommandType.Frustum,
            Position    = center,
            Size        = new Vector3(fov, maxRange, minRange),
            HeadSize    = aspect,
            Color       = Color,
            Thickness   = Thickness,
            MaxDistance = MaxDistance,
            Matrix      = Matrix
        });
    }
}