using System.Windows;

namespace ConSecOrg.Client.Themes;

/// <summary>
/// Стандартный WPF-паттерн для доступа к внешнему DataContext из шаблона
/// (в т.ч. из рекурсивных шаблонов, где RelativeSource не подходит).
///
/// Использование:
/// <code>
///   &lt;StackPanel.Resources&gt;
///     &lt;t:BindingProxy x:Key="cardProxy" Data="{Binding}"/&gt;
///   &lt;/StackPanel.Resources&gt;
///   ...
///   Command="{Binding Source={StaticResource cardProxy}, Path=Data.SomeCommand}"
/// </code>
/// </summary>
public class BindingProxy : Freezable
{
    protected override Freezable CreateInstanceCore() => new BindingProxy();

    public object Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));
}
