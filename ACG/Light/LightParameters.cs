using System.Windows.Media;

namespace Graphics.UI.Light;

public class LightParameters
{
    public float AmbientCoeff
    {
        get => _ambientCoeff;
        set
        {
            _ambientCoeff = value;
            OnTransformationChanged();
        }
    }

    public float DiffuseCoeff{
        get => _diffuseCoeff;
        set
        {
            _diffuseCoeff = value;
            OnTransformationChanged();
        }
    }
    public float SpecularCoeff{
        get => _specularCoeff;
        set
        {
            _specularCoeff = value;
            OnTransformationChanged();
        }
    }
    public float Shininess{
        get => _shininess;
        set
        {
            _shininess = value;
            OnTransformationChanged();
        }
    }
    
    public Color AmbientColor{
        get => _ambientColor;
        set
        {
            _ambientColor = value;
            OnTransformationChanged();
        }
    }
    
    public Color DiffuseColor{
        get => _diffuseColor;
        set
        {
            _diffuseColor = value;
            OnTransformationChanged();
        }
    }
    
    public Color SpecularColor{
        get => _specularColor;
        set
        {
            _specularColor = value;
            OnTransformationChanged();
        }
    }
    
    public Color BackgroundColor{
        get => _backgroundColor;
        set
        {
            _backgroundColor = value;
            OnTransformationChanged();
        }
    }
    public event EventHandler? TransformationChanged;
    protected virtual void OnTransformationChanged()
    {
        TransformationChanged?.Invoke(this, EventArgs.Empty);
    }
    
        
    private Color _ambientColor = Colors.Purple;
    private Color _diffuseColor = Colors.Purple;
    private Color _specularColor = Colors.Purple;
    private Color _backgroundColor = Colors.Purple;
 
    private float _ambientCoeff;
    private float _diffuseCoeff;
    private float _specularCoeff;
    private float _shininess;
    private float _transparency = 1.0f;
}