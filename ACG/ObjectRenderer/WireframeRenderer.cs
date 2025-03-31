using System.Numerics;
using System.Windows;
using System.Windows.Media.Imaging;
using Graphics.Core;

namespace Graphics.UI.ObjectRenderer;

public static class WireframeRenderer
{
    public static void DrawObject(Vector4[] transformedVertices,ObjectModel model,
        Camera camera, WriteableBitmap wb, System.Windows.Media.Color color)
    {
        wb.Lock();
        int intColor = (color.B << 0) | (color.G << 8) | (color.R << 16) | (color.A << 24);
        var pBackBuffer = wb.BackBuffer;
        var width = wb.PixelWidth;
        var height = wb.PixelHeight;
        foreach (var face in model.Object.Faces)
        {
            int count = face.Vertices.Count();
            if (count < 2)
                continue;

            for (int i = 0; i < count; i++)
            {
                int index1 = face.Vertices[i].VertexIndex - 1;
                int index2 = face.Vertices[(i + 1) % count].VertexIndex - 1;
                
                if (index1 < 0 || index1 >= transformedVertices.Length ||
                    index2 < 0 || index2 >= transformedVertices.Length)
                    continue;
                
                int x0 = (int)Math.Round(transformedVertices[index1].X);
                int y0 = (int)Math.Round(transformedVertices[index1].Y);
                int x1 = (int)Math.Round(transformedVertices[index2].X);
                int y1 = (int)Math.Round(transformedVertices[index2].Y);
                
                float z0 = transformedVertices[index1].Z;
                float z1 = transformedVertices[index2].Z;

                if (!((x0 >= width && x1 >= width) || (x0 <= 0 && x1 <= 0) || (y0 >= height && y1 >= height) ||
                    (y0 <= 0 && y1 <= 0) || (z0 < camera.ZNear || z1 < camera.ZNear) || (z0 > camera.ZFar || z1 > camera.ZFar)))
                {
                    DrawLineBresenham(pBackBuffer, width, height, x0, y0, x1, y1,intColor);
                }
            }
        }
        wb.AddDirtyRect(new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight));
        wb.Unlock();
    }

    private static unsafe void DrawLineBresenham(IntPtr buffer, int width, int height, int x0, int y0, int x1, int y1,
        int color)
    {
        int* bufferPtr = (int*)buffer;
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
            {
                bufferPtr[y0 * width + x0] = color;
            }

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
        
    }
    
    public static void ClearBitmap(WriteableBitmap wb, System.Windows.Media.Color clearColor)
    {
        int intColor = (clearColor.A << 24) | (clearColor.R << 16) | (clearColor.G << 8) | clearColor.B;

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