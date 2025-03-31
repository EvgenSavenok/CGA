using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Graphics.UI.Objects.Light;

public class CustomLight : INotifyPropertyChanged
{
    private Vector3 _sourceOfLight;
    private Color _color;
    private float _intensity;

    public Vector3 SourceOfLight
    {
        get => _sourceOfLight;
        set { _sourceOfLight = value; OnPropertyChanged(); }
    }
    
    public Color Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(); }
    }

    public float Intensity
    {
        get => _intensity;
        set { _intensity = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}