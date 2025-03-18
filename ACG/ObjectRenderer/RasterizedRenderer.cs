using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Graphics.Core;

namespace Graphics.UI.Render;

public static class RasterizedRenderer
{
    private static float[] zBuffer;
    
    public static void DrawObject(Vector4[] transformedVertices, ObjectModel model, Camera camera, WriteableBitmap wb, Color color,
        List<CustomLight> lights
        )
    {
        zBuffer = new float[wb.PixelHeight * wb.PixelWidth];
        wb.Lock();
        int intColor = (color.B << 0) | (color.G << 8) | (color.R << 16) | (color.A << 24);
        var pBackBuffer = wb.BackBuffer;
        var width = wb.PixelWidth;
        var height = wb.PixelHeight;
        Parallel.ForEach(model.Object.Faces, face =>
        {
            int count = face.Vertices.Count();
            if (count < 2)
                return;

            int maxX = int.MinValue;
            int minX = int.MaxValue;
            int maxY = int.MinValue;
            int minY = int.MaxValue;

            
            var vertices = face.Vertices.Select(index => transformedVertices[index.VertexIndex - 1]).ToArray();
            var worldVertices = face.Vertices.Select(index => model.TransformedVertices[index.VertexIndex - 1]).ToArray();
            for (int j = 1; j < count - 1; j++)
            {
                Vector4 idx0 = vertices[0];
                Vector4 idx1 = vertices[j];
                Vector4 idx2 = vertices[j + 1];

                Vector4 center = (worldVertices[0] + worldVertices[j] + worldVertices[j + 1]) / 3.0f;
                Vector4 v1 = worldVertices[j] - worldVertices[0];
                Vector4 v2 = worldVertices[j + 1] - worldVertices[0];
                Vector3 normal = Vector3.Cross(
                    new Vector3(v1.X, v1.Y, v1.Z),
                    new Vector3(v2.X, v2.Y, v2.Z));

                normal = Vector3.Normalize(normal);
                var surfacePosition = new Vector3(center.X, center.Y, center.Z);

                int lambertColor = color.ApplyLambert(normal, surfacePosition, lights);
                int countNear = 0;
                int countFar = 0;
                
                if (idx0.Z < camera.ZNear) countNear++;
                if (idx1.Z < camera.ZNear) countNear++;
                if (idx2.Z < camera.ZNear) countNear++;

                if (idx0.Z > camera.ZFar) countFar++;
                if (idx1.Z > camera.ZFar) countFar++;
                if (idx2.Z > camera.ZFar) countFar++;
                
                if (countFar + countNear < 1)
                {
                    RasterizeTriangle(pBackBuffer, width, height, idx0, idx1, idx2,lambertColor);
                }
            }
        });
        wb.AddDirtyRect(new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight));
        wb.Unlock();
    }

    private static float EdgeFunction(Vector4 a, Vector4 b, Vector4 c)
    {
        return (c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X);
    }
    private static unsafe void RasterizeTriangle(IntPtr buffer, int width, int height, Vector4 v0, Vector4 v1, Vector4 v2,
        int color)
    {
        int* bufferPtr = (int*)buffer;
        var xMin = (int)Math.Round(MathF.Min(v0.X, MathF.Min(v1.X, v2.X)));
        var yMin = (int)Math.Round(MathF.Min(v0.Y, MathF.Min(v1.Y, v2.Y)));
        var xMax = (int)Math.Round(MathF.Max(v0.X, MathF.Max(v1.X, v2.X)));
        var yMax = (int)Math.Round(MathF.Max(v0.Y, MathF.Max(v1.Y, v2.Y)));
        

        xMax = Math.Min(width-1, xMax);
        yMax = Math.Min(height-1, yMax);
        xMin = Math.Max(0, xMin);
        yMin = Math.Max(0, yMin);
        float area = EdgeFunction(v0, v1, v2);
        for (var y = yMin; y <= yMax; y++)
        {
            for (var x = xMin; x <= xMax; x++)
            {
                Vector4 pixel = new Vector4(x + 0.5f, y + 0.5f, 0, 0);
                float w0 = EdgeFunction(v1, v2, pixel);
                float w1 = EdgeFunction(v2, v0, pixel);
                float w2 = EdgeFunction(v0, v1, pixel);
                
                if (w0 >= 0 && w1 >= 0 && w2 >= 0)
                {
                    
                    w0 /= area;
                    w1 /= area;
                    w2 /= area;
                    float z = v0.Z * w0 + v1.Z * w1 + v2.Z * w2;
                    z = 1 / z;
                    if (z > zBuffer[y * width + x])
                    {
                        Interlocked.Exchange(ref zBuffer[y * width + x], z);
                        bufferPtr[y * width + x] = color;
                    }
                }
            }
        }
    }

    private static int ApplyLambert(this Color color, Vector3 normal, Vector3 center, List<CustomLight> lights)
    {
        float rColor = color.ScR;
        float gColor = color.ScG;
        float bColor = color.ScB;
        foreach (var light in lights)
        {
            Vector3 lightDirection = (light.Source - center);
            lightDirection = Vector3.Normalize(lightDirection);

            float intensity = MathF.Max(Vector3.Dot(normal, lightDirection) * light.Intensity, 0);
            
            rColor += intensity * light.Color.ScR;
            gColor += intensity * light.Color.ScG;
            bColor += intensity * light.Color.ScB;
        }
        return 
             (int)MathF.Min(255.0f,MathF.Round((bColor) * 255)) |
            ((int)MathF.Min(255.0f,MathF.Round(((gColor) * 255))) << 8) |
            ((int)MathF.Min(255.0f,MathF.Round(((rColor) * 255))) << 16) |
            ((int)MathF.Round(color.A) << 24);
    }
    public static void ClearBitmap(WriteableBitmap wb, System.Windows.Media.Color clearColor)
    {
        int intColor = (clearColor.A << 24) | (clearColor.R << 16) | (clearColor.G << 8) | clearColor.B;
        zBuffer = new float[wb.PixelHeight * wb.PixelWidth];
        wb.Lock();
        
        try
        {
            unsafe
            {
                int* pBackBuffer = (int*)wb.BackBuffer;

                for (int i = 0; i < wb.PixelHeight; i++)
                {
                    for (int j = 0; j < wb.PixelWidth; j++)
                    {
                        *pBackBuffer++ = intColor;
                    }
                }
            }

            wb.AddDirtyRect(new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight));
        }
        finally
        {
            wb.Unlock();
        }
    }
}