using System.Windows;

namespace BigWalkVRInstaller
{
    // button template corner radius, split buttons square their joined edge
    public static class Corners
    {
        public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached(
            "Radius", typeof(CornerRadius), typeof(Corners), new FrameworkPropertyMetadata(new CornerRadius(6)));

        public static CornerRadius GetRadius(DependencyObject element) => (CornerRadius)element.GetValue(RadiusProperty);
        public static void SetRadius(DependencyObject element, CornerRadius value) => element.SetValue(RadiusProperty, value);
    }
}
