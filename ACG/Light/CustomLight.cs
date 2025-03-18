using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Graphics.UI;

public class CustomLight : INotifyPropertyChanged
{
    private Vector3 _source;
    private Color _color;
    private float _intensity;

    public Vector3 Source
    {
        get => _source;
        set { _source = value; OnPropertyChanged(); }
    }

    public float X
    {
        get => _source.X;
        set { _source.X = value; OnPropertyChanged(); }
    }
    
    public float Y
    {
        get => _source.Y;
        set { _source.Y = value; OnPropertyChanged(); }
    }
    
    public float Z
    {
        get => _source.Z;
        set { _source.Z = value; OnPropertyChanged(); }
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