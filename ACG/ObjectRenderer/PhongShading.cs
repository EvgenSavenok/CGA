    using System.Numerics;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Graphics.Core;

    namespace Graphics.UI.Render;

    public static class PhongShading
    {
        private static float[] zBuffer;


        public static void DrawObject(Vector4[] transformedVertices, ObjectModel model, Camera camera, WriteableBitmap wb,
            List<CustomLight> lights, List<CustomMaterial> materials, LightParameters lightParameters
        )
        {
            zBuffer = new float[wb.PixelHeight * wb.PixelWidth];
            wb.Lock();

            var pBackBuffer = wb.BackBuffer;
            var width = wb.PixelWidth;
            var height = wb.PixelHeight;

            Dictionary<string, LightParameters> lightDictionary = new();
            
            foreach (var material in materials)
            {
                string materialName = material.Name;

                Vector3 ambientVector = material.AmbientColor;
                Vector3 diffuseVector = material.DiffuseColor;
                Vector3 specularVector = material.SpecularColor;

                var faceLightParameters = new LightParameters()
                {
                    AmbientCoeff = lightParameters.AmbientCoeff,
                    AmbientColor = Color.FromScRgb(1f, ambientVector.X, ambientVector.Y, ambientVector.Z),
                    DiffuseCoeff = lightParameters.DiffuseCoeff,
                    DiffuseColor = Color.FromScRgb(1f, diffuseVector.X, diffuseVector.Y, diffuseVector.Z),
                    SpecularCoeff = lightParameters.SpecularCoeff,
                    SpecularColor = Color.FromScRgb(1f, specularVector.X, specularVector.Y, specularVector.Z),
                    Shininess = material.Shininess
                };

                lightDictionary.Add(materialName,faceLightParameters);
            }
            
            Parallel.ForEach(model.Object.Faces, face =>
            {
                int count = face.Vertices.Count();
                if (count < 2)
                    return;
                
                var vertices = face.Vertices.Select(index => transformedVertices[index.VertexIndex - 1]).ToArray();
                var normals = face.Vertices.Select(index => model.TransformedNormals[index.NormalIndex - 1]).ToArray();
                var worldVertices = face.Vertices.Select(index => model.TransformedVertices[index.VertexIndex - 1]).ToArray();

                string materialName = face.MaterialName;
                LightParameters faceLightParameters;
                if (lightDictionary.ContainsKey(materialName))
                {
                    faceLightParameters = lightDictionary[materialName];
                }
                else
                {
                    faceLightParameters = lightParameters;
                }
                
                
                for (int j = 1; j < count - 1; j++)
                {
                    Vector4 idx0 = vertices[0];
                    Vector4 idx1 = vertices[j];
                    Vector4 idx2 = vertices[j + 1];
                    
                    
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
                            RasterizeTriangle(pBackBuffer, width, height,
                                idx0, idx1, idx2,
                                normals[0], normals[j], normals[j + 1],
                                worldVertices[0], worldVertices[j], worldVertices[j + 1],
                                lights, faceLightParameters, camera);
                        }
                    }
                }
            });
            wb.AddDirtyRect(new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight));
            wb.Unlock();
        }
        public static void DrawObject(Vector4[] transformedVertices, ObjectModel model, Camera camera, WriteableBitmap wb,
            List<CustomLight> lights, LightParameters lightParameters
            )
        {
            zBuffer = new float[wb.PixelHeight * wb.PixelWidth];
            wb.Lock();

            var pBackBuffer = wb.BackBuffer;
            var width = wb.PixelWidth;
            var height = wb.PixelHeight;
            Parallel.ForEach(model.Object.Faces, face =>
            {
                int count = face.Vertices.Count();
                if (count < 2)
                    return;
                
                var vertices = face.Vertices.Select(index => transformedVertices[index.VertexIndex - 1]).ToArray();
                var normals = face.Vertices.Select(index => model.TransformedNormals[index.NormalIndex - 1]).ToArray();
                var worldVertices = face.Vertices.Select(index => model.TransformedVertices[index.VertexIndex - 1]).ToArray();
                for (int j = 1; j < count - 1; j++)
                {
                    Vector4 idx0 = vertices[0];
                    Vector4 idx1 = vertices[j];
                    Vector4 idx2 = vertices[j + 1];
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
                        RasterizeTriangle(pBackBuffer, width, height, 
                            idx0, idx1, idx2,
                            normals[0],normals[j],normals[j+1],
                            worldVertices[0],worldVertices[j],worldVertices[j+1],
                            lights, lightParameters, camera);
                    }
                }
            });
            wb.AddDirtyRect(new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight));
            wb.Unlock();
        }

        public static float EdgeFunction(Vector4 a, Vector4 b, Vector4 c)
        {
            return (c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X);
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

                        var index = y * width + x;
                        if (z > zBuffer[index])
                        {
                            Vector4 normal4 = (n0 * w0 + n1 * w1 + n2 * w2);
                            Vector3 normal = new Vector3(normal4.X, normal4.Y, normal4.Z);
                            Vector4 position4 = (world0 * w0 + world1 * w1 + world2 * w2);
                            Vector3 position = new Vector3(position4.X, position4.Y, position4.Z);
                            int phongColor = ApplyPhongShading(normal, position, lights, lightParameters, camera);    
                            Interlocked.Exchange(ref zBuffer[index], z);
                            bufferPtr[index] = phongColor;
                        }
                    }
                }
            }
        }

        public static int ApplyPhongShading(Vector3 normal, Vector3 center,
            List<CustomLight> lights, LightParameters lightParameters, Camera camera)
        {
            normal = Vector3.Normalize(normal);
            Vector3 viewDir = Vector3.Normalize(camera.EyeCoords - center);
            
            float rColor = lightParameters.AmbientColor.ScR * lightParameters.AmbientCoeff; 
            float gColor = lightParameters.AmbientColor.ScG * lightParameters.AmbientCoeff;
            float bColor = lightParameters.AmbientColor.ScB * lightParameters.AmbientCoeff;

            foreach (var light in lights)
            {
                Vector3 lightDirection = (light.Source - center);
                lightDirection = Vector3.Normalize(lightDirection);

                float intensity = MathF.Max(Vector3.Dot(normal, lightDirection) * light.Intensity, 0);
                
                
                rColor += intensity * light.Color.ScR * lightParameters.DiffuseCoeff * lightParameters.DiffuseColor.ScR;
                gColor += intensity * light.Color.ScG * lightParameters.DiffuseCoeff * lightParameters.DiffuseColor.ScG;
                bColor += intensity * light.Color.ScB * lightParameters.DiffuseCoeff * lightParameters.DiffuseColor.ScB;
                
                float specular = ComputeSpecular(normal, lightDirection, viewDir, lightParameters.Shininess);
                
                rColor += (lightParameters.SpecularCoeff * specular) * light.Color.ScR * lightParameters.SpecularColor.ScR;
                gColor += (lightParameters.SpecularCoeff * specular) * light.Color.ScG * lightParameters.SpecularColor.ScG;
                bColor += (lightParameters.SpecularCoeff * specular) * light.Color.ScB * lightParameters.SpecularColor.ScB;
            }
            
            /**
            return (lightParameters.DiffuseColor.B) |
                   (lightParameters.DiffuseColor.G << 8) |
                   ((lightParameters.DiffuseColor.R) << 16) |
                   (255) << 24;
                   **/
            return 
                (int)MathF.Min(255.0f,MathF.Round((bColor) * 255)) |
                ((int)MathF.Min(255.0f,MathF.Round(((gColor) * 255))) << 8) |
                ((int)MathF.Min(255.0f,MathF.Round(((rColor) * 255))) << 16) |
                ((int)(lightParameters.AmbientColor.A) << 24);
        }
        
        public static float ComputeSpecular(Vector3 normal, Vector3 lightDir, Vector3 viewDir, float shininess)
        {
            Vector3 reflectedLight = Vector3.Reflect(-lightDir, normal);  
            float specFactor = MathF.Max(Vector3.Dot(reflectedLight, viewDir), 0.0f);
            return MathF.Pow(specFactor, shininess); 
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