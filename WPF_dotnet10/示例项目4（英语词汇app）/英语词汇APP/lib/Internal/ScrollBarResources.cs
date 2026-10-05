using System.Collections;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Xml;

namespace StartUI4Controls.Internal
{
    /// <summary>
    /// 集中提供组件库内部使用的 ScrollBar / ScrollViewer 样式资源，
    /// 避免同一份 XAML 在多个组件文件中重复定义。
    /// </summary>
    internal static class ScrollBarResources
    {
        // ────────────────────────────────────────────────────────────
        //  1. 隐式 ScrollBar 样式片段（供 ResourceDictionary 包裹）
        //     用于 UI4TextBox / UI4PasswordBox / UI4ComboBox
        // ────────────────────────────────────────────────────────────

        private const string ScrollBarStylesFragment = @"
    <Style x:Key='ScrollBarThumb' TargetType='{x:Type Thumb}'>
        <Setter Property='OverridesDefaultStyle' Value='true'/>
        <Setter Property='IsTabStop' Value='false'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='{x:Type Thumb}'>
                    <Rectangle Fill='{DynamicResource UI4.Brush.ScrollBarThumb}' RadiusX='3' RadiusY='3'/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key='HorizontalScrollBarPageButton' TargetType='{x:Type RepeatButton}'>
        <Setter Property='OverridesDefaultStyle' Value='true'/>
        <Setter Property='Background' Value='Transparent'/>
        <Setter Property='Focusable' Value='false'/>
        <Setter Property='IsTabStop' Value='false'/>
        <Setter Property='Opacity' Value='0'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='{x:Type RepeatButton}'>
                    <Rectangle Fill='{TemplateBinding Background}'
                               Width='{TemplateBinding Width}'
                               Height='{TemplateBinding Height}'/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key='VerticalScrollBarPageButton' TargetType='{x:Type RepeatButton}'>
        <Setter Property='OverridesDefaultStyle' Value='true'/>
        <Setter Property='Background' Value='Transparent'/>
        <Setter Property='Focusable' Value='false'/>
        <Setter Property='IsTabStop' Value='false'/>
        <Setter Property='Opacity' Value='0'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='{x:Type RepeatButton}'>
                    <Rectangle Fill='{TemplateBinding Background}'
                               Width='{TemplateBinding Width}'
                               Height='{TemplateBinding Height}'/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style TargetType='{x:Type ScrollBar}'>
        <Setter Property='Stylus.IsPressAndHoldEnabled' Value='false'/>
        <Setter Property='Stylus.IsFlicksEnabled' Value='false'/>
        <Setter Property='Background' Value='Transparent'/>
        <Setter Property='Margin' Value='0,1,2,6'/>
        <Setter Property='Width' Value='6'/>
        <Setter Property='MinWidth' Value='6'/>
        <Setter Property='Opacity' Value='0'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='{x:Type ScrollBar}'>
                    <Grid x:Name='Bg' SnapsToDevicePixels='true'>
                        <Track x:Name='PART_Track' IsDirectionReversed='true'>
                            <Track.DecreaseRepeatButton>
                                <RepeatButton Style='{StaticResource VerticalScrollBarPageButton}'
                                              Command='{x:Static ScrollBar.PageUpCommand}'/>
                            </Track.DecreaseRepeatButton>
                            <Track.IncreaseRepeatButton>
                                <RepeatButton Style='{StaticResource VerticalScrollBarPageButton}'
                                              Command='{x:Static ScrollBar.PageDownCommand}'/>
                            </Track.IncreaseRepeatButton>
                            <Track.Thumb>
                                <Thumb Style='{StaticResource ScrollBarThumb}'/>
                            </Track.Thumb>
                        </Track>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Trigger.EnterActions>
                                <BeginStoryboard>
                                    <Storyboard>
                                        <DoubleAnimation Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:0.2'/>
                                    </Storyboard>
                                </BeginStoryboard>
                            </Trigger.EnterActions>
                            <Trigger.ExitActions>
                                <BeginStoryboard>
                                    <Storyboard>
                                        <DoubleAnimation Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:0.5'/>
                                    </Storyboard>
                                </BeginStoryboard>
                            </Trigger.ExitActions>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property='Orientation' Value='Horizontal'>
                <Setter Property='Background' Value='Transparent'/>
                <Setter Property='Margin' Value='2,0,6,2'/>
                <Setter Property='Height' Value='6'/>
                <Setter Property='MinHeight' Value='6'/>
                <Setter Property='Width' Value='Auto'/>
                <Setter Property='Opacity' Value='0'/>
                <Setter Property='Template'>
                    <Setter.Value>
                        <ControlTemplate TargetType='{x:Type ScrollBar}'>
                            <Grid x:Name='Bg' SnapsToDevicePixels='true'>
                                <Track x:Name='PART_Track'>
                                    <Track.DecreaseRepeatButton>
                                        <RepeatButton Style='{StaticResource HorizontalScrollBarPageButton}'
                                                      Command='{x:Static ScrollBar.PageLeftCommand}'/>
                                    </Track.DecreaseRepeatButton>
                                    <Track.IncreaseRepeatButton>
                                        <RepeatButton Style='{StaticResource HorizontalScrollBarPageButton}'
                                                      Command='{x:Static ScrollBar.PageRightCommand}'/>
                                    </Track.IncreaseRepeatButton>
                                    <Track.Thumb>
                                        <Thumb Style='{StaticResource ScrollBarThumb}'/>
                                    </Track.Thumb>
                                </Track>
                            </Grid>
                            <ControlTemplate.Triggers>
                                <Trigger Property='IsMouseOver' Value='True'>
                                    <Trigger.EnterActions>
                                        <BeginStoryboard>
                                            <Storyboard>
                                                <DoubleAnimation Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:0.2'/>
                                            </Storyboard>
                                        </BeginStoryboard>
                                    </Trigger.EnterActions>
                                    <Trigger.ExitActions>
                                        <BeginStoryboard>
                                            <Storyboard>
                                                <DoubleAnimation Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:0.5'/>
                                            </Storyboard>
                                        </BeginStoryboard>
                                    </Trigger.ExitActions>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Trigger>
        </Style.Triggers>
    </Style>";

        // ────────────────────────────────────────────────────────────
        //  2. 带 Key 的 ScrollBar 样式片段（供 ScrollViewer Style 内嵌）
        //     用于 UI4ListBox / UI4ScrollViewer / UI4CodeEditor
        // ────────────────────────────────────────────────────────────

        private const string KeyedScrollBarStylesFragment = @"
    <Style x:Key='ScrollBarThumb' TargetType='{x:Type Thumb}'>
        <Setter Property='OverridesDefaultStyle' Value='true'/>
        <Setter Property='IsTabStop' Value='false'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='{x:Type Thumb}'>
                    <Rectangle Fill='{DynamicResource UI4.Brush.ScrollBarThumb}' RadiusX='3' RadiusY='3'/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key='HorizontalScrollBarPageButton' TargetType='{x:Type RepeatButton}'>
        <Setter Property='OverridesDefaultStyle' Value='true'/>
        <Setter Property='Background' Value='Transparent'/>
        <Setter Property='Focusable' Value='false'/>
        <Setter Property='IsTabStop' Value='false'/>
        <Setter Property='Opacity' Value='0'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='{x:Type RepeatButton}'>
                    <Rectangle Fill='{TemplateBinding Background}'
                               Width='{TemplateBinding Width}'
                               Height='{TemplateBinding Height}'/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key='VerticalScrollBarPageButton' TargetType='{x:Type RepeatButton}'>
        <Setter Property='OverridesDefaultStyle' Value='true'/>
        <Setter Property='Background' Value='Transparent'/>
        <Setter Property='Focusable' Value='false'/>
        <Setter Property='IsTabStop' Value='false'/>
        <Setter Property='Opacity' Value='0'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='{x:Type RepeatButton}'>
                    <Rectangle Fill='{TemplateBinding Background}'
                               Width='{TemplateBinding Width}'
                               Height='{TemplateBinding Height}'/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key='for_scrollbar' TargetType='{x:Type ScrollBar}'>
        <Setter Property='Stylus.IsPressAndHoldEnabled' Value='false'/>
        <Setter Property='Stylus.IsFlicksEnabled' Value='false'/>
        <Setter Property='Background' Value='Transparent'/>
        <Setter Property='Margin' Value='0,1,2,6'/>
        <Setter Property='Width' Value='6'/>
        <Setter Property='MinWidth' Value='6'/>
        <Setter Property='Opacity' Value='0'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='{x:Type ScrollBar}'>
                    <Grid x:Name='Bg' SnapsToDevicePixels='true'>
                        <Track x:Name='PART_Track' IsEnabled='{TemplateBinding IsMouseOver}' IsDirectionReversed='true'>
                            <Track.DecreaseRepeatButton>
                                <RepeatButton Style='{StaticResource VerticalScrollBarPageButton}'
                                              Command='{x:Static ScrollBar.PageUpCommand}'/>
                            </Track.DecreaseRepeatButton>
                            <Track.IncreaseRepeatButton>
                                <RepeatButton Style='{StaticResource VerticalScrollBarPageButton}'
                                              Command='{x:Static ScrollBar.PageDownCommand}'/>
                            </Track.IncreaseRepeatButton>
                            <Track.Thumb>
                                <Thumb Style='{StaticResource ScrollBarThumb}'/>
                            </Track.Thumb>
                        </Track>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Trigger.EnterActions>
                                <BeginStoryboard>
                                    <Storyboard>
                                        <DoubleAnimation Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:0.2'/>
                                    </Storyboard>
                                </BeginStoryboard>
                            </Trigger.EnterActions>
                            <Trigger.ExitActions>
                                <BeginStoryboard>
                                    <Storyboard>
                                        <DoubleAnimation Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:0.5'/>
                                    </Storyboard>
                                </BeginStoryboard>
                            </Trigger.ExitActions>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property='Orientation' Value='Horizontal'>
                <Setter Property='Background' Value='Transparent'/>
                <Setter Property='Margin' Value='2,0,6,2'/>
                <Setter Property='Height' Value='6'/>
                <Setter Property='MinHeight' Value='6'/>
                <Setter Property='Width' Value='Auto'/>
                <Setter Property='Opacity' Value='0'/>
                <Setter Property='Template'>
                    <Setter.Value>
                        <ControlTemplate TargetType='{x:Type ScrollBar}'>
                            <Grid x:Name='Bg' SnapsToDevicePixels='true'>
                                <Track x:Name='PART_Track' IsEnabled='{TemplateBinding IsMouseOver}'>
                                    <Track.DecreaseRepeatButton>
                                        <RepeatButton Style='{StaticResource HorizontalScrollBarPageButton}'
                                                      Command='{x:Static ScrollBar.PageLeftCommand}'/>
                                    </Track.DecreaseRepeatButton>
                                    <Track.IncreaseRepeatButton>
                                        <RepeatButton Style='{StaticResource HorizontalScrollBarPageButton}'
                                                      Command='{x:Static ScrollBar.PageRightCommand}'/>
                                    </Track.IncreaseRepeatButton>
                                    <Track.Thumb>
                                        <Thumb Style='{StaticResource ScrollBarThumb}'/>
                                    </Track.Thumb>
                                </Track>
                            </Grid>
                            <ControlTemplate.Triggers>
                                <Trigger Property='IsMouseOver' Value='True'>
                                    <Trigger.EnterActions>
                                        <BeginStoryboard>
                                            <Storyboard>
                                                <DoubleAnimation Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:0.2'/>
                                            </Storyboard>
                                        </BeginStoryboard>
                                    </Trigger.EnterActions>
                                    <Trigger.ExitActions>
                                        <BeginStoryboard>
                                            <Storyboard>
                                                <DoubleAnimation Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:0.5'/>
                                            </Storyboard>
                                        </BeginStoryboard>
                                    </Trigger.ExitActions>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Trigger>
        </Style.Triggers>
    </Style>";

        // ────────────────────────────────────────────────────────────
        //  公共 API
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// 获取包含隐式 ScrollBar 样式的 ResourceDictionary XAML。
        /// 用于 UI4TextBox / UI4PasswordBox / UI4ComboBox。
        /// </summary>
        public static string GetResourceDictionaryXaml()
        {
            return @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>"
                + ScrollBarStylesFragment +
                @"
</ResourceDictionary>";
        }

        /// <summary>
        /// 解析并返回 ResourceDictionary 实例。
        /// </summary>
        public static ResourceDictionary GetResourceDictionary()
        {
            return (ResourceDictionary)XamlReader.Parse(GetResourceDictionaryXaml());
        }

        /// <summary>
        /// 将 ScrollBar 样式注入目标 ResourceDictionary（不覆盖已有同名 Key）。
        /// </summary>
        public static void MergeInto(ResourceDictionary target)
        {
            var dict = GetResourceDictionary();
            foreach (DictionaryEntry entry in dict)
            {
                if (!target.Contains(entry.Key))
                    target.Add(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// 获取包含内嵌 ScrollBar 样式的 ScrollViewer Style XAML。
        /// 用于 UI4ListBox / UI4ScrollViewer / UI4CodeEditor。
        /// </summary>
        public static string GetScrollViewerStyleXaml()
        {
            return @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='{x:Type ScrollViewer}'>
    <Style.Resources>"
                + KeyedScrollBarStylesFragment +
                @"
    </Style.Resources>
    <Setter Property='BorderBrush' Value='{DynamicResource UI4.Brush.BorderWeak}'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='HorizontalContentAlignment' Value='Left'/>
    <Setter Property='HorizontalScrollBarVisibility' Value='Auto'/>
    <Setter Property='VerticalContentAlignment' Value='Top'/>
    <Setter Property='VerticalScrollBarVisibility' Value='Auto'/>
    <Setter Property='Template'>
        <Setter.Value>
            <ControlTemplate TargetType='{x:Type ScrollViewer}'>
                <Border BorderBrush='{TemplateBinding BorderBrush}'
                        BorderThickness='{TemplateBinding BorderThickness}'
                        SnapsToDevicePixels='True'>
                    <Grid Background='{TemplateBinding Background}'>
                        <ScrollContentPresenter
                            Cursor='{TemplateBinding Cursor}'
                            Margin='{TemplateBinding Padding}'
                            ContentTemplate='{TemplateBinding ContentTemplate}'/>
                        <ScrollBar x:Name='PART_VerticalScrollBar'
                                   HorizontalAlignment='Right'
                                   Maximum='{TemplateBinding ScrollableHeight}'
                                   Orientation='Vertical'
                                   Style='{StaticResource for_scrollbar}'
                                   ViewportSize='{TemplateBinding ViewportHeight}'
                                   Value='{TemplateBinding VerticalOffset}'
                                   Visibility='{TemplateBinding ComputedVerticalScrollBarVisibility}'/>
                        <ScrollBar x:Name='PART_HorizontalScrollBar'
                                   Maximum='{TemplateBinding ScrollableWidth}'
                                   Orientation='Horizontal'
                                   Style='{StaticResource for_scrollbar}'
                                   VerticalAlignment='Bottom'
                                   Value='{TemplateBinding HorizontalOffset}'
                                   ViewportSize='{TemplateBinding ViewportWidth}'
                                   Visibility='{TemplateBinding ComputedHorizontalScrollBarVisibility}'/>
                    </Grid>
                </Border>
                <ControlTemplate.Triggers>
                    <EventTrigger RoutedEvent='ScrollChanged'>
                        <BeginStoryboard>
                            <Storyboard>
                                <DoubleAnimation Storyboard.TargetName='PART_VerticalScrollBar' Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:0.2'/>
                                <DoubleAnimation Storyboard.TargetName='PART_VerticalScrollBar' Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:0.5' BeginTime='0:0:1.5'/>
                                <DoubleAnimation Storyboard.TargetName='PART_HorizontalScrollBar' Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:0.2'/>
                                <DoubleAnimation Storyboard.TargetName='PART_HorizontalScrollBar' Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:0.5' BeginTime='0:0:1.5'/>
                            </Storyboard>
                        </BeginStoryboard>
                    </EventTrigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>";
        }

        /// <summary>
        /// 解析并返回 ScrollViewer Style 实例。
        /// </summary>
        public static Style GetScrollViewerStyle()
        {
            string xaml = GetScrollViewerStyleXaml();
            try
            {
                using (var sr = new StringReader(xaml))
                using (var xr = XmlReader.Create(sr))
                {
                    return (Style)XamlReader.Load(xr);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取带 Key 的独立 ScrollBar Style XAML（用于 DataGrid 等需要显式引用的场景）。
        /// </summary>
        public static string GetKeyedScrollBarStyleXaml()
        {
            return @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       x:Key='UI4ScrollBarStyle'
       TargetType='{x:Type ScrollBar}'>
    <Style.Resources>"
                + KeyedScrollBarStylesFragment +
                @"
    </Style.Resources>"
                + @"
    <Setter Property='Stylus.IsPressAndHoldEnabled' Value='false'/>
    <Setter Property='Stylus.IsFlicksEnabled' Value='false'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Margin' Value='0,1,2,6'/>
    <Setter Property='Width' Value='6'/>
    <Setter Property='MinWidth' Value='6'/>
    <Setter Property='Opacity' Value='0'/>
    <Setter Property='Template'>
        <Setter.Value>
            <ControlTemplate TargetType='{x:Type ScrollBar}'>
                <Grid x:Name='Bg' SnapsToDevicePixels='true'>
                    <Track x:Name='PART_Track' IsEnabled='{TemplateBinding IsMouseOver}' IsDirectionReversed='true'>
                        <Track.DecreaseRepeatButton>
                            <RepeatButton Style='{StaticResource VerticalScrollBarPageButton}'
                                          Command='{x:Static ScrollBar.PageUpCommand}'/>
                        </Track.DecreaseRepeatButton>
                        <Track.IncreaseRepeatButton>
                            <RepeatButton Style='{StaticResource VerticalScrollBarPageButton}'
                                          Command='{x:Static ScrollBar.PageDownCommand}'/>
                        </Track.IncreaseRepeatButton>
                        <Track.Thumb>
                            <Thumb Style='{StaticResource ScrollBarThumb}'/>
                        </Track.Thumb>
                    </Track>
                </Grid>
                <ControlTemplate.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'>
                        <Trigger.EnterActions>
                            <BeginStoryboard>
                                <Storyboard>
                                    <DoubleAnimation Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:0.2'/>
                                </Storyboard>
                            </BeginStoryboard>
                        </Trigger.EnterActions>
                        <Trigger.ExitActions>
                            <BeginStoryboard>
                                <Storyboard>
                                    <DoubleAnimation Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:0.5'/>
                                </Storyboard>
                            </BeginStoryboard>
                        </Trigger.ExitActions>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
    <Style.Triggers>
        <Trigger Property='Orientation' Value='Horizontal'>
            <Setter Property='Background' Value='Transparent'/>
            <Setter Property='Margin' Value='2,0,6,2'/>
            <Setter Property='Height' Value='6'/>
            <Setter Property='MinHeight' Value='6'/>
            <Setter Property='Width' Value='Auto'/>
            <Setter Property='Opacity' Value='0'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type ScrollBar}'>
                        <Grid x:Name='Bg' SnapsToDevicePixels='true'>
                            <Track x:Name='PART_Track' IsEnabled='{TemplateBinding IsMouseOver}'>
                                <Track.DecreaseRepeatButton>
                                    <RepeatButton Style='{StaticResource HorizontalScrollBarPageButton}'
                                                  Command='{x:Static ScrollBar.PageLeftCommand}'/>
                                </Track.DecreaseRepeatButton>
                                <Track.IncreaseRepeatButton>
                                    <RepeatButton Style='{StaticResource HorizontalScrollBarPageButton}'
                                                  Command='{x:Static ScrollBar.PageRightCommand}'/>
                                </Track.IncreaseRepeatButton>
                                <Track.Thumb>
                                    <Thumb Style='{StaticResource ScrollBarThumb}'/>
                                </Track.Thumb>
                            </Track>
                        </Grid>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsMouseOver' Value='True'>
                                <Trigger.EnterActions>
                                    <BeginStoryboard>
                                        <Storyboard>
                                            <DoubleAnimation Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:0.2'/>
                                        </Storyboard>
                                    </BeginStoryboard>
                                </Trigger.EnterActions>
                                <Trigger.ExitActions>
                                    <BeginStoryboard>
                                        <Storyboard>
                                            <DoubleAnimation Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:0.5'/>
                                        </Storyboard>
                                    </BeginStoryboard>
                                </Trigger.ExitActions>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Trigger>
    </Style.Triggers>
</Style>";
        }

        /// <summary>
        /// 解析并返回带 Key 的 ScrollBar Style 实例。
        /// </summary>
        public static Style GetKeyedScrollBarStyle()
        {
            string xaml = GetKeyedScrollBarStyleXaml();
            try
            {
                using (var sr = new StringReader(xaml))
                using (var xr = XmlReader.Create(sr))
                {
                    return (Style)XamlReader.Load(xr);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
