using System.Numerics;

namespace Graphics.Core;

public class Camera
{
    public Vector3 RotatedEye
    {
        get {
            var rotatedEye = new Vector3(0,0,0);
        
            rotatedEye.Y = MathF.Sin(Eye.Y) * Eye.X;
            rotatedEye.Z = MathF.Cos(Eye.Y) * MathF.Cos(Eye.Z) * Eye.X;
            rotatedEye.X = MathF.Cos(Eye.Y) * MathF.Sin(Eye.Z) * Eye.X;
            return rotatedEye;
        }
    }
    public event EventHandler? TransformationChanged;
    
    protected virtual void OnTransformationChanged()
    {
        TransformationChanged?.Invoke(this, EventArgs.Empty);
    }
    
    public Vector3 Eye
    {
        get => _eye;
    }
    public float Pitch
    {
        get => Eye.Y;
        set
        {
            if (_eye.Y != value && (value < MathF.PI / 2) && (value > -MathF.PI / 2))
            {
                _eye.Y = value;
            }
        }
    }

    public float Radius
    {
        get => Eye.X;
        set
        {
            if (_eye.X != value && (value > 0))
            {
                _eye.X = value;
                OnTransformationChanged();
            }
        }
    }
    
    public float Yaw
    {
        get => Eye.Z;
        set
        {
            if (_eye.Z != value)
            {
                _eye.Z = value;
            }
        }
    }

    public Vector3 EyeCoords
    {
        get
        {
            var rotatedEye = new Vector3(0,0,0);
        
            rotatedEye.Y = MathF.Sin(Eye.Y) * Eye.X;
            rotatedEye.Z = MathF.Cos(Eye.Y) * MathF.Cos(Eye.Z) * Eye.X;
            rotatedEye.X = MathF.Cos(Eye.Y) * MathF.Sin(Eye.Z) * Eye.X;

            return Target + rotatedEye;
        }
    }

    public Vector3 Target
    {
        get => _target;
        set
        {
            if (_target != value)
            {
                _target = value;
                OnTransformationChanged();
            }
        }
    }

    private Vector3 _target = Vector3.Zero;
    private Vector3 _eye = new(MathF.PI, 0, 0);
    public Vector3 Up { get; init; } = Vector3.UnitY;
    
    public float Fov { get; init; } = MathF.PI / 3.0f;  
    public float Aspect { get; init; } = 16f / 9f;
    public float ZNear { get; init; } = 0.1f;
    public float ZFar { get; init; } = 10000.0f;
}