
Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Data
Imports System.Windows.Markup
Imports System.Windows.Media

<ContentProperty("Items")>
Public Class ThemedComboBox

    Private _syncingSelection As Boolean = False

    Shared Sub New()
        Control.PaddingProperty.OverrideMetadata(GetType(ThemedComboBox),
            New FrameworkPropertyMetadata(New Thickness(1, 2, 1, 2)))
    End Sub


    Public Sub New()
        InitializeComponent()

        ' Store a typed reference to self in InnerCombo.Tag so the ControlTemplate
        ' can reach custom DPs via {Binding Tag.X, RelativeSource AncestorType=ComboBox}
        ' without using x:Reference (which the XAML designer can't resolve across
        ' ControlTemplate name scope boundaries).

        InnerCombo.Tag = Me

        ' Bind InnerCombo's standard properties to the UserControl equivalents
        ' so {Binding Background/Foreground/... RelativeSource AncestorType=ComboBox}
        ' inside the template reflects whatever the caller sets on the UserControl.

        InnerCombo.SetBinding(ComboBox.BackgroundProperty,
            New Binding(NameOf(Background)) With {.Source = Me})
        InnerCombo.SetBinding(ComboBox.ForegroundProperty,
            New Binding(NameOf(Foreground)) With {.Source = Me})
        InnerCombo.SetBinding(ComboBox.BorderBrushProperty,
            New Binding(NameOf(BorderBrush)) With {.Source = Me})
        InnerCombo.SetBinding(ComboBox.BorderThicknessProperty,
            New Binding(NameOf(BorderThickness)) With {.Source = Me})
        InnerCombo.SetBinding(ComboBox.PaddingProperty,
            New Binding(NameOf(Padding)) With {.Source = Me})

        BuildItemContainerStyle()
        ApplyModeDefaults()

        ' Sync outer DPs when the user picks an item, so two-way bindings on
        ' SelectedItem / SelectedIndex / SelectedValue propagate back to the source.
        ' _syncingSelection prevents the DP callbacks from re-setting InnerCombo.SelectedItem,
        ' which would cause a second SelectionChanged and corrupt SelectionBoxItem display.

        AddHandler InnerCombo.SelectionChanged, Sub(s, e)
                                                    If _syncingSelection Then Return
                                                    _syncingSelection = True
                                                    SetValue(SelectedItemProperty, InnerCombo.SelectedItem)
                                                    SetValue(SelectedIndexProperty, InnerCombo.SelectedIndex)
                                                    SetValue(SelectedValueProperty, InnerCombo.SelectedValue)
                                                    _syncingSelection = False
                                                End Sub
    End Sub


    Private Sub Me_IsEnabledChanged() Handles Me.IsEnabledChanged

        Opacity = If(IsEnabled, 1, 0.6)

    End Sub


    ' ── ItemContainerStyle (built in code to avoid cross-name scope XAML bindings) ──

    Private Sub BuildItemContainerStyle()

        Dim style As New Style(GetType(ComboBoxItem))

        ' Default property values
        style.Setters.Add(New Setter(Control.ForegroundProperty,
            New Binding(NameOf(Foreground)) With {.Source = Me}))
        style.Setters.Add(New Setter(Control.BackgroundProperty, Brushes.Transparent))
        style.Setters.Add(New Setter(Control.BorderBrushProperty, Brushes.Transparent))
        style.Setters.Add(New Setter(Control.BorderThicknessProperty, New Thickness(1)))
        style.Setters.Add(New Setter(Control.PaddingProperty, New Thickness(5, 4, 5, 4)))
        style.Setters.Add(New Setter(Control.TemplateProperty,
            CType(Resources("ThemedItemTemplate"), ControlTemplate)))

        ' Mouse-over trigger
        Dim hoverTrigger As New Trigger() With {.Property = UIElement.IsMouseOverProperty, .Value = True}
        hoverTrigger.Setters.Add(New Setter(Control.BackgroundProperty,
            New Binding(NameOf(ItemMouseOverBackground)) With {.Source = Me}))
        hoverTrigger.Setters.Add(New Setter(Control.BorderBrushProperty,
            New Binding(NameOf(ItemMouseOverBorderBrush)) With {.Source = Me}))
        style.Triggers.Add(hoverTrigger)

        ' Selected trigger
        Dim selectedTrigger As New Trigger() With {.Property = Selector.IsSelectedProperty, .Value = True}
        selectedTrigger.Setters.Add(New Setter(Control.BackgroundProperty,
            New Binding(NameOf(ItemSelectedBackground)) With {.Source = Me}))
        selectedTrigger.Setters.Add(New Setter(Control.BorderBrushProperty,
            New Binding(NameOf(ItemSelectedBorderBrush)) With {.Source = Me}))
        style.Triggers.Add(selectedTrigger)

        InnerCombo.ItemContainerStyle = style

    End Sub


    ' ── IsDarkMode ────────────────────────────────────────────────────────────
    '    Background, Foreground, BorderBrush, ItemMouseOverBackground,
    '    ItemMouseOverBorderBrush, ItemSelectedBackground and ItemSelectedBorderBrush
    '    are "effective" properties: they are recomputed automatically from the
    '    matching *Light / *Dark pair below whenever IsDarkMode or either variant
    '    changes. Customize appearance via the *Light / *Dark properties, not by
    '    setting these directly — direct sets are overwritten on the next recompute.

    Public Property IsDarkMode As Boolean
        Get
            Return CBool(GetValue(IsDarkModeProperty))
        End Get
        Set(value As Boolean)
            SetValue(IsDarkModeProperty, value)
        End Set
    End Property

    Public Shared ReadOnly IsDarkModeProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(IsDarkMode), GetType(Boolean), GetType(ThemedComboBox),
            New PropertyMetadata(False, AddressOf OnIsDarkModeChanged))

    Private Shared Sub OnIsDarkModeChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim ctrl = CType(d, ThemedComboBox)
        ctrl.ApplyModeDefaults()
        ctrl.UpdateActiveIcon()
    End Sub

    ' Shared changed-callback for every *Light / *Dark variant property: any one
    ' of them changing means the effective brushes must be recomputed.
    Private Shared Sub OnVariantBrushChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        CType(d, ThemedComboBox).ApplyModeDefaults()
    End Sub

    Private Sub ApplyModeDefaults()

        Background = If(IsDarkMode, BackgroundDark, BackgroundLight)
        Foreground = If(IsDarkMode, ForegroundDark, ForegroundLight)
        BorderBrush = If(IsDarkMode, BorderBrushDark, BorderBrushLight)
        ItemMouseOverBackground = If(IsDarkMode, ItemMouseOverBackgroundDark, ItemMouseOverBackgroundLight)
        ItemMouseOverBorderBrush = If(IsDarkMode, ItemMouseOverBorderBrushDark, ItemMouseOverBorderBrushLight)
        ItemSelectedBackground = If(IsDarkMode, ItemSelectedBackgroundDark, ItemSelectedBackgroundLight)
        ItemSelectedBorderBrush = If(IsDarkMode, ItemSelectedBorderBrushDark, ItemSelectedBorderBrushLight)

    End Sub


    ' ── CornerRadius ──────────────────────────────────────────────────────────

    Public Property CornerRadius As CornerRadius
        Get
            Return CType(GetValue(CornerRadiusProperty), CornerRadius)
        End Get
        Set(value As CornerRadius)
            SetValue(CornerRadiusProperty, value)
        End Set
    End Property

    Public Shared ReadOnly CornerRadiusProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(CornerRadius), GetType(CornerRadius), GetType(ThemedComboBox),
            New PropertyMetadata(New CornerRadius(5)))


    ' ── Background (effective) ────────────────────────────────────────────────

    Public Property BackgroundLight As Brush
        Get
            Return CType(GetValue(BackgroundLightProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(BackgroundLightProperty, value)
        End Set
    End Property

    Public Shared ReadOnly BackgroundLightProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(BackgroundLight), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(Brushes.White, AddressOf OnVariantBrushChanged))

    Public Property BackgroundDark As Brush
        Get
            Return CType(GetValue(BackgroundDarkProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(BackgroundDarkProperty, value)
        End Set
    End Property

    Public Shared ReadOnly BackgroundDarkProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(BackgroundDark), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&H2A, &H2A, &H2A)), AddressOf OnVariantBrushChanged))


    ' ── Foreground (effective) ────────────────────────────────────────────────

    Public Property ForegroundLight As Brush
        Get
            Return CType(GetValue(ForegroundLightProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(ForegroundLightProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ForegroundLightProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ForegroundLight), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&H1A, &H1A, &H2A)), AddressOf OnVariantBrushChanged))

    Public Property ForegroundDark As Brush
        Get
            Return CType(GetValue(ForegroundDarkProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(ForegroundDarkProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ForegroundDarkProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ForegroundDark), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(Brushes.WhiteSmoke, AddressOf OnVariantBrushChanged))


    ' ── BorderBrush (effective) ───────────────────────────────────────────────

    Public Property BorderBrushLight As Brush
        Get
            Return CType(GetValue(BorderBrushLightProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(BorderBrushLightProperty, value)
        End Set
    End Property

    Public Shared ReadOnly BorderBrushLightProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(BorderBrushLight), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&H97, &H97, &H97)), AddressOf OnVariantBrushChanged))

    Public Property BorderBrushDark As Brush
        Get
            Return CType(GetValue(BorderBrushDarkProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(BorderBrushDarkProperty, value)
        End Set
    End Property

    Public Shared ReadOnly BorderBrushDarkProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(BorderBrushDark), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&HA6, &HB4, &HF8)), AddressOf OnVariantBrushChanged))


    ' ── ItemMouseOverBackground (effective) ───────────────────────────────────

    Public Property ItemMouseOverBackground As Brush
        Get
            Return CType(GetValue(ItemMouseOverBackgroundProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(ItemMouseOverBackgroundProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ItemMouseOverBackgroundProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ItemMouseOverBackground), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&HE4, &HF6, &HFF))))

    Public Property ItemMouseOverBackgroundLight As Brush
        Get
            Return CType(GetValue(ItemMouseOverBackgroundLightProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(ItemMouseOverBackgroundLightProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ItemMouseOverBackgroundLightProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ItemMouseOverBackgroundLight), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&HE4, &HF6, &HFF)), AddressOf OnVariantBrushChanged))

    Public Property ItemMouseOverBackgroundDark As Brush
        Get
            Return CType(GetValue(ItemMouseOverBackgroundDarkProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(ItemMouseOverBackgroundDarkProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ItemMouseOverBackgroundDarkProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ItemMouseOverBackgroundDark), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&H1E, &H20, &H50)), AddressOf OnVariantBrushChanged))


    ' ── ItemMouseOverBorderBrush (effective) ──────────────────────────────────

    Public Property ItemMouseOverBorderBrush As Brush
        Get
            Return CType(GetValue(ItemMouseOverBorderBrushProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(ItemMouseOverBorderBrushProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ItemMouseOverBorderBrushProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ItemMouseOverBorderBrush), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&H9F, &HDE, &HFF))))

    Public Property ItemMouseOverBorderBrushLight As Brush
        Get
            Return CType(GetValue(ItemMouseOverBorderBrushLightProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(ItemMouseOverBorderBrushLightProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ItemMouseOverBorderBrushLightProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ItemMouseOverBorderBrushLight), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&H9F, &HDE, &HFF)), AddressOf OnVariantBrushChanged))

    Public Property ItemMouseOverBorderBrushDark As Brush
        Get
            Return CType(GetValue(ItemMouseOverBorderBrushDarkProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(ItemMouseOverBorderBrushDarkProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ItemMouseOverBorderBrushDarkProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ItemMouseOverBorderBrushDark), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&H6A, &H80, &HD8)), AddressOf OnVariantBrushChanged))


    ' ── ItemSelectedBackground (effective) ────────────────────────────────────

    Public Property ItemSelectedBackground As Brush
        Get
            Return CType(GetValue(ItemSelectedBackgroundProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(ItemSelectedBackgroundProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ItemSelectedBackgroundProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ItemSelectedBackground), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&HB7, &HE6, &HFF))))

    Public Property ItemSelectedBackgroundLight As Brush
        Get
            Return CType(GetValue(ItemSelectedBackgroundLightProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(ItemSelectedBackgroundLightProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ItemSelectedBackgroundLightProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ItemSelectedBackgroundLight), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&HB7, &HE6, &HFF)), AddressOf OnVariantBrushChanged))

    Public Property ItemSelectedBackgroundDark As Brush
        Get
            Return CType(GetValue(ItemSelectedBackgroundDarkProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(ItemSelectedBackgroundDarkProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ItemSelectedBackgroundDarkProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ItemSelectedBackgroundDark), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&H2A, &H2D, &H60)), AddressOf OnVariantBrushChanged))


    ' ── ItemSelectedBorderBrush (effective) ───────────────────────────────────

    Public Property ItemSelectedBorderBrush As Brush
        Get
            Return CType(GetValue(ItemSelectedBorderBrushProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(ItemSelectedBorderBrushProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ItemSelectedBorderBrushProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ItemSelectedBorderBrush), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&H96, &HDB, &HFF))))

    Public Property ItemSelectedBorderBrushLight As Brush
        Get
            Return CType(GetValue(ItemSelectedBorderBrushLightProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(ItemSelectedBorderBrushLightProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ItemSelectedBorderBrushLightProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ItemSelectedBorderBrushLight), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&H96, &HDB, &HFF)), AddressOf OnVariantBrushChanged))

    Public Property ItemSelectedBorderBrushDark As Brush
        Get
            Return CType(GetValue(ItemSelectedBorderBrushDarkProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(ItemSelectedBorderBrushDarkProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ItemSelectedBorderBrushDarkProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ItemSelectedBorderBrushDark), GetType(Brush), GetType(ThemedComboBox),
            New PropertyMetadata(New SolidColorBrush(Color.FromRgb(&H90, &HA8, &HF0)), AddressOf OnVariantBrushChanged))


    ' ── PlaceholderText ───────────────────────────────────────────────────────

    Public Property PlaceholderText As String
        Get
            Return CStr(GetValue(PlaceholderTextProperty))
        End Get
        Set(value As String)
            SetValue(PlaceholderTextProperty, value)
        End Set
    End Property

    Public Shared ReadOnly PlaceholderTextProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(PlaceholderText), GetType(String), GetType(ThemedComboBox),
            New PropertyMetadata(String.Empty))


    ' ── IconDark / IconLight / ActiveIcon ─────────────────────────────────────
    '    ActiveIcon is read-only and automatically reflects the correct icon
    '    for the current IsDarkMode state.

    Public Property IconDark As Object
        Get
            Return GetValue(IconDarkProperty)
        End Get
        Set(value As Object)
            SetValue(IconDarkProperty, value)
        End Set
    End Property

    Public Shared ReadOnly IconDarkProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(IconDark), GetType(Object), GetType(ThemedComboBox),
            New PropertyMetadata(Nothing, AddressOf OnIconChanged))


    Public Property IconLight As Object
        Get
            Return GetValue(IconLightProperty)
        End Get
        Set(value As Object)
            SetValue(IconLightProperty, value)
        End Set
    End Property

    Public Shared ReadOnly IconLightProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(IconLight), GetType(Object), GetType(ThemedComboBox),
            New PropertyMetadata(Nothing, AddressOf OnIconChanged))


    Private Shared ReadOnly ActiveIconPropertyKey As DependencyPropertyKey =
        DependencyProperty.RegisterReadOnly(NameOf(ActiveIcon), GetType(Object), GetType(ThemedComboBox),
            New PropertyMetadata(Nothing))

    Public Shared ReadOnly ActiveIconProperty As DependencyProperty = ActiveIconPropertyKey.DependencyProperty

    Public ReadOnly Property ActiveIcon As Object
        Get
            Return GetValue(ActiveIconProperty)
        End Get
    End Property

    Private Shared Sub OnIconChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        CType(d, ThemedComboBox).UpdateActiveIcon()
    End Sub

    Private Sub UpdateActiveIcon()
        SetValue(ActiveIconPropertyKey, If(IsDarkMode, IconDark, IconLight))
    End Sub



    ' ── Forwarded ComboBox properties ─────────────────────────────────────────
    '    Properties that callers may bind to must be DependencyProperties.
    '    CLR-only properties silently reject {Binding} — the engine tries to
    '    assign the Binding object itself and fails the type check.

    ' ContentProperty("Items") routes XAML child elements here instead of UserControl.Content,
    ' allowing <local:ThemedComboBox><ComboBoxItem>...</ComboBoxItem></local:ThemedComboBox> syntax.
    '
    Public ReadOnly Property Items As ItemCollection
        Get
            Return InnerCombo.Items
        End Get
    End Property


    Public Property ItemsSource As IEnumerable
        Get
            Return CType(GetValue(ItemsSourceProperty), IEnumerable)
        End Get
        Set(value As IEnumerable)
            SetValue(ItemsSourceProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ItemsSourceProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ItemsSource), GetType(IEnumerable), GetType(ThemedComboBox),
            New PropertyMetadata(Nothing, Sub(d, e) CType(d, ThemedComboBox).InnerCombo.ItemsSource = CType(e.NewValue, IEnumerable)))



    Public Property ItemTemplate As DataTemplate
        Get
            Return CType(GetValue(ItemTemplateProperty), DataTemplate)
        End Get
        Set(value As DataTemplate)
            SetValue(ItemTemplateProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ItemTemplateProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ItemTemplate), GetType(DataTemplate), GetType(ThemedComboBox),
            New PropertyMetadata(Nothing, Sub(d, e) CType(d, ThemedComboBox).InnerCombo.ItemTemplate = CType(e.NewValue, DataTemplate)))



    Public Property DisplayMemberPath As String
        Get
            Return CStr(GetValue(DisplayMemberPathProperty))
        End Get
        Set(value As String)
            SetValue(DisplayMemberPathProperty, value)
        End Set
    End Property

    Public Shared ReadOnly DisplayMemberPathProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(DisplayMemberPath), GetType(String), GetType(ThemedComboBox),
            New PropertyMetadata(String.Empty, Sub(d, e) CType(d, ThemedComboBox).InnerCombo.DisplayMemberPath = CStr(e.NewValue)))



    Public Property SelectedItem As Object
        Get
            Return GetValue(SelectedItemProperty)
        End Get
        Set(value As Object)
            SetValue(SelectedItemProperty, value)
        End Set
    End Property

    Public Shared ReadOnly SelectedItemProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(SelectedItem), GetType(Object), GetType(ThemedComboBox),
            New FrameworkPropertyMetadata(Nothing, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                Sub(d, e)
                    Dim ctrl = CType(d, ThemedComboBox)
                    If ctrl._syncingSelection Then Return
                    ctrl._syncingSelection = True
                    ctrl.InnerCombo.SelectedItem = e.NewValue
                    ctrl._syncingSelection = False
                End Sub))



    Public Property SelectedIndex As Integer
        Get
            Return CInt(GetValue(SelectedIndexProperty))
        End Get
        Set(value As Integer)
            SetValue(SelectedIndexProperty, value)
        End Set
    End Property

    Public Shared ReadOnly SelectedIndexProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(SelectedIndex), GetType(Integer), GetType(ThemedComboBox),
            New FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                Sub(d, e)
                    Dim ctrl = CType(d, ThemedComboBox)
                    If ctrl._syncingSelection Then Return
                    ctrl._syncingSelection = True
                    ctrl.InnerCombo.SelectedIndex = CInt(e.NewValue)
                    ctrl._syncingSelection = False
                End Sub))

    Public Property SelectedValue As Object
        Get
            Return GetValue(SelectedValueProperty)
        End Get
        Set(value As Object)
            SetValue(SelectedValueProperty, value)
        End Set
    End Property



    Public Shared ReadOnly SelectedValueProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(SelectedValue), GetType(Object), GetType(ThemedComboBox),
            New FrameworkPropertyMetadata(Nothing, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                Sub(d, e)
                    Dim ctrl = CType(d, ThemedComboBox)
                    If ctrl._syncingSelection Then Return
                    ctrl._syncingSelection = True
                    ctrl.InnerCombo.SelectedValue = e.NewValue
                    ctrl._syncingSelection = False
                End Sub))

    Public Property SelectedValuePath As String
        Get
            Return CStr(GetValue(SelectedValuePathProperty))
        End Get
        Set(value As String)
            SetValue(SelectedValuePathProperty, value)
        End Set
    End Property

    Public Shared ReadOnly SelectedValuePathProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(SelectedValuePath), GetType(String), GetType(ThemedComboBox),
            New PropertyMetadata(String.Empty, Sub(d, e) CType(d, ThemedComboBox).InnerCombo.SelectedValuePath = CStr(e.NewValue)))



    Public Property MaxDropDownHeight As Double
        Get
            Return CDbl(GetValue(MaxDropDownHeightProperty))
        End Get
        Set(value As Double)
            SetValue(MaxDropDownHeightProperty, value)
        End Set
    End Property

    Public Shared ReadOnly MaxDropDownHeightProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(MaxDropDownHeight), GetType(Double), GetType(ThemedComboBox),
            New PropertyMetadata(Double.PositiveInfinity, Sub(d, e) CType(d, ThemedComboBox).InnerCombo.MaxDropDownHeight = CDbl(e.NewValue)))

    Public ReadOnly Property IsDropDownOpen As Boolean
        Get
            Return InnerCombo.IsDropDownOpen
        End Get
    End Property


    ' ── Forwarded events ──────────────────────────────────────────────────────

    Public Custom Event SelectionChanged As SelectionChangedEventHandler

        AddHandler(value As SelectionChangedEventHandler)
            AddHandler InnerCombo.SelectionChanged, value
        End AddHandler
        RemoveHandler(value As SelectionChangedEventHandler)
            RemoveHandler InnerCombo.SelectionChanged, value
        End RemoveHandler
        RaiseEvent(sender As Object, e As SelectionChangedEventArgs)
        End RaiseEvent

    End Event

    Public Custom Event DropDownOpened As EventHandler

        AddHandler(value As EventHandler)
            AddHandler InnerCombo.DropDownOpened, value
        End AddHandler
        RemoveHandler(value As EventHandler)
            RemoveHandler InnerCombo.DropDownOpened, value
        End RemoveHandler
        RaiseEvent(sender As Object, e As EventArgs)
        End RaiseEvent

    End Event

    Public Custom Event DropDownClosed As EventHandler

        AddHandler(value As EventHandler)
            AddHandler InnerCombo.DropDownClosed, value
        End AddHandler
        RemoveHandler(value As EventHandler)
            RemoveHandler InnerCombo.DropDownClosed, value
        End RemoveHandler
        RaiseEvent(sender As Object, e As EventArgs)
        End RaiseEvent

    End Event

End Class


''' <summary>
''' Extracts the SelectedItem's display text via reflection using DisplayMemberPath,
''' bypassing WPF's internal SelectionBoxItem/SelectionBoxItemTemplate computation
''' (unreliable in heavily re-templated ComboBoxes). Supports dotted paths ("User.Name").
''' Passes the item through unchanged when DisplayMemberPath is empty, so ItemTemplate
''' (bound separately as ContentTemplate) still renders the raw item correctly.
''' </summary>
''' 
Public Class DisplayMemberPathConverter

    Implements IMultiValueConverter

    Public Function Convert(values() As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object _
        Implements IMultiValueConverter.Convert

        If values Is Nothing OrElse values.Length < 2 OrElse values(0) Is Nothing OrElse values(0) Is DependencyProperty.UnsetValue Then
            Return Nothing
        End If

        Dim item As Object = values(0)
        Dim path As String = TryCast(values(1), String)

        ' When items are inline ComboBoxItem elements (rather than data objects),
        ' SelectedItem IS the live ComboBoxItem — still parented inside the dropdown's
        ' ItemsPresenter. Returning it as-is would make the caller assign that same
        ' UIElement as ContentPresenter.Content, throwing "already has a parent".
        ' Extract its Content instead, mirroring WPF's own SelectionBoxItem behavior.
        Dim cc = TryCast(item, ContentControl)
        If cc IsNot Nothing Then
            item = cc.Content
            If item Is Nothing Then Return Nothing
        End If

        If String.IsNullOrEmpty(path) Then Return item

        Dim current As Object = item
        For Each segment In path.Split("."c)
            If current Is Nothing Then Exit For
            Dim prop = current.GetType().GetProperty(segment)
            If prop Is Nothing Then Return item
            current = prop.GetValue(current)
        Next

        Return current
    End Function

    Public Function ConvertBack(value As Object, targetTypes() As Type, parameter As Object, culture As CultureInfo) As Object() _
        Implements IMultiValueConverter.ConvertBack
        Throw New NotSupportedException()
    End Function

End Class
