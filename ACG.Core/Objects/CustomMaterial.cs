using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Graphics.Core.Objects;

public class CustomMaterial : INotifyPropertyChanged
{
    private string _name;
    
    public string Name { 
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }
    
    public Vector3 AmbientColor { get; }
    public Vector3 DiffuseColor { get; }
    public Vector3 SpecularColor { get; }
    
    // Насколько сильно будет отражаться зеркальная поверхность
    public float Shininess { get; }
    
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}