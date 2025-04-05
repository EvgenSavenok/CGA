using System.Collections.ObjectModel;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Graphics.Core;
using Graphics.Core.Objects;
using Graphics.Core.Parsers;
using Graphics.Core.Transformations;
using Graphics.UI.ObjectRenderer;
using Graphics.UI.Objects.Light;
using Graphics.UI.Objects.Textures;
using Microsoft.Win32;
using Camera = Graphics.Core.Camera;
using Vector = System.Windows.Vector;

namespace Graphics.UI;

public partial class MainWindow : Window
{
    private RenderMode _currentRenderMode = RenderMode.Texture;
    public RenderMode CurrentRenderMode
    {
        get => _currentRenderMode;
        set
        {
            _currentRenderMode = value;
            Scene_Changed(this,new EventArgs());
        }
    }
    
    public  System.Drawing.Size WindowSize { get; set; }
    private Camera MyCamera { get; set; } = new Camera();
    private ObjectModel? ObjModel { get; set; } = new ObjectModel();
    private WriteableBitmap? Wb { get; set; } 
    
    private Dictionary<string, Texture> _texturesMap = new Dictionary<string, Texture>();
    public Color ForegroundSelectedColor
    {
        get => LightParams.AmbientColor;
        set
        {
            LightParams.AmbientColor = value;
        }
    }
    public Color BackgroundSelectedColor
    {
        get => LightParams.BackgroundColor;
        set
        {
            LightParams.BackgroundColor = value;
        }
    }
    private float RotateSensitivity { get; init; } = MathF.PI / 360.0f;
    
    public LightParameters LightParams { get; set; } = new LightParameters()
    {
        AmbientCoeff = 0.2f,
        DiffuseCoeff = 0.7f,
        SpecularCoeff = 0.4f,
        Shininess = 32f,
        
        AmbientColor = Colors.Purple,
        DiffuseColor = Colors.White,
        SpecularColor = Colors.White,
        BackgroundColor = Colors.White
    };

    
    public bool UseMaterialFile
    {
        get => _useMaterialFile;
        set
        {
            _useMaterialFile = value;
            Scene_Changed(this,new EventArgs());
        }
    }
    
    private ObservableCollection<CustomLight> _lights = new()
    {
        new CustomLight()
        {
            SourceOfLight = new(0, 0, 3 ),
            Intensity = 1f,
            Color = Colors.White
        }
    };
    
    public string SelectedMaterialFile
    {
        get => _selectedMaterialFile;
        set
        {
            _selectedMaterialFile = value;
            try
            {
                var materialFile = TextureParser.Parse(value);
                _materials.Clear();

                foreach (var material in materialFile.Materials)
                {
                    _materials.Add(material.Value);
                }
            }
            catch (FileNotFoundException e)
            {
                MessageBox.Show($"{e.FileName} not found!!!");
            }
            Scene_Changed(this,new EventArgs());
        }
    }

    private ObservableCollection<CustomMaterial> _materials = new ObservableCollection<CustomMaterial>();
    private bool _isRotating;
    private Point _lastMousePos;
    private bool _useMaterialFile;
    private string _selectedMaterialFile = string.Empty;
    private bool _altPressed = false;
    
    public MainWindow()
    {
        InitializeComponent();
        RenderModeComboBox.ItemsSource = Enum.GetValues(typeof(RenderMode)).Cast<RenderMode>();
        DataContext = this;
    }
    
    
    private void BtnLoad_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog();
        dlg.Filter = "OBJ Files | *.obj";
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var obj = ObjParser.Parse(dlg.FileName!);
                ObjModel = new ObjectModel();
                MyCamera = new Camera();
                _altPressed = false;
                ObjModel.Object = obj;
                int width = (int)(ImagePanel.ActualWidth > 0 ? ImagePanel.ActualWidth : 800);
                int height = (int)(ImagePanel.ActualHeight > 0 ? ImagePanel.ActualHeight : 600);
                WindowSize = new(width, height);
                
                Wb = new WriteableBitmap(WindowSize.Width, WindowSize.Height, 96, 96, PixelFormats.Bgra32, null);
                ImgDisplay.Source = Wb;
                
                MyCamera.TransformationChanged += Scene_Changed;
                ObjModel.TransformationChanged += Scene_Changed;
                LightParams.TransformationChanged += Scene_Changed;
                _lights.CollectionChanged += Lights_CollectionChanged;
                _materials.CollectionChanged += Materials_CollectionChanged;
                foreach (CustomLight light in _lights)
                {
                    light.PropertyChanged += Scene_Changed;
                }
    
                ObjModel.Object.MtlFile = Path.GetDirectoryName(dlg.FileName) + Path.DirectorySeparatorChar + ObjModel.Object.MtlFile;
                SelectedMaterialFile = ObjModel.Object.MtlFile;
                // Для того, чтобы все перерисовалось
                // Вызовется RedrawModel()
                ObjModel.Scale = ObjModel.Delta * 10f; 
                
                // Опускаем модель пониже
                MyCamera.Target -= new Vector3(0, -1.0f, 0); 
                RedrawModel();
                
            }
            catch (Exception ex)
            {
                MessageBox.Show("Loading error: " + ex.Message);
            }
        }
    }

    private void BtnClear_OnClick(object sender, RoutedEventArgs e)
    {
        if (Wb != null)
        {
            WireframeRenderer.ClearBitmap(Wb, BackgroundSelectedColor);
            ObjModel = null;
        }
    }

    private void ImagePanel_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ObjModel != null)
        {
            if (_altPressed)
            {
                if (e.Delta > 0)
                {
                    MyCamera.Radius += 0.1f;
                }
                else
                {
                    MyCamera.Radius -= 0.1f;
                }
            } 
            else if (e.Delta > 0)
            {
                ObjModel.Scale += ObjModel.Delta;
            }
            else
            {
                ObjModel.Scale -= ObjModel.Delta;
            }
        }
    }

    private void ImagePanel_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ImagePanel.Focus();
        _isRotating = true;
        _lastMousePos = e.GetPosition(ImagePanel);
        ImagePanel.CaptureMouse();
    }

    private void ImagePanel_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isRotating = false;
        ImagePanel.ReleaseMouseCapture();
    }
    
   private void ImagePanel_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (ObjModel is null) return;
        
        var optimalStep = ObjModel.GetOptimalTranslationStep();
        
        switch (e.Key)
        {
            case Key.A:
                if (_altPressed)
                {
                    MyCamera.Target += new Vector3(0.2f, 0, 0);
                }
                else
                {
                    ObjModel.Translation += new Vector3(-optimalStep.X, 0, 0);
                }
                break;
            case Key.D:
                if (_altPressed)
                {
                    MyCamera.Target += new Vector3(-0.2f, 0, 0);
                }
                else
                {
                    ObjModel.Translation += new Vector3(optimalStep.X, 0, 0);
                }
                break;
            case Key.W:
                if (_altPressed)
                {
                    MyCamera.Target += new Vector3(0, -0.2f, 0);
                }
                else
                {
                    ObjModel.Translation += new Vector3(0, optimalStep.Y, 0);
                }
                break;
            case Key.S:
                if (_altPressed)
                {
                    MyCamera.Target += new Vector3(0, 0.2f, 0);
                }
                else
                {
                    ObjModel.Translation += new Vector3(0, -optimalStep.Y, 0);
                }
                break;
            case Key.System:
                _altPressed = !_altPressed;
                break;
        }
    }

    private void ImagePanel_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isRotating && ObjModel != null)
        {
            Point currentPos = e.GetPosition(ImagePanel);
            Vector delta = currentPos - _lastMousePos;

            if (_altPressed)
            {
                MyCamera.Pitch += (float)delta.Y * RotateSensitivity;
                RedrawModel();
            }
            else
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                ObjModel.Rotation = new Vector3(
                    ObjModel.Rotation.X,
                    ObjModel.Rotation.Y,
                    ObjModel.Rotation.Z - (float)delta.X * RotateSensitivity);
            }
            else
            {
                ObjModel.Rotation = new Vector3(
                    ObjModel.Rotation.X,
                    ObjModel.Rotation.Y + (float)delta.X * RotateSensitivity,
                    ObjModel.Rotation.Z);
            }
            _lastMousePos = currentPos;
        }
    }
    
    private void Lights_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (CustomLight light in e.NewItems)
            {
                light.PropertyChanged += Scene_Changed;
            }
        }

        if (e.OldItems != null)
        {
            foreach (CustomLight light in e.OldItems)
            {
                light.PropertyChanged -= Scene_Changed;
            }
        }
        RedrawModel();
    }
    
    private void Materials_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (CustomMaterial material in e.NewItems)
            {
                material.PropertyChanged += (o, args) =>
                {
                    try
                    {
                        if (!_texturesMap.ContainsKey(material.DiffuseMap) && !string.IsNullOrEmpty(material.DiffuseMap))
                        {
                            _texturesMap.TryAdd(material.DiffuseMap, new Texture(material.DiffuseMap));
                        }
                        
                        if (!_texturesMap.ContainsKey(material.NormalMap) && !string.IsNullOrEmpty(material.NormalMap))
                        {
                            _texturesMap.TryAdd(material.NormalMap, new Texture(material.NormalMap));
                        }
                        
                        if (!_texturesMap.ContainsKey(material.SpecularMap) && !string.IsNullOrEmpty(material.SpecularMap))
                        {
                            _texturesMap.TryAdd(material.SpecularMap, new Texture(material.SpecularMap));
                        }
                    }
                    catch (FileNotFoundException e){}
                };
                    
                material.PropertyChanged += Scene_Changed;

                try
                {
                    if (!string.IsNullOrEmpty(material.DiffuseMap))
                        _texturesMap.TryAdd(material.DiffuseMap, new Texture(material.DiffuseMap));
                    
                    if (!string.IsNullOrEmpty(material.NormalMap))
                        _texturesMap.TryAdd(material.NormalMap, new Texture(material.NormalMap));
                    
                    if (!string.IsNullOrEmpty(material.SpecularMap))
                        _texturesMap.TryAdd(material.SpecularMap, new Texture(material.SpecularMap));
                }
                catch (FileNotFoundException ex){}
            }
        }

        if (e.OldItems != null)
        {
            foreach (CustomMaterial material in e.OldItems)
            {
                material.PropertyChanged -= Scene_Changed;
            }
        }
        RedrawModel();
    }
    
    private void Scene_Changed(object? sender, EventArgs e)
    {
        RedrawModel();
    }

    private void RedrawModel()
    {
        if (Wb == null || ObjModel == null) 
            return;
        
        var viewTransform = Transformation.CreateViewMatrix(MyCamera.EyeCoords, MyCamera.Target, MyCamera.Up);
        
        var projectionTransform = Transformation.CreatePerspectiveProjection(MyCamera.Fov, MyCamera.Aspect, MyCamera.ZNear, MyCamera.ZFar);

        var viewportTransform = Transformation.CreateViewportMatrix(WindowSize.Width, WindowSize.Height);
        
        var finalTransform = viewTransform * projectionTransform * viewportTransform;
        var res =Transformation.ApplyTransformations(ObjModel,MyCamera,finalTransform);
        
        PhongShadingRenderer.ClearBitmap(Wb, BackgroundSelectedColor);
        switch (_currentRenderMode)
        {
            case RenderMode.Shadow:
                PhongShadingRenderer.DrawObject(res, ObjModel, MyCamera, Wb, _lights.ToList(), LightParams);
                break;
            case RenderMode.Wireframe:
                WireframeRenderer.DrawObject(res,ObjModel,MyCamera, Wb, ForegroundSelectedColor);
                break;
            case RenderMode.Rasterized:
                RasterizedRenderer.DrawObject(res, ObjModel, MyCamera, Wb, ForegroundSelectedColor, _lights.ToList());
                break;
            case RenderMode.Texture:
                TextureRenderer.DrawObject(res,ObjModel,MyCamera,Wb,_lights.ToList(),_materials.ToList(), LightParams, _texturesMap);
                break;
        }
    }
}