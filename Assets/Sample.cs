using UnityEngine;

public class Sample : MonoBehaviour
{
    private void Update()
    {
        OnDrawGizmos();
    }

    private void OnDrawGizmos()
    {
        RuntimeGizmos.Context = this;

        var t = Time.time;

        RuntimeGizmos.Color = Color.white;
        RuntimeGizmos.Thickness = 2.5f;
        RuntimeGizmos.DrawCross(Vector3.zero, 0.1f);
        RuntimeGizmos.Thickness = 2f;
        RuntimeGizmos.Color = Color.black;
        RuntimeGizmos.DrawText(new Vector3(0, 0, 0), "Origin", anchor:TextAnchor.UpperLeft);

        RuntimeGizmos.Color = Color.red;
        RuntimeGizmos.DrawLine(new Vector3(-2, 0, 0), new Vector3(2, 0, 0));
        RuntimeGizmos.Color = Color.green;
        RuntimeGizmos.DrawLine(new Vector3(0, -2, 0), new Vector3(0, 2, 0));
        RuntimeGizmos.Color = Color.blue;
        RuntimeGizmos.DrawLine(new Vector3(0, 0, -2), new Vector3(0, 0, 2));

        const float col0X   = -1.5f;
        const float colDX   =  1.0f;
        const float row1Y   =  1.5f;
        const float row2Y   =  0.0f;
        const float row3Y   = -1.5f;
        const float labelDY =  0.3f;

        var r1c0 = new Vector3(col0X + 0 * colDX, row1Y, 0);
        var r1c1 = new Vector3(col0X + 1 * colDX, row1Y, 0);
        var r1c2 = new Vector3(col0X + 2 * colDX, row1Y, 0);
        var r1c3 = new Vector3(col0X + 3 * colDX, row1Y, 0);

        RuntimeGizmos.Color = new Color(0.2f, 0.4f, 0.8f, 0.6f);
        RuntimeGizmos.DrawRect(r1c0, new Vector2(0.3f, 0.3f));
        
        RuntimeGizmos.Color = Color.green;
        RuntimeGizmos.Thickness = 4;
        RuntimeGizmos.DrawWireRect(r1c1, new Vector2(0.3f, 0.3f));

        RuntimeGizmos.Thickness = 2f;
        RuntimeGizmos.Color = new Color(0.8f, 0.2f, 0.4f, 0.5f);
        RuntimeGizmos.DrawCube(r1c2, new Vector3(0.3f, 0.3f, 0.3f));

        RuntimeGizmos.Color = Color.yellow;
        RuntimeGizmos.Matrix = Matrix4x4.TRS(r1c3, Quaternion.Euler(45, 45, 0), Vector3.one);
        RuntimeGizmos.DrawWireCube(Vector3.zero, new Vector3(0.3f, 0.3f, 0.3f));
        RuntimeGizmos.Matrix = Matrix4x4.identity;

        RuntimeGizmos.Color = Color.white;
        RuntimeGizmos.DrawText(r1c0 + Vector3.up * labelDY, "Rect"    );
        RuntimeGizmos.DrawText(r1c1 + Vector3.up * labelDY, "WireRect");
        RuntimeGizmos.DrawText(r1c2 + Vector3.up * labelDY, "Cube"    );
        RuntimeGizmos.DrawText(r1c3 + Vector3.up * labelDY, "WireCube");

        var r2c0 = new Vector3(col0X + 0 * colDX, row2Y, 0);
        var r2c1 = new Vector3(col0X + 1 * colDX, row2Y, 0);
        var r2c2 = new Vector3(col0X + 2 * colDX, row2Y, 0);
        var r2c3 = new Vector3(col0X + 3 * colDX, row2Y, 0);

        RuntimeGizmos.Color = new Color(1f, 0.5f, 0f, 0.5f);
        RuntimeGizmos.DrawCircle(r2c0, 0.15f);

        RuntimeGizmos.Color = Color.cyan;
        RuntimeGizmos.DrawWireCircle(r2c1, 0.15f);

        RuntimeGizmos.Color = new Color(0.4f, 0.8f, 0.2f, 1f);
        RuntimeGizmos.DrawSphere(r2c2, 0.15f);

        RuntimeGizmos.MaxDistance = 5.0f;
        RuntimeGizmos.Color = Color.magenta;
        RuntimeGizmos.DrawWireSphere(r2c3, 0.15f);
        RuntimeGizmos.MaxDistance = 0;

        RuntimeGizmos.Color = Color.white;
        RuntimeGizmos.DrawText(r2c0 + Vector3.up * labelDY, "Circle"    );
        RuntimeGizmos.DrawText(r2c1 + Vector3.up * labelDY, "WireCircle");
        RuntimeGizmos.DrawText(r2c2 + Vector3.up * labelDY, "Sphere"    );
        RuntimeGizmos.DrawText(r2c3 + Vector3.up * labelDY, "WireSphere");

        var r3c0 = new Vector3(col0X + 0 * colDX, row3Y, 0);
        var r3c1 = new Vector3(col0X + 1 * colDX, row3Y, 0);
        var r3c2 = new Vector3(col0X + 2 * colDX, row3Y, 0);
        var r3c3 = new Vector3(col0X + 3 * colDX, row3Y, 0);

        RuntimeGizmos.Color = Color.red;
        RuntimeGizmos.DrawCross(r3c0, 0.3f);
        
        RuntimeGizmos.Color = Color.yellow;
        RuntimeGizmos.DrawArrow(r3c1 + new Vector3(-0.15f, -0.15f, 0), r3c1 + new Vector3(0.15f, 0.15f, 0));

        RuntimeGizmos.Color = Color.cyan;
        RuntimeGizmos.DrawLineList(new []
        {
            r3c2 + new Vector3(-0.15f, -0.15f, 0), r3c2 + new Vector3(-0.05f,  0.15f, 0),
            r3c2 + new Vector3(-0.05f,  0.15f, 0), r3c2 + new Vector3( 0.05f, -0.15f, 0),
            r3c2 + new Vector3( 0.05f, -0.15f, 0), r3c2 + new Vector3( 0.15f,  0.15f, 0),
        });

        RuntimeGizmos.Color = Color.yellow;
        RuntimeGizmos.DrawLineStrip(new []
        {
            r3c3 + new Vector3(-0.15f, -0.15f, 0),
            r3c3 + new Vector3(-0.05f,  0.15f, 0),
            r3c3 + new Vector3( 0.05f, -0.15f, 0),
            r3c3 + new Vector3( 0.15f,  0.15f, 0),
        });

        RuntimeGizmos.Color = Color.white;
        RuntimeGizmos.DrawText(r3c0 + Vector3.up * labelDY, "Cross"    );
        RuntimeGizmos.DrawText(r3c1 + Vector3.up * labelDY, "Arrow"    );
        RuntimeGizmos.DrawText(r3c2 + Vector3.up * labelDY, "LineList" );
        RuntimeGizmos.DrawText(r3c3 + Vector3.up * labelDY, "LineStrip");
    }
}