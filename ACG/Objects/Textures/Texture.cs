using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Graphics.UI.Objects.Textures;

public class Texture
{
    private readonly byte[] _pixels;
    private readonly int _width;
    private readonly int _height;
    private readonly int _stride;

    public Texture(BitmapSource source)
    {
        _width = source.PixelWidth;
        _height = source.PixelHeight;
        _stride = _width * 4;
        _pixels = new byte[_height * _stride];
        source.CopyPixels(_pixels, _stride, 0);
    }

    public Color GetColor(float u, float v)
    {
        // u и v - нормализованные координаты
        // x и y - пиксельные координаты
        int x = (int)(u * (_width - 1));
        int y = (int)(v * (_height - 1));
        
        // Для зацикливания текстуры (если x = 102, а _width = 100,
        // то x % 100 = 2, т.е. пиксель возьмется из начала текстуры
        x %= _width;
        y %= _height;
        // Смещение пикселя в массиве _pixels, который хранит все пиксели текстуры
        int offset = y * _stride + x * 4;
        
        return Color.FromArgb(
            _pixels[offset + 3],  // Alpha
            _pixels[offset + 2],  // Red
            _pixels[offset + 1],  // Green
            _pixels[offset]       // Blue
        );
    }
    
    public Texture(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        
        _width = image.PixelWidth;
        _height = image.PixelHeight;
        _stride = _width * 4;
        _pixels = new byte[_height * _stride];
        image.CopyPixels(_pixels, _stride, 0);
    }
}