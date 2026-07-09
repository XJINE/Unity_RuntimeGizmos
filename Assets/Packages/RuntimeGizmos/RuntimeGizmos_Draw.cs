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
}
