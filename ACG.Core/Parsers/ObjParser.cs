using System.Globalization;
using System.Numerics;

namespace Graphics.Core.Parsers;

public static class ObjParser
{
    public static ObjectFile Parse(string filePath)
    {
        var obj = new ObjectFile();
        
        int lineIndex = 0;
        string materialName = String.Empty;
        
        Vector4 min = new Vector4(float.MaxValue, float.MaxValue, float.MaxValue, 1.0f);
        Vector4 max = new Vector4(float.MinValue, float.MinValue, float.MinValue, 1.0f);
        foreach (var line in File.ReadLines(filePath))
        {
            var trimmedLine = line.Trim();
            
            //Skip comments and empty lines 
            if (String.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#'))
                continue;

            var tokens = trimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            //Parsing tokens by token first symbol
            switch (tokens[0])
            {
                case "mtllib":
                    if (tokens.Length < 2) 
                    {
                        throw new ArgumentException($"Incorrect tokens amount in {lineIndex} line");
                    }

                    obj.MtlFile = string.Join("", tokens[1..]);
                    break;
                case "usemtl":
                    if (tokens.Length < 2) 
                    {
                        throw new ArgumentException($"Incorrect tokens amount in {lineIndex} line");
                    }
                    materialName = tokens[1];
                    break;
                //vertex
                case "v": //v x y z [w]
                {
                    if (tokens.Length < 4)
                    {
                        throw new ArgumentException($"Incorrect tokens amount in {lineIndex} line");
                    }

                    float x = float.Parse(tokens[1],CultureInfo.InvariantCulture);
                    float y = float.Parse(tokens[2],CultureInfo.InvariantCulture);
                    float z = float.Parse(tokens[3],CultureInfo.InvariantCulture);
                    float w = tokens.Length >= 5 ? float.Parse(tokens[4],CultureInfo.InvariantCulture) : 1.0f;
                    Vector4 vertex = new Vector4(x, y, z, w);
                    obj.OriginalVertices.Add(vertex);

                    if (vertex.X < min.X) min.X = vertex.X;
                    if (vertex.Y < min.Y) min.Y = vertex.Y;
                    if (vertex.Z < min.Z) min.Z = vertex.Z;

                    if (vertex.X > max.X) max.X = vertex.X;
                    if (vertex.Y > max.Y) max.Y = vertex.Y;
                    if (vertex.Z > max.Z) max.Z = vertex.Z;
                    break;
                }
                // indicates texture coordinates (s or uv coordinates), followed by 2 floats.
                case "vt":
                {
                    // Ожидается: vt u [v] [w]
                    if (tokens.Length < 2)
                    {
                        throw new ArgumentException($"Incorrect tokens amount in {lineIndex} line");
                    }

                    float u = float.Parse(tokens[1],CultureInfo.InvariantCulture);
                    float v = tokens.Length >= 3 ? float.Parse(tokens[2],CultureInfo.InvariantCulture) : 0;
                    float w = tokens.Length >= 4 ? float.Parse(tokens[3],CultureInfo.InvariantCulture) : 0;
                    obj.TextureCoords.Add(new(u, v, w));
                    break;
                }
                case "vn":  //indicates the start of a normal definition, also followed by 3 floats.
                {
                    //vn i j k
                    if (tokens.Length < 4)
                    {
                        throw new ArgumentException($"Incorrect tokens amount in {lineIndex} line");
                    }

                    float i = float.Parse(tokens[1],CultureInfo.InvariantCulture);
                    float j = float.Parse(tokens[2],CultureInfo.InvariantCulture);
                    float k = float.Parse(tokens[3],CultureInfo.InvariantCulture);
                    obj.Normals.Add(new (i, j, k,0));
                    break;
                }
                case "f": //indicates the definition of a face.
                {
                    if (tokens.Length < 4)
                    {
                        throw new ArgumentException($"Incorrect tokens amount in {lineIndex} line");
                    }

                    var face = new Face();
                    face.MaterialName = materialName;
                    for (int i = 1; i < tokens.Length; i++)
                    {
                        var faceVertex = new FaceVertex();

                        if (tokens.Contains("//"))
                        {
                            var parts = tokens[i].Split("//");

                            if (int.TryParse(parts[0],CultureInfo.InvariantCulture, out var vertexIndex))
                            {
                                faceVertex.VertexIndex = vertexIndex;
                            }
                            else
                            {
                                throw new ArgumentException($"Parsing error: ${lineIndex} line");
                            }

                            if (parts.Length > 1 && int.TryParse(parts[1],CultureInfo.InvariantCulture, out var normIndex))
                            {
                                faceVertex.NormalIndex = normIndex;
                            }
                            else
                            {
                                throw new ArgumentException($"Parsing error: ${lineIndex} line");
                            }
                        }
                        else
                        {
                            var parts = tokens[i].Split('/');
                            
                            if (int.TryParse(parts[0],CultureInfo.InvariantCulture, out var vertexIndex))
                            {
                                faceVertex.VertexIndex = vertexIndex;
                            }
                            else
                            {
                                throw new ArgumentException($"Parsing error: ${lineIndex} line");
                            }
                            
                            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                            {
                                if (int.TryParse(parts[1], CultureInfo.InvariantCulture, out var texIndex))
                                {
                                    faceVertex.TextureIndex = texIndex;
                                }
                                else
                                {
                                    throw new ArgumentException(
                                        $"Parsing error: ${lineIndex} line");
                                }
                            }
                            
                            if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
                            {
                                if (int.TryParse(parts[2],CultureInfo.InvariantCulture, out var normIndex))
                                {
                                    faceVertex.NormalIndex = normIndex;
                                }
                                else
                                {
                                    throw new ArgumentException(
                                        $"Ошибка парсинга индекса нормали на ${lineIndex} строке");
                                }
                            }
                        }

                        face.Vertices.Add(faceVertex);
                    }

                    obj.Faces.Add(face);
                    break;
                }
                default:
                    break;
            }

            lineIndex++;
        }

        var diff = Vector4.Abs(max - min);
        float maxDiff = MathF.Max(diff.X, MathF.Max(diff.Y, diff.Z));
        
        obj.Min = min;
        obj.Max = max;

        var normalsAmount = obj.Normals.Count;
        Dictionary<int, int> vertexNormals = new Dictionary<int, int>();
        foreach (var face in obj.Faces)
        {
            int count = face.Vertices.Count();
            
            var vertexes = face.Vertices.Select(index => obj.OriginalVertices[index.VertexIndex - 1]).ToArray();
            for (int j = 1; j < count - 1; j++)
            {
                Vector4 idx0 = vertexes[0];
                Vector4 idx1 = vertexes[j];
                Vector4 idx2 = vertexes[j + 1];
                
                Vector4 v1 = idx1 - idx0;
                Vector4 v2 = idx2 - idx0;
                Vector3 normal3 = Vector3.Normalize(Vector3.Cross(
                    new Vector3(v1.X, v1.Y, v1.Z),
                    new Vector3(v2.X, v2.Y, v2.Z)));

                var normal = new Vector4(normal3, 0);
                
                //TODO: make better
                if (face.Vertices[0].NormalIndex == 0)
                {
                    if (!vertexNormals.ContainsKey(face.Vertices[0].VertexIndex))
                    {
                        obj.Normals.Add(normal);
                        vertexNormals[face.Vertices[0].VertexIndex] = obj.Normals.Count;
                        face.Vertices[0].NormalIndex = vertexNormals[face.Vertices[0].VertexIndex];
                    }
                    else
                    {
                        face.Vertices[0].NormalIndex = vertexNormals[face.Vertices[0].VertexIndex];
                        obj.Normals[face.Vertices[0].NormalIndex - 1] += normal;
                    }
                }
                
                if (face.Vertices[j].NormalIndex == 0)
                {
                    if (!vertexNormals.ContainsKey(face.Vertices[j].VertexIndex))
                    {
                        obj.Normals.Add(normal);
                        vertexNormals[face.Vertices[j].VertexIndex] = obj.Normals.Count;
                        face.Vertices[j].NormalIndex = vertexNormals[face.Vertices[j].VertexIndex];
                    }
                    else
                    {
                        face.Vertices[j].NormalIndex = vertexNormals[face.Vertices[j].VertexIndex];
                        obj.Normals[face.Vertices[j].NormalIndex - 1] += normal;
                    }
                }
                
                if (face.Vertices[j + 1].NormalIndex == 0)
                {
                    if (!vertexNormals.ContainsKey(face.Vertices[j+1].VertexIndex))
                    {
                        obj.Normals.Add(normal);
                        vertexNormals[face.Vertices[j+1].VertexIndex] = obj.Normals.Count;
                        face.Vertices[j+1].NormalIndex = vertexNormals[face.Vertices[j+1].VertexIndex];
                    }
                    else
                    {
                        face.Vertices[j+1].NormalIndex = vertexNormals[face.Vertices[j+1].VertexIndex];
                        obj.Normals[face.Vertices[j+1].NormalIndex - 1] += normal;
                    }
                }
            }
        }

        for (int i = 0; i < obj.Normals.Count; i++)
        {
            obj.Normals[i] = Vector4.Normalize(obj.Normals[i]);
        }
        
        return obj;
    }
}