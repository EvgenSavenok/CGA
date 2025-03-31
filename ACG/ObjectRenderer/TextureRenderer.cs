using System.Collections.Immutable;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Graphics.Core;
using Graphics.Core.Objects;
using Graphics.UI.Objects.Light;
using Graphics.UI.Objects.Textures;

namespace Graphics.UI.ObjectRenderer;

public static class TextureRenderer
{
    private static float[]? _zBuffer;
    
    private static object _zLock = new ();

    public static void DrawObject(
        Vector4[] transformedVertices, 
        ObjectModel model, 
        Camera camera, 
        WriteableBitmap wb,
        List<CustomLight> lights, 
        List<CustomMaterial> materials,
        LightParameters lightParameters, 
        Dictionary<string,Texture> textures
    )
    {
        _zBuffer = new float[wb.PixelHeight * wb.PixelWidth];
        wb.Lock();

        var matrix = model.GetWorldMatrix();
        var pBackBuffer = wb.BackBuffer;
        var width = wb.PixelWidth;
        var height = wb.PixelHeight;

        var lightDictionary = InitializeLightDictionary(materials, lightParameters);
        
        Parallel.ForEach(model.Object.Faces, face =>
        {
            ProcessFace(face, transformedVertices, model, camera, pBackBuffer, width, height, lights, lightDictionary, lightParameters, materials, textures, matrix);
        });
        
        wb.AddDirtyRect(new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight));
        wb.Unlock();
    }
    
    private static Dictionary<string, LightParameters> InitializeLightDictionary(
        List<CustomMaterial> materials, 
        LightParameters lightParameters)
    {
        Dictionary<string, LightParameters> lightDictionary = new();

        foreach (var material in materials)
        {
            string materialName = material.Name;

            Vector3 ambientVector = material.AmbientColor;
            Vector3 diffuseVector = material.DiffuseColor;
            Vector3 specularVector = material.SpecularColor;

            var faceLightParameters = new LightParameters
            {
                AmbientCoeff = lightParameters.AmbientCoeff,
                AmbientColor = Color.FromScRgb(1f, ambientVector.X, ambientVector.Y, ambientVector.Z),
                DiffuseCoeff = lightParameters.DiffuseCoeff,
                DiffuseColor = Color.FromScRgb(1f, diffuseVector.X, diffuseVector.Y, diffuseVector.Z),
                SpecularCoeff = lightParameters.SpecularCoeff,
                SpecularColor = Color.FromScRgb(1f, specularVector.X, specularVector.Y, specularVector.Z),
                Shininess = material.Shininess
            };

            lightDictionary.Add(materialName, faceLightParameters);
        }

        return lightDictionary;
    }
    
    private static void ProcessFace(
        Face face,
        Vector4[] transformedVertices,
        ObjectModel model,
        Camera camera,
        IntPtr pBackBuffer,
        int width,
        int height,
        List<CustomLight> lights,
        Dictionary<string, LightParameters> lightDictionary,
        LightParameters defaultLightParameters,
        List<CustomMaterial> materials,
        Dictionary<string, Texture> textures,
        Matrix4x4 matrix)
    {
        int count = face.Vertices.Count;
        if (count < 2)
            return;

        var vertices = face.Vertices.Select(index => transformedVertices[index.VertexIndex - 1]).ToImmutableArray();
        var normals = face.Vertices.Select(index => model.TransformedNormals[index.NormalIndex - 1]).ToArray();
        var worldVertices = face.Vertices.Select(index => model.TransformedVertices[index.VertexIndex - 1]).ToImmutableArray();
        var texturePositions = face.Vertices.Select(index => model.Object.TextureCoords[index.TextureIndex - 1]).ToImmutableArray(); 
        
        string materialName = face.MaterialName;
        LightParameters faceLightParameters = lightDictionary.GetValueOrDefault(materialName, defaultLightParameters);

        var material = materials.FirstOrDefault(a => a.Name == materialName);
        Texture? diffuseTexture = null;
        Texture? normalTexture = null;
        Texture? specularTexture = null;

        if (material != null && textures.Count > 0)
        {
            textures.TryGetValue(material.DiffuseMap, out diffuseTexture);
            textures.TryGetValue(material.NormalMap, out normalTexture);
            textures.TryGetValue(material.SpecularMap, out specularTexture);
        }

        ProcessTriangles(
            vertices.ToArray(), 
            texturePositions.ToArray(), 
            worldVertices.ToArray(), 
            normals, 
            camera, pBackBuffer, width, height, 
            lights, faceLightParameters, 
            diffuseTexture, normalTexture, specularTexture, matrix);
    }
        
    private static void ProcessTriangles(
        Vector4[] vertices,
        Vector3[] texturePositions,
        Vector4[] worldVertices,
        Vector4[] normals,
        Camera camera,
        IntPtr pBackBuffer,
        int width,
        int height,
        List<CustomLight> lights,
        LightParameters faceLightParameters,
        Texture? diffuseTexture,
        Texture? normalTexture,
        Texture? specularTexture,
        Matrix4x4 matrix)
    {
        int count = vertices.Length;
        
        for (int j = 1; j < count - 1; j++)
        {
            Vector4 idx0 = vertices[0];
            Vector4 idx1 = vertices[j];
            Vector4 idx2 = vertices[j + 1];

            Vector3 uv0 = texturePositions[0];
            Vector3 uv1 = texturePositions[j];
            Vector3 uv2 = texturePositions[j + 1];

            Vector4 v1 = worldVertices[j] - worldVertices[0];
            Vector4 v2 = worldVertices[j + 1] - worldVertices[0];
            Vector3 normal = Vector3.Cross(
                new Vector3(v1.X, v1.Y, v1.Z),
                new Vector3(v2.X, v2.Y, v2.Z));

            Vector4 center4 = (worldVertices[0] + worldVertices[j] + worldVertices[j + 1]) / 3.0f;
            var center = new Vector3(center4.X, center4.Y, center4.Z);
            var cameraDirection = (center - camera.EyeCoords);

            if (Vector3.Dot(normal, cameraDirection) <= 0)
            {
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
                    RasterizeTriangle(
                        pBackBuffer, width, height,
                        idx0, idx1, idx2,
                        normals[0], normals[j], normals[j + 1],
                        uv0, uv1, uv2,
                        worldVertices[0], worldVertices[j], worldVertices[j + 1],
                        lights, faceLightParameters, camera,
                        diffuseTexture, normalTexture, specularTexture,
                        matrix
                    );
                }
            }
        }
    }
        
    private static float EdgeFunction(Vector4 a, Vector4 b, Vector4 c)
    {
        return (c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X);
    }
    private static unsafe void RasterizeTriangle(IntPtr buffer,  int width, int height,
        Vector4 v0, Vector4 v1, Vector4 v2,
        Vector4 n0, Vector4 n1, Vector4 n2,
        Vector3 uv0, Vector3 uv1, Vector3 uv2,
        Vector4 world0, Vector4 world1, Vector4 world2,
        List<CustomLight> lights, LightParameters lightParameters, Camera camera,
        Texture? diffuseTexture, Texture? normalMap, Texture? specularMap,
        Matrix4x4 modelWorldMatrix
        )
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
        
        float area =  EdgeFunction(v0, v1, v2);
        
        for (var y = yMin; y <= yMax; y++)
        {
            for (var x = xMin; x <= xMax; x++)
            {
                Vector4 pixel = new Vector4(x, y, 0, 1);
                float w0 =  EdgeFunction(v1, v2, pixel);
                float w1 =  EdgeFunction(v2, v0, pixel);
                float w2 =  EdgeFunction(v0, v1, pixel);
                
                if (w0 >= 0 && w1 >= 0 && w2 >= 0)
                {
                    var w0Old = v0.W;
                    var w1Old = v1.W;
                    var w2Old = v2.W;
                    
                    w0 /= area;
                    w1 /= area;
                    w2 /= area;

                    float z = v0.Z * w0 + v1.Z * w1 + v2.Z * w2;
                    
                    var index = y * width + x;
                    
                    z = 1.0f / z;
                    //lock (zLock) 
                    //{
                        if (_zBuffer != null && z > _zBuffer[index])
                        {
                            float div = w0 / w0Old + w1 / w1Old + w2 / w2Old;
                            
                            float u = ((w0 * uv0.X) / w0Old + (w1 * uv1.X) / w1Old + (w2 * uv2.X) / w2Old) / div;
                            float v = ((w0 * uv0.Y) / w0Old + (w1 * uv1.Y) / w1Old + (w2 * uv2.Y) / w2Old) / div;

                            v = 1.0f - v;

                            u = Math.Clamp(u, 0.0f, 1.0f);
                            v = Math.Clamp(v, 0.0f, 1.0f);

                            //Diffuse map
                            Color diffuseColor;
                            diffuseColor = diffuseTexture?.GetColor(u, v) ?? Colors.Fuchsia;

                            //Normal map
                            Vector3 normal;
                            if (normalMap == null)
                            {
                                Vector4 normal4 = (n0 * w0 + n1 * w1 + n2 * w2);
                                normal = new Vector3(normal4.X, normal4.Y, normal4.Z);
                            }
                            else
                            {
                                Color normalColor = normalMap.GetColor(u, v);
                                Vector3 sampledNormal = new Vector3(
                                    normalColor.ScR * 2 - 1,
                                    normalColor.ScG * 2 - 1,
                                    normalColor.ScB * 2 - 1
                                );
                                normal = Vector3.TransformNormal(sampledNormal, modelWorldMatrix);
                                normal = Vector3.Normalize(normal);
                            }
                            
                            //Specular color
                            float specularStrength = 1.0f;
                            Color specularColor = lightParameters.SpecularColor;

                            if (specularMap != null)
                            {
                                specularColor = specularMap.GetColor(u, v);
                                specularStrength = (specularColor.ScR + specularColor.ScG + specularColor.ScB) /
                                                   3.0f;
                            }

                            float specularCoeff = lightParameters.SpecularCoeff * specularStrength;
                            
                            Vector4 position4 = (world0 * w0 + world1 * w1 + world2 * w2);
                            Vector3 position = new Vector3(position4.X, position4.Y, position4.Z);

                            int phongColor = ApplyPhongShading(
                                normal, position, lights, lightParameters,
                                diffuseColor,
                                specularColor, specularCoeff,
                                camera);
                           
                                Interlocked.Exchange(ref _zBuffer[index], z);

                                Interlocked.Exchange(ref bufferPtr[index], phongColor);
                        }
                    //}
                }
            }
        }
    }

    private static int ApplyPhongShading(
        Vector3 normal, 
        Vector3 center,
        List<CustomLight> lights,
        LightParameters lightParameters,
        Color diffuseColor,
        Color specularColor, 
        float specularCoeff,
        Camera camera)
    {
        normal = Vector3.Normalize(normal);
        Vector3 viewDir = Vector3.Normalize(camera.EyeCoords - center);
        
        float rColor = lightParameters.AmbientColor.ScR * lightParameters.AmbientCoeff; 
        float gColor = lightParameters.AmbientColor.ScG * lightParameters.AmbientCoeff;
        float bColor = lightParameters.AmbientColor.ScB * lightParameters.AmbientCoeff;

        foreach (var light in lights)
        {
            Vector3 lightDirection = (light.SourceOfLight - center);
            lightDirection = Vector3.Normalize(lightDirection);

            float intensity = MathF.Max(Vector3.Dot(normal, lightDirection) * light.Intensity, 0);
            
            
            rColor += intensity * light.Color.ScR * lightParameters.DiffuseCoeff * diffuseColor.ScR;
            gColor += intensity * light.Color.ScG * lightParameters.DiffuseCoeff * diffuseColor.ScG;
            bColor += intensity * light.Color.ScB * lightParameters.DiffuseCoeff * diffuseColor.ScB;
            
            float specular = PhongShadingRenderer.ComputeSpecular(normal, lightDirection, viewDir, lightParameters.Shininess);
            
            rColor += (specularCoeff * specular) * light.Color.ScR * specularColor.ScR;
            gColor += (specularCoeff * specular) * light.Color.ScG * specularColor.ScG;
            bColor += (specularCoeff * specular) * light.Color.ScB * specularColor.ScB;
        }
        
        return 
            (int)MathF.Min(255.0f,MathF.Round((bColor) * 255)) |
            ((int)MathF.Min(255.0f,MathF.Round(((gColor) * 255))) << 8) |
            ((int)MathF.Min(255.0f,MathF.Round(((rColor) * 255))) << 16) |
            (lightParameters.AmbientColor.A << 24);
    }
}