using System;
using UnityEngine;

public static partial class RuntimeGizmos
{
    private static float PixelScale(float distance, Vector2 screenScale)
    {
        return distance * screenScale.x + screenScale.y;
    }

    private static bool IsCulled(DrawCommand cmd, Vector3 camPos)
    {
        if (cmd.MaxDistance <= 0f)
        {
            return false;
        }

        var worldPos = cmd.Matrix.MultiplyPoint3x4(cmd.Position);

        return cmd.MaxDistance < Vector3.Distance(camPos, worldPos);
    }

    private static Color ShadeColor(Color baseColor, Vector3 normal, Vector3 lightDir)
    {
        var nDotL = Vector3.Dot(normal, -lightDir) * 0.5f + 0.5f;

        return new Color(baseColor.r * nDotL, baseColor.g * nDotL, baseColor.b * nDotL, baseColor.a);
    }

    private static void EmitShapes(Vector3 camPos, Vector3 camRight, Vector3 camUp, Vector3 camForward, Vector2 screenScale)
    {
        GL.Begin(GL.QUADS);

        foreach (var cmd in _drawCommands)
        {
            if (IsCulled(cmd, camPos))
            {
                continue;
            }

            switch (cmd.Type)
            {
                case CommandType.Rect:       EmitRect      (cmd, camRight, camUp);                      break;
                case CommandType.WireRect:   EmitWireRect  (cmd, camRight, camUp, camPos, screenScale); break;
                case CommandType.Cross:      EmitCross     (cmd, camRight, camUp, camPos, screenScale); break;
                case CommandType.Line:       EmitLine      (cmd, camPos, screenScale);                  break;
                case CommandType.Arrow:      EmitArrowShaft(cmd, camForward, camPos, screenScale);      break;
                case CommandType.WireCircle: EmitWireCircle(cmd, camRight, camUp, camPos, screenScale); break;
                case CommandType.Cube:       EmitCube      (cmd, camForward);                           break;
                case CommandType.WireCube:   EmitWireCube  (cmd, camPos, screenScale);                  break;
                case CommandType.WireSphere: EmitWireSphere(cmd, camPos, screenScale);                  break;
                case CommandType.Frustum:    EmitFrustum   (cmd, camPos, screenScale);                  break;
                case CommandType.Text:                                                                  break;
                case CommandType.Circle:                                                                break;
                case CommandType.Sphere:                                                                break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        GL.End();

        GL.Begin(GL.TRIANGLES);

        foreach (var cmd in _drawCommands)
        {
            if (IsCulled(cmd, camPos))
            {
                continue;
            }

            switch (cmd.Type)
            {
                case CommandType.Arrow:  EmitArrowHead(cmd, camForward, camPos, screenScale); break;
                case CommandType.Circle: EmitCircle   (cmd, camRight, camUp);                 break;
                case CommandType.Sphere: EmitSphere   (cmd, camForward);                      break;
                case CommandType.Text:                                                        break;
                case CommandType.Rect:                                                        break;
                case CommandType.WireRect:                                                    break;
                case CommandType.Cross:                                                       break;
                case CommandType.Line:                                                        break;
                case CommandType.WireCircle:                                                  break;
                case CommandType.Cube:                                                        break;
                case CommandType.WireCube:                                                    break;
                case CommandType.WireSphere:                                                  break;
                case CommandType.Frustum:                                                     break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        GL.End();
    }

    private static void EmitRect(DrawCommand cmd, Vector3 camRight, Vector3 camUp)
    {
        var center = cmd.Matrix.MultiplyPoint3x4(cmd.Position);
        var r      = camRight * (cmd.Size.x * 0.5f);
        var u      = camUp    * (cmd.Size.y * 0.5f);

        GL.Color(cmd.Color);
        GL.Vertex(center - r - u);
        GL.Vertex(center + r - u);
        GL.Vertex(center + r + u);
        GL.Vertex(center - r + u);
    }

    private static void EmitWireRect(DrawCommand cmd, Vector3 camRight, Vector3 camUp, Vector3 camPos, Vector2 screenScale)
    {
        var center = cmd.Matrix.MultiplyPoint3x4(cmd.Position);
        var scale  = PixelScale(Vector3.Distance(camPos, center), screenScale);
        var hw     = cmd.Size.x * 0.5f;
        var hh     = cmd.Size.y * 0.5f;
        var t      = cmd.Thickness * scale * 0.5f;

        GL.Color(cmd.Color);

        var outerR = hw + t;
        var outerU = hh + t;
        var innerR = hw - t;
        var innerU = hh - t;

        GL.Vertex(center - camRight * outerR - camUp * outerU);
        GL.Vertex(center + camRight * outerR - camUp * outerU);
        GL.Vertex(center + camRight * outerR - camUp * innerU);
        GL.Vertex(center - camRight * outerR - camUp * innerU);

        GL.Vertex(center - camRight * outerR + camUp * innerU);
        GL.Vertex(center + camRight * outerR + camUp * innerU);
        GL.Vertex(center + camRight * outerR + camUp * outerU);
        GL.Vertex(center - camRight * outerR + camUp * outerU);

        GL.Vertex(center - camRight * outerR - camUp * innerU);
        GL.Vertex(center - camRight * innerR - camUp * innerU);
        GL.Vertex(center - camRight * innerR + camUp * innerU);
        GL.Vertex(center - camRight * outerR + camUp * innerU);

        GL.Vertex(center + camRight * innerR - camUp * innerU);
        GL.Vertex(center + camRight * outerR - camUp * innerU);
        GL.Vertex(center + camRight * outerR + camUp * innerU);
        GL.Vertex(center + camRight * innerR + camUp * innerU);
    }

    private static void EmitCross(DrawCommand cmd, Vector3 camRight, Vector3 camUp, Vector3 camPos, Vector2 screenScale)
    {
        var center   = cmd.Matrix.MultiplyPoint3x4(cmd.Position);
        var scale    = PixelScale(Vector3.Distance(camPos, center), screenScale);
        var halfSize = cmd.Size.x * 0.5f;
        var halfTh   = cmd.Size.y * scale * 0.5f;

        GL.Color(cmd.Color);

        GL.Vertex(center - camRight * halfSize - camUp * halfTh);
        GL.Vertex(center + camRight * halfSize - camUp * halfTh);
        GL.Vertex(center + camRight * halfSize + camUp * halfTh);
        GL.Vertex(center - camRight * halfSize + camUp * halfTh);

        GL.Vertex(center - camRight * halfTh - camUp * halfSize);
        GL.Vertex(center + camRight * halfTh - camUp * halfSize);
        GL.Vertex(center + camRight * halfTh + camUp * halfSize);
        GL.Vertex(center - camRight * halfTh + camUp * halfSize);
    }

    private static void EmitLine(DrawCommand cmd, Vector3 camPos, Vector2 screenScale)
    {
        var m     = cmd.Matrix;
        var from  = m.MultiplyPoint3x4(cmd.Position);
        var to    = m.MultiplyPoint3x4(cmd.EndPosition);
        var mid   = (from + to) * 0.5f;
        var scale = PixelScale(Vector3.Distance(camPos, mid), screenScale);
        var wt    = cmd.Thickness * scale * 0.5f;

        var lineDir = to - from;
        var len     = lineDir.magnitude;

        if (len < 0.0001f)
        {
            return;
        }

        lineDir /= len;

        var viewDir = (camPos - mid).normalized;
        var perp    = Vector3.Cross(lineDir, viewDir).normalized * wt;

        GL.Color(cmd.Color);
        GL.Vertex(from + perp);
        GL.Vertex(from - perp);
        GL.Vertex(to   - perp);
        GL.Vertex(to   + perp);
    }

    private static void EmitArrowShaft(DrawCommand cmd, Vector3 camForward, Vector3 camPos, Vector2 screenScale)
    {
        var m     = cmd.Matrix;
        var from  = m.MultiplyPoint3x4(cmd.Position);
        var to    = m.MultiplyPoint3x4(cmd.EndPosition);
        var dir   = to - from;
        var len   = dir.magnitude;
        var mid   = (from + to) * 0.5f;
        var scale = PixelScale(Vector3.Distance(camPos, mid), screenScale);

        if (len < 0.0001f || len <= cmd.HeadSize * scale)
        {
            return;
        }

        dir /= len;

        var shaftEnd = to - dir * cmd.HeadSize * scale;
        var radius   = cmd.Thickness * scale * 0.5f;
        var segments = Mathf.Max(cmd.Segments, 3);

        var perp = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) < 0.99f ? Vector3.Cross(dir, Vector3.up   ).normalized
                                                                   : Vector3.Cross(dir, Vector3.right).normalized;
        var binormal = Vector3.Cross(dir, perp).normalized;

        var step = 2f * Mathf.PI / segments;

        for (var i = 0; i < segments; i++)
        {
            var a0   = step * i;
            var a1   = step * (i + 1);
            var cos0 = Mathf.Cos(a0);
            var sin0 = Mathf.Sin(a0);
            var cos1 = Mathf.Cos(a1);
            var sin1 = Mathf.Sin(a1);

            var n0 = perp * cos0 + binormal * sin0;
            var n1 = perp * cos1 + binormal * sin1;

            var p0 = from     + n0 * radius;
            var p1 = from     + n1 * radius;
            var p2 = shaftEnd + n1 * radius;
            var p3 = shaftEnd + n0 * radius;

            GL.Color(ShadeColor(cmd.Color, n0, camForward));
            GL.Vertex(p0);
            GL.Color(ShadeColor(cmd.Color, n1, camForward));
            GL.Vertex(p1);
            GL.Vertex(p2);
            GL.Color(ShadeColor(cmd.Color, n0, camForward));
            GL.Vertex(p3);
        }
    }

    private static void EmitArrowHead(DrawCommand cmd, Vector3 camForward, Vector3 camPos, Vector2 screenScale)
    {
        var m     = cmd.Matrix;
        var from  = m.MultiplyPoint3x4(cmd.Position);
        var to    = m.MultiplyPoint3x4(cmd.EndPosition);
        var dir   = to - from;
        var len   = dir.magnitude;
        var scale = PixelScale(Vector3.Distance(camPos, to), screenScale);

        if (len < 0.0001f)
        {
            return;
        }

        dir /= len;

        var headSize   = cmd.HeadSize * scale;
        var basePos    = to - dir * headSize;
        var coneRadius = headSize * 0.35f;
        var segments   = Mathf.Max(cmd.Segments, 3);

        var perp = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) < 0.99f ? Vector3.Cross(dir, Vector3.up   ).normalized
                                                                   : Vector3.Cross(dir, Vector3.right).normalized;
        var binormal = Vector3.Cross(dir, perp).normalized;

        var step     = 2f * Mathf.PI / segments;
        var slopeLen = Mathf.Sqrt(headSize * headSize + coneRadius * coneRadius);
        var cosSlope = headSize / slopeLen;
        var sinSlope = coneRadius / slopeLen;

        for (var i = 0; i < segments; i++)
        {
            var a0 = step * i;
            var a1 = step * (i + 1);

            var radial0 = perp * Mathf.Cos(a0) + binormal * Mathf.Sin(a0);
            var radial1 = perp * Mathf.Cos(a1) + binormal * Mathf.Sin(a1);

            var n0 = (radial0 * cosSlope + dir * sinSlope).normalized;
            var n1 = (radial1 * cosSlope + dir * sinSlope).normalized;

            var p0 = basePos + radial0 * coneRadius;
            var p1 = basePos + radial1 * coneRadius;

            // Side
            GL.Color(ShadeColor(cmd.Color, n0, camForward));
            GL.Vertex(to);
            GL.Color(ShadeColor(cmd.Color, n0, camForward));
            GL.Vertex(p0);
            GL.Color(ShadeColor(cmd.Color, n1, camForward));
            GL.Vertex(p1);

            // Base cap
            GL.Color(ShadeColor(cmd.Color, -dir, camForward));
            GL.Vertex(basePos);
            GL.Vertex(p1);
            GL.Vertex(p0);
        }
    }

    private static void EmitCircle(DrawCommand cmd, Vector3 camRight, Vector3 camUp)
    {
        var center   = cmd.Matrix.MultiplyPoint3x4(cmd.Position);
        var radius   = cmd.Size.x;
        var segments = cmd.Segments;
        var step     = 2f * Mathf.PI / segments;

        GL.Color(cmd.Color);

        for (var i = 0; i < segments; i++)
        {
            var a0 = step * i;
            var a1 = step * (i + 1);

            GL.Vertex(center);
            GL.Vertex(center + camRight * (Mathf.Cos(a0) * radius) + camUp * (Mathf.Sin(a0) * radius));
            GL.Vertex(center + camRight * (Mathf.Cos(a1) * radius) + camUp * (Mathf.Sin(a1) * radius));
        }
    }

    private static void EmitWireCircle(DrawCommand cmd, Vector3 camRight, Vector3 camUp, Vector3 camPos, Vector2 screenScale)
    {
        var center   = cmd.Matrix.MultiplyPoint3x4(cmd.Position);
        var scale    = PixelScale(Vector3.Distance(camPos, center), screenScale);
        var radius   = cmd.Size.x;
        var t        = cmd.Thickness * scale * 0.5f;
        var segments = cmd.Segments;
        var rOuter   = radius + t;
        var rInner   = radius - t;
        var step     = 2f * Mathf.PI / segments;

        GL.Color(cmd.Color);

        for (var i = 0; i < segments; i++)
        {
            var a0   = step * i;
            var a1   = step * (i + 1);
            var cos0 = Mathf.Cos(a0);
            var sin0 = Mathf.Sin(a0);
            var cos1 = Mathf.Cos(a1);
            var sin1 = Mathf.Sin(a1);

            var innerP0 = center + camRight * (cos0 * rInner) + camUp * (sin0 * rInner);
            var outerP0 = center + camRight * (cos0 * rOuter) + camUp * (sin0 * rOuter);
            var outerP1 = center + camRight * (cos1 * rOuter) + camUp * (sin1 * rOuter);
            var innerP1 = center + camRight * (cos1 * rInner) + camUp * (sin1 * rInner);

            GL.Vertex(innerP0);
            GL.Vertex(outerP0);
            GL.Vertex(outerP1);
            GL.Vertex(innerP1);
        }
    }

    private static void EmitEdge(Vector3 from, Vector3 to, float halfThickness, Vector3 camPos)
    {
        var dir = to - from;
        var len = dir.magnitude;

        if (len < 0.0001f)
        {
            return;
        }

        dir /= len;

        var mid     = (from + to) * 0.5f;
        var viewDir = (camPos - mid).normalized;
        var perp    = Vector3.Cross(dir, viewDir).normalized * halfThickness;

        GL.Vertex(from + perp);
        GL.Vertex(from - perp);
        GL.Vertex(to   - perp);
        GL.Vertex(to   + perp);
    }

    private static void EmitCube(DrawCommand cmd, Vector3 camForward)
    {
        var m = cmd.Matrix;
        var c = cmd.Position;
        var h = cmd.Size * 0.5f;

        var v0 = m.MultiplyPoint3x4(c + new Vector3(-h.x, -h.y, -h.z));
        var v1 = m.MultiplyPoint3x4(c + new Vector3( h.x, -h.y, -h.z));
        var v2 = m.MultiplyPoint3x4(c + new Vector3( h.x,  h.y, -h.z));
        var v3 = m.MultiplyPoint3x4(c + new Vector3(-h.x,  h.y, -h.z));
        var v4 = m.MultiplyPoint3x4(c + new Vector3(-h.x, -h.y,  h.z));
        var v5 = m.MultiplyPoint3x4(c + new Vector3( h.x, -h.y,  h.z));
        var v6 = m.MultiplyPoint3x4(c + new Vector3( h.x,  h.y,  h.z));
        var v7 = m.MultiplyPoint3x4(c + new Vector3(-h.x,  h.y,  h.z));

        GL.Color(ShadeColor(cmd.Color, m.MultiplyVector(Vector3.forward).normalized, camForward));
        GL.Vertex(v4); GL.Vertex(v5); GL.Vertex(v6); GL.Vertex(v7);

        GL.Color(ShadeColor(cmd.Color, m.MultiplyVector(Vector3.back).normalized, camForward));
        GL.Vertex(v1); GL.Vertex(v0); GL.Vertex(v3); GL.Vertex(v2);

        GL.Color(ShadeColor(cmd.Color, m.MultiplyVector(Vector3.up).normalized, camForward));
        GL.Vertex(v3); GL.Vertex(v7); GL.Vertex(v6); GL.Vertex(v2);

        GL.Color(ShadeColor(cmd.Color, m.MultiplyVector(Vector3.down).normalized, camForward));
        GL.Vertex(v0); GL.Vertex(v1); GL.Vertex(v5); GL.Vertex(v4);

        GL.Color(ShadeColor(cmd.Color, m.MultiplyVector(Vector3.right).normalized, camForward));
        GL.Vertex(v1); GL.Vertex(v2); GL.Vertex(v6); GL.Vertex(v5);

        GL.Color(ShadeColor(cmd.Color, m.MultiplyVector(Vector3.left).normalized, camForward));
        GL.Vertex(v0); GL.Vertex(v4); GL.Vertex(v7); GL.Vertex(v3);
    }

    private static void EmitWireCube(DrawCommand cmd, Vector3 camPos, Vector2 screenScale)
    {
        var m     = cmd.Matrix;
        var c     = cmd.Position;
        var wc    = m.MultiplyPoint3x4(c);
        var scale = PixelScale(Vector3.Distance(camPos, wc), screenScale);
        var h     = cmd.Size * 0.5f;
        var ht    = cmd.Thickness * scale * 0.5f;

        var v0 = m.MultiplyPoint3x4(c + new Vector3(-h.x, -h.y, -h.z));
        var v1 = m.MultiplyPoint3x4(c + new Vector3( h.x, -h.y, -h.z));
        var v2 = m.MultiplyPoint3x4(c + new Vector3( h.x,  h.y, -h.z));
        var v3 = m.MultiplyPoint3x4(c + new Vector3(-h.x,  h.y, -h.z));
        var v4 = m.MultiplyPoint3x4(c + new Vector3(-h.x, -h.y,  h.z));
        var v5 = m.MultiplyPoint3x4(c + new Vector3( h.x, -h.y,  h.z));
        var v6 = m.MultiplyPoint3x4(c + new Vector3( h.x,  h.y,  h.z));
        var v7 = m.MultiplyPoint3x4(c + new Vector3(-h.x,  h.y,  h.z));

        GL.Color(cmd.Color);

        EmitEdge(v0, v1, ht, camPos);
        EmitEdge(v1, v2, ht, camPos);
        EmitEdge(v2, v3, ht, camPos);
        EmitEdge(v3, v0, ht, camPos);

        EmitEdge(v4, v5, ht, camPos);
        EmitEdge(v5, v6, ht, camPos);
        EmitEdge(v6, v7, ht, camPos);
        EmitEdge(v7, v4, ht, camPos);

        EmitEdge(v0, v4, ht, camPos);
        EmitEdge(v1, v5, ht, camPos);
        EmitEdge(v2, v6, ht, camPos);
        EmitEdge(v3, v7, ht, camPos);
    }

    private static void EmitSphere(DrawCommand cmd, Vector3 camForward)
    {
        var m         = cmd.Matrix;
        var center    = cmd.Position;
        var radius    = cmd.Size.x;
        var segments  = cmd.Segments;
        var slices    = segments * 2;
        var baseColor = cmd.Color;

        for (var lat = 0; lat < segments; lat++)
        {
            var theta0 = Mathf.PI * lat       / segments;
            var theta1 = Mathf.PI * (lat + 1) / segments;
            var sinT0  = Mathf.Sin(theta0);
            var cosT0  = Mathf.Cos(theta0);
            var sinT1  = Mathf.Sin(theta1);
            var cosT1  = Mathf.Cos(theta1);

            for (var lon = 0; lon < slices; lon++)
            {
                var phi0  = 2f * Mathf.PI * lon       / slices;
                var phi1  = 2f * Mathf.PI * (lon + 1) / slices;
                var sinP0 = Mathf.Sin(phi0);
                var cosP0 = Mathf.Cos(phi0);
                var sinP1 = Mathf.Sin(phi1);
                var cosP1 = Mathf.Cos(phi1);

                var n00 = new Vector3(sinT0 * cosP0, cosT0, sinT0 * sinP0);
                var n10 = new Vector3(sinT1 * cosP0, cosT1, sinT1 * sinP0);
                var n11 = new Vector3(sinT1 * cosP1, cosT1, sinT1 * sinP1);
                var n01 = new Vector3(sinT0 * cosP1, cosT0, sinT0 * sinP1);

                var wn00 = m.MultiplyVector(n00).normalized;
                var wn10 = m.MultiplyVector(n10).normalized;
                var wn11 = m.MultiplyVector(n11).normalized;
                var wn01 = m.MultiplyVector(n01).normalized;

                GL.Color(ShadeColor(baseColor, wn00, camForward)); GL.Vertex(m.MultiplyPoint3x4(center + n00 * radius));
                GL.Color(ShadeColor(baseColor, wn10, camForward)); GL.Vertex(m.MultiplyPoint3x4(center + n10 * radius));
                GL.Color(ShadeColor(baseColor, wn11, camForward)); GL.Vertex(m.MultiplyPoint3x4(center + n11 * radius));

                GL.Color(ShadeColor(baseColor, wn00, camForward)); GL.Vertex(m.MultiplyPoint3x4(center + n00 * radius));
                GL.Color(ShadeColor(baseColor, wn11, camForward)); GL.Vertex(m.MultiplyPoint3x4(center + n11 * radius));
                GL.Color(ShadeColor(baseColor, wn01, camForward)); GL.Vertex(m.MultiplyPoint3x4(center + n01 * radius));
            }
        }
    }

    private static void EmitWireSphere(DrawCommand cmd, Vector3 camPos, Vector2 screenScale)
    {
        var m        = cmd.Matrix;
        var center   = cmd.Position;
        var wc       = m.MultiplyPoint3x4(center);
        var scale    = PixelScale(Vector3.Distance(camPos, wc), screenScale);
        var radius   = cmd.Size.x;
        var ht       = cmd.Thickness * scale * 0.5f;
        var segments = cmd.Segments;
        var step     = 2f * Mathf.PI / segments;

        GL.Color(cmd.Color);

        for (var i = 0; i < segments; i++)
        {
            var a0 = step * i;
            var a1 = step * (i + 1);
            var c0 = Mathf.Cos(a0);
            var s0 = Mathf.Sin(a0);
            var c1 = Mathf.Cos(a1);
            var s1 = Mathf.Sin(a1);

            EmitEdge(m.MultiplyPoint3x4(center + new Vector3(c0 * radius, s0 * radius, 0)),
                     m.MultiplyPoint3x4(center + new Vector3(c1 * radius, s1 * radius, 0)),
                     ht, camPos);

            EmitEdge(m.MultiplyPoint3x4(center + new Vector3(c0 * radius, 0, s0 * radius)),
                     m.MultiplyPoint3x4(center + new Vector3(c1 * radius, 0, s1 * radius)),
                     ht, camPos);

            EmitEdge(m.MultiplyPoint3x4(center + new Vector3(0, c0 * radius, s0 * radius)),
                     m.MultiplyPoint3x4(center + new Vector3(0, c1 * radius, s1 * radius)),
                     ht, camPos);
        }
    }

    private static void EmitFrustum(DrawCommand cmd, Vector3 camPos, Vector2 screenScale)
    {
        var m        = cmd.Matrix;
        var apex     = cmd.Position;
        var fov      = cmd.Size.x;
        var maxRange = cmd.Size.y;
        var minRange = cmd.Size.z;
        var aspect   = cmd.HeadSize;
        var wc       = m.MultiplyPoint3x4(apex);
        var scale    = PixelScale(Vector3.Distance(camPos, wc), screenScale);
        var ht       = cmd.Thickness * scale * 0.5f;

        var tanFov = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        var nh     = tanFov * minRange;
        var nw     = nh * aspect;
        var fh     = tanFov * maxRange;
        var fw     = fh * aspect;

        var n0 = m.MultiplyPoint3x4(apex + new Vector3(-nw, -nh, minRange));
        var n1 = m.MultiplyPoint3x4(apex + new Vector3( nw, -nh, minRange));
        var n2 = m.MultiplyPoint3x4(apex + new Vector3( nw,  nh, minRange));
        var n3 = m.MultiplyPoint3x4(apex + new Vector3(-nw,  nh, minRange));
        var f0 = m.MultiplyPoint3x4(apex + new Vector3(-fw, -fh, maxRange));
        var f1 = m.MultiplyPoint3x4(apex + new Vector3( fw, -fh, maxRange));
        var f2 = m.MultiplyPoint3x4(apex + new Vector3( fw,  fh, maxRange));
        var f3 = m.MultiplyPoint3x4(apex + new Vector3(-fw,  fh, maxRange));

        GL.Color(cmd.Color);

        EmitEdge(n0, n1, ht, camPos);
        EmitEdge(n1, n2, ht, camPos);
        EmitEdge(n2, n3, ht, camPos);
        EmitEdge(n3, n0, ht, camPos);

        EmitEdge(f0, f1, ht, camPos);
        EmitEdge(f1, f2, ht, camPos);
        EmitEdge(f2, f3, ht, camPos);
        EmitEdge(f3, f0, ht, camPos);

        EmitEdge(n0, f0, ht, camPos);
        EmitEdge(n1, f1, ht, camPos);
        EmitEdge(n2, f2, ht, camPos);
        EmitEdge(n3, f3, ht, camPos);
    }

    private static void EmitText(DrawCommand cmd, Camera camera, Vector3 camPos, Vector3 camRight, Vector3 camUp)
    {
        if (string.IsNullOrEmpty(cmd.Text))
        {
            return;
        }

        var worldPos = cmd.Matrix.MultiplyPoint3x4(cmd.Position);
        var distance = Vector3.Distance(camPos, worldPos);

        var faceInfo = _fontAsset.faceInfo;
        var atlasW   = (float)_fontAsset.atlasTexture.width;
        var atlasH   = (float)_fontAsset.atlasTexture.height;

        float worldScale;

        if (camera.orthographic)
        {
            worldScale = cmd.FontSize * camera.orthographicSize * 2f
                       / (faceInfo.pointSize * camera.pixelHeight);
        }
        else
        {
            worldScale = cmd.FontSize * distance * 2f
                       * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                       / (faceInfo.pointSize * camera.pixelHeight);
        }

        var lines        = cmd.Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var lineAdvances = new float[lines.Length];
        var maxAdvance   = 0f;

        for (var i = 0; i < lines.Length; i++)
        {
            var advance = 0f;

            foreach (var t in lines[i])
            {
                if (_fontAsset.characterLookupTable.TryGetValue(t, out var ch))
                {
                    advance += ch.glyph.metrics.horizontalAdvance;
                }
            }

            lineAdvances[i] = advance;

            if (advance > maxAdvance)
            {
                maxAdvance = advance;
            }
        }

        var lineHeight = faceInfo.lineHeight * worldScale;
        var textWidth  = maxAdvance * worldScale;
        var textHeight = (faceInfo.ascentLine - faceInfo.descentLine) * worldScale
                       + lineHeight * (lines.Length - 1);

        var offsetX = cmd.Anchor switch
        {
            TextAnchor.UpperLeft   or TextAnchor.MiddleLeft   or TextAnchor.LowerLeft   => 0f,
            TextAnchor.UpperCenter or TextAnchor.MiddleCenter or TextAnchor.LowerCenter => textWidth * 0.5f,
            _ => textWidth
        };

        var offsetY = cmd.Anchor switch
        {
            TextAnchor.UpperLeft  or TextAnchor.UpperCenter  or TextAnchor.UpperRight  => 0f,
            TextAnchor.MiddleLeft or TextAnchor.MiddleCenter or TextAnchor.MiddleRight => textHeight * 0.5f,
            _ => textHeight
        };

        var topLeft = worldPos - camRight * offsetX + camUp * offsetY;

        GL.Color(cmd.Color);

        for (var i = 0; i < lines.Length; i++)
        {
            var line        = lines[i];
            var lineWidth   = lineAdvances[i] * worldScale;
            var lineOffsetX = cmd.Anchor switch
            {
                TextAnchor.UpperLeft   or TextAnchor.MiddleLeft   or TextAnchor.LowerLeft   => 0f,
                TextAnchor.UpperCenter or TextAnchor.MiddleCenter or TextAnchor.LowerCenter => (textWidth - lineWidth) * 0.5f,
                _ => textWidth - lineWidth
            };

            var lineOrigin = topLeft
                           + camRight * lineOffsetX
                           - camUp * (lineHeight * i);
            var baseline   = lineOrigin - camUp * (faceInfo.ascentLine * worldScale);

            foreach (var t in line)
            {
                if (!_fontAsset.characterLookupTable.TryGetValue(t, out var ch))
                {
                    continue;
                }

                var glyph   = ch.glyph;
                var rect    = glyph.glyphRect;
                var metrics = glyph.metrics;

                var bearingX = metrics.horizontalBearingX * worldScale;
                var bearingY = metrics.horizontalBearingY * worldScale;
                var glyphW   = rect.width  * worldScale;
                var glyphH   = rect.height * worldScale;

                var origin = baseline + camRight * bearingX;
                var p0     = origin + camUp * (bearingY - glyphH);
                var p1     = p0 + camRight * glyphW;
                var p2     = p1 + camUp * glyphH;
                var p3     = origin + camUp * bearingY;

                var u0 = rect.x / atlasW;
                var v0 = rect.y / atlasH;
                var u1 = (rect.x + rect.width)  / atlasW;
                var v1 = (rect.y + rect.height) / atlasH;

                GL.TexCoord2(u0, v0); GL.Vertex(p0);
                GL.TexCoord2(u1, v0); GL.Vertex(p1);
                GL.TexCoord2(u1, v1); GL.Vertex(p2);
                GL.TexCoord2(u0, v1); GL.Vertex(p3);

                baseline += camRight * (metrics.horizontalAdvance * worldScale);
            }
        }
    }
}