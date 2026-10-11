using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace MacOSDock;

internal sealed class LiquidGlassEffect : ShaderEffect
{
    private static readonly PixelShader GlassShader = new()
    {
        UriSource = new Uri("pack://application:,,,/MacOSDock;component/Shaders/LiquidGlass.ps", UriKind.Absolute)
    };

    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty(nameof(Input), typeof(LiquidGlassEffect), 0);

    public static readonly DependencyProperty PixelSizeProperty = DependencyProperty.Register(
        nameof(PixelSize), typeof(Point), typeof(LiquidGlassEffect),
        new UIPropertyMetadata(new Point(0.01, 0.01), PixelShaderConstantCallback(0)));

    public Brush Input
    {
        get => (Brush)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    public Point PixelSize
    {
        get => (Point)GetValue(PixelSizeProperty);
        set => SetValue(PixelSizeProperty, value);
    }

    public LiquidGlassEffect()
    {
        PixelShader = GlassShader;
        UpdateShaderValue(InputProperty);
        UpdateShaderValue(PixelSizeProperty);
    }
}
