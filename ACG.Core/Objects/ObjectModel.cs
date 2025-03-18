using System.Drawing;
using System.Numerics;
using Graphics.Core.Transformations;

namespace Graphics.Core;

public class ObjectModel
{
    private float _scale;
    private Vector3 _translation = Vector3.Zero;
    private Vector3 _rotation = Vector3.Zero;
    public Vector4[] TransformedVertices { get; set; } = [];
    public Vector4[] TransformedNormals { get; set; } = [];
    private ObjectFile _object = new ObjectFile();
    public ObjectFile Object
    {
        get => _object;
        set
        {
            _object = value;
            TransformedVertices = new Vector4[value.OriginalVertices.Count];
            TransformedNormals = new Vector4[value.Normals.Count];
            var diff = Vector4.Abs(value.Max - value.Min);
            var maxDiff = MathF.Max(diff.X, MathF.Max(diff.Y, diff.Z));
            Scale = 2.0f / (maxDiff == 0 ? 1 : maxDiff);
        }
    }
    public float Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            Delta = _scale / 10.0f;
            OnTransformationChanged();
        }
    }
    
    public Vector3 Translation
    {
        get => _translation;
        set
        {
            if (_translation != value)
            {
                _translation = value;
                OnTransformationChanged();
            }
        }
    }
    
    public Vector3 Rotation
    {
        get => _rotation;
        set
        {
            if (_rotation != value)
            {
                _rotation = value;
                OnTransformationChanged();
            }
        }
    }
    
    public float Delta { get; set; }
    
    public Size WindowSize { get; set; }
    
    public event EventHandler? TransformationChanged;
    
    protected virtual void OnTransformationChanged()
    {
        UpdateImage();
        TransformationChanged?.Invoke(this, EventArgs.Empty);
    }

    public Matrix4x4 GetWorldMatrix()
    {
        var rotationMatrix = Matrix4x4.CreateFromYawPitchRoll(Rotation.Y,Rotation.X, Rotation.Z);
        Matrix4x4.Invert(rotationMatrix, out var rotationMatrixInv);
        var worldTransform = Transformation.CreateWorldTransform(Scale, rotationMatrix, Translation);
        return worldTransform;
    }
    public void UpdateImage()
    {
        // Start point to change TransformedVertices
        var rotationMatrix = Matrix4x4.CreateFromYawPitchRoll(Rotation.Y,Rotation.X, Rotation.Z);
        Matrix4x4.Invert(rotationMatrix, out var rotationMatrixInv);
        var worldTransform = Transformation.CreateWorldTransform(Scale, rotationMatrix, Translation);
        int count = this.Object.OriginalVertices.Count;
        Parallel.For(0, count, i =>
        {
            var v = Vector4.Transform(this.Object.OriginalVertices[i], worldTransform);
            this.TransformedVertices[i] = v;
        });
       
        Matrix4x4 normalMatrix = worldTransform;
        normalMatrix.M14 = 0;
        normalMatrix.M24 = 0;
        normalMatrix.M34 = 0;
        normalMatrix.M44 = 1;
        
        count = this.Object.Normals.Count;
        Parallel.For(0, count, i =>
        {
            var v = Vector4.Transform(this.Object.Normals[i], normalMatrix);
            this.TransformedNormals[i] = v;
        });
    }
    
    public Vector3 GetOptimalTranslationStep()
    {
        float dx = Object.Max.X - Object.Min.X;
        float dy = Object.Max.Y - Object.Min.Y;
        float dz = Object.Max.Z - Object.Min.Z;

        float stepX = dx / 60.0f;
        float stepY = dy / 60.0f;
        float stepZ = dz / 60.0f;

        return new Vector3(stepX, stepY, stepZ);
    }
}