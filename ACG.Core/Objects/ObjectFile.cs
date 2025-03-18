using System.Numerics;

namespace Graphics.Core;

public class ObjectFile
{
    private float _scale;
    public List<Vector4> OriginalVertices { get; } = [];
    public List<Vector3> TextureCoords { get; } = [];
    public List<Vector4> Normals { get; } = [];
    public List<Face> Faces { get; } = [];
    
    public Vector4 Min { get; set; }
    public Vector4 Max { get; set; }
    
    public string MtlFile { get; set; }
}