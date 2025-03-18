using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Graphics.Core;
using Graphics.UI.Light;

namespace Graphics.UI.ObjectRenderer;

public static class PhongShading
{
    private static float[]? _zBuffer;
    
    public static void DrawObject(
        Vector4[] transformedVertices, 
        ObjectModel model, 
        Camera camera, 
        WriteableBitmap wb,
        List<CustomLight> lights, 
        LightParameters lightParameters)
    {
        _zBuffer = new float[wb.PixelHeight * wb.PixelWidth];
        wb.Lock();

        var buffer = wb.BackBuffer;
        var width = wb.PixelWidth;
        var height = wb.PixelHeight;

        Parallel.ForEach(model.Object.Faces, face =>
        {
            DrawFace(face, transformedVertices, model, camera, buffer, width, height, lights, lightParameters);
        });

        wb.AddDirtyRect(new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight));
        wb.Unlock();
    }

    private static void DrawFace(Face face, Vector4[] transformedVertices, ObjectModel model, Camera camera, IntPtr buffer, int width, int height, List<CustomLight> lights, LightParameters lightParameters)
    {
        int count = face.Vertices.Count;
        if (count < 3)
            return;

        var vertices = face.Vertices.Select(index => transformedVertices[index.VertexIndex - 1]).ToArray();
        var normals = face.Vertices.Select(index => model.TransformedNormals[index.NormalIndex - 1]).ToArray();
        var worldVertices = face.Vertices.Select(index => model.TransformedVertices[index.VertexIndex - 1]).ToArray();

        for (int j = 1; j < count - 1; j++)
        {
            Vector4 idx0 = vertices[0];
            Vector4 idx1 = vertices[j];
            Vector4 idx2 = vertices[j + 1];

            if (IsBackFace(idx0, idx1, idx2, camera))
            {
                RasterizeTriangle(
                    buffer, width, height, 
                    idx0, idx1, idx2,
                    normals[0], normals[j], normals[j + 1],
                    worldVertices[0], worldVertices[j], worldVertices[j + 1],
                    lights, lightParameters, 
                    camera);
            }
        }
    }

    private static bool IsBackFace(Vector4 idx0, Vector4 idx1, Vector4 idx2, Camera camera)
    {
        int countNear = 0;
        int countFar = 0;

        if (idx0.Z < camera.ZNear) 
            countNear++;
        if (idx1.Z < camera.ZNear) 
            countNear++;
        if (idx2.Z < camera.ZNear) 
            countNear++;

        if (idx0.Z > camera.ZFar) 
            countFar++;
        if (idx1.Z > camera.ZFar) 
            countFar++;
        if (idx2.Z > camera.ZFar)
            countFar++;

        return countFar + countNear < 1;
    }
    
    private static unsafe void RasterizeTriangle(IntPtr buffer, int width, int height,
        Vector4 v0, Vector4 v1, Vector4 v2,
        Vector4 n0, Vector4 n1, Vector4 n2,
        Vector4 world0, Vector4 world1, Vector4 world2,
        List<CustomLight> lights, LightParameters lightParameters, Camera camera)
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
        
        float denom = (v2.X - v0.X) * (v1.Y - v0.Y) - (v2.Y - v0.Y) * (v1.X - v0.X);
        if (Math.Abs(denom) < float.Epsilon) 
            return; 
        
        for (var y = yMin; y <= yMax; y++)
        {
            if (y < 0 || y >= height)
                return;
            
            for (var x = xMin; x <= xMax; x++)
            {
                if (x < 0 || x >= width)
                    continue;
                
                Vector4 pixel = new Vector4(x + 0.5f, y + 0.5f, 0, 0);
                
                float alpha = (pixel.X - v1.X) * (v2.Y - v1.Y) - (pixel.Y - v1.Y) * (v2.X - v1.X);
                float beta = (pixel.X - v2.X) * (v0.Y - v2.Y) - (pixel.Y - v2.Y) * (v0.X - v2.X);
                float gamma = (pixel.X - v0.X) * (v1.Y - v0.Y) - (pixel.Y - v0.Y) * (v1.X - v0.X); 
                
                if (alpha >= 0 && beta >= 0 && gamma >= 0)
                {
                    // Нормализуем, чтобы в сумме давали 1
                    alpha /= denom;
                    beta /= denom;
                    gamma /= denom;
                    
                    float depth = v0.Z * alpha + v1.Z * beta + v2.Z * gamma;
                    
                    depth = 1 / depth;

                    var index = y * width + x;
                    
                    if (depth > _zBuffer![index])
                    {
                        // Интерполируем нормаль (нужна для освещения)
                        Vector4 normal4 = (n0 * alpha + n1 * beta + n2 * gamma);
                        // Приводим к 3D
                        Vector3 normal = new Vector3(normal4.X, normal4.Y, normal4.Z);
                        
                        // Позиция пикселя
                        Vector4 position4 = (world0 * alpha + world1 * beta + world2 * gamma);
                        Vector3 position = new Vector3(position4.X, position4.Y, position4.Z);
                        
                        int phongColor = ApplyPhongShading(
                            normal, 
                            position,
                            lights, 
                            lightParameters, 
                            camera);    
                        
                        _zBuffer[index] = depth;
                        bufferPtr[index] = phongColor;
                    }
                }
            }
        }
    }

    private static int ApplyPhongShading(
        Vector3 normal, 
        Vector3 center,
        List<CustomLight> lights,
        LightParameters lightParameters,
        Camera camera)
    {
        normal = NormalizeNormal(normal);
        Vector3 viewDir = CalculateViewDirection(center, camera);

        // Рассчитываем фоновое освещение
        var (rColor, gColor, bColor) = ComputeAmbientLighting(lightParameters);

        // Для каждого источника света вычисляем его вклад в освещение
        foreach (var light in lights)
        {
            Vector3 lightDirection = Vector3.Normalize(light.SourceOfLight - center);

            // Рассчитываем рассеянное освещение
            var diffuse = ComputeDiffuseLighting(normal, lightDirection, light, lightParameters, (rColor, gColor, bColor));

            // Рассчитываем зеркальное освещение
            var finalColor = ComputeSpecularLighting(normal, lightDirection, viewDir, light, lightParameters, diffuse);

            rColor = finalColor.rColor;
            gColor = finalColor.gColor;
            bColor = finalColor.bColor;
        }

        int pixelWithShadow = 
               (int)MathF.Min(255.0f, MathF.Round((bColor) * 255)) |
               ((int)MathF.Min(255.0f, MathF.Round(((gColor) * 255))) << 8) |
               ((int)MathF.Min(255.0f, MathF.Round(((rColor) * 255))) << 16) |
               (lightParameters.AmbientColor.A << 24);
        
        return pixelWithShadow;
    }

    private static Vector3 NormalizeNormal(Vector3 normal)
    {
        return Vector3.Normalize(normal);
    }

    private static Vector3 CalculateViewDirection(Vector3 center, Camera camera)
    {
        return Vector3.Normalize(camera.EyeCoords - center);
    }

    private static (float rColor, float gColor, float bColor) ComputeAmbientLighting(LightParameters lightParameters)
    {
        float rColor = lightParameters.AmbientColor.ScR * lightParameters.AmbientCoeff;
        float gColor = lightParameters.AmbientColor.ScG * lightParameters.AmbientCoeff;
        float bColor = lightParameters.AmbientColor.ScB * lightParameters.AmbientCoeff;
    
        return (rColor, gColor, bColor);
    }

    private static (float rColor, float gColor, float bColor) ComputeDiffuseLighting(
        Vector3 normal, 
        Vector3 lightDirection, 
        CustomLight light,
        LightParameters lightParameters,
        (float rColor, float gColor, float bColor) ambient)
    {
        float intensity = MathF.Max(Vector3.Dot(normal, lightDirection) * light.Intensity, 0);
    
        float rColor = intensity * light.Color.ScR * lightParameters.DiffuseCoeff * lightParameters.DiffuseColor.ScR;
        float gColor = intensity * light.Color.ScG * lightParameters.DiffuseCoeff * lightParameters.DiffuseColor.ScG;
        float bColor = intensity * light.Color.ScB * lightParameters.DiffuseCoeff * lightParameters.DiffuseColor.ScB;
    
        return (rColor, gColor, bColor);
    }

    private static (float rColor, float gColor, float bColor) ComputeSpecularLighting(Vector3 normal, Vector3 lightDirection, Vector3 viewDir, CustomLight light, LightParameters lightParameters, (float rColor, float gColor, float bColor) diffuse)
    {
        float specular = ComputeSpecular(normal, lightDirection, viewDir, lightParameters.Shininess);
    
        // Суммируем зеркальное и рассеянное освещение
        float rColor = diffuse.rColor + specular * light.Color.ScR * lightParameters.SpecularColor.ScR;
        float gColor = diffuse.gColor + specular * light.Color.ScG * lightParameters.SpecularColor.ScG;
        float bColor = diffuse.bColor + specular * light.Color.ScB * lightParameters.SpecularColor.ScB;
    
        return (rColor, gColor, bColor);
    }

    // (R * V)^a
    private static float ComputeSpecular(Vector3 normal, Vector3 lightDir, Vector3 viewDir, float shininess)
    {
        // Вычисляется отраженный вектор R
        Vector3 reflectedLight = Vector3.Reflect(-lightDir, normal);  
        // Вычисляется скалярное произведение R и вектора камеры
        float specFactor = MathF.Max(Vector3.Dot(reflectedLight, viewDir), 0.0f);
        // Возводим в степень размытия блика, чтобы его контролировать 
        // Если shininess большой, то блик будет маленьким, но ярким
        // Если shininess маленький, то блик будет большим и размытым
        return MathF.Pow(specFactor, shininess); 
    }
    
    public static void ClearBitmap(WriteableBitmap wb, Color clearColor)
    {
        int intColor = (clearColor.A << 24) | (clearColor.R << 16) | (clearColor.G << 8) | clearColor.B;
        wb.Lock();
        try
        {
            unsafe
            {
                int* pBackBuffer = (int*)wb.BackBuffer;

                int pixelCount = wb.PixelHeight * wb.PixelWidth;
                for (int i = 0; i < pixelCount; i++)
                    pBackBuffer[i] = intColor;
            }
            wb.AddDirtyRect(new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight));
        }
        finally
        {
            wb.Unlock();
        }
    }
}