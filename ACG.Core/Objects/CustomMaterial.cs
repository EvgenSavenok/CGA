using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Graphics.Core.Objects;

public class CustomMaterial : INotifyPropertyChanged
{
    private string _name;
    private string _diffuseMap = string.Empty;
    private string _normalMap = string.Empty;
    private string _specularMap = string.Empty;
    
    public string Name { 
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }
    
    public Vector3 AmbientColor { get; set; }
    public Vector3 DiffuseColor { get; set; }
    public Vector3 SpecularColor { get; set; }
    
    // Для текстур
    public float Transparency { get; set; }
    public float OpticalDensity { get; set; }
    public int IlluminationModel { get; set; }

    public float BumpScale = 1;
    
    // Насколько сильно будет отражаться зеркальная поверхность
    public float Shininess { get; set; }
    
    public string DiffuseMap
    {
        get => _diffuseMap;
        set
        {
            _diffuseMap = value;
            OnPropertyChanged();
        }
    }

    public string NormalMap
    {
        get => _normalMap;
        set
        {
            _normalMap = value;
            OnPropertyChanged();
        }
    }

    public string BumpMap { get; set; } = string.Empty;
    public string MraoMap { get; set; } = string.Empty;
    public string AoMap { get; set; } = string.Empty;
    public string MetallicMap { get; set; } = string.Empty;
    
    public string RoughnessMap { get; set; } = string.Empty;
    public string EmissiveMap { get; set; } = string.Empty;
    public string SpecularMap {
        get => _specularMap;
        set
        {
            _specularMap = value;
            OnPropertyChanged();
        }
    }

    
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}