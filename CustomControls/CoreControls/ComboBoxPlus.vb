
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media

Public Class ComboPlus

    Inherits ComboBox

    Public Enum PopupPlacementMode
        Auto
        Down
        Up
    End Enum


    Private Property ToggleButtonBorder As New Border


    Public Sub Me_Loaded() Handles Me.Loaded

        ApplyTemplate()
        Dim toggleButton = TryCast(Template.FindName("toggleButton", Me), Control)

        toggleButton.ApplyTemplate()
        ToggleButtonBorder = TryCast(toggleButton.Template.FindName("templateRoot", toggleButton), Border)

        Dim cboPopup = TryCast(Template.FindName("PART_Popup", Me), Primitives.Popup)
        If DropdownDirection = PopupPlacementMode.Down Then
            cboPopup.Placement = Primitives.PlacementMode.Bottom
        ElseIf DropdownDirection = PopupPlacementMode.Up Then
            cboPopup.Placement = Primitives.PlacementMode.Top
        End If

        'set defaults
        With ToggleButtonBorder
            .CornerRadius = CornerRadius
            .Background = CboBackground
            .BorderBrush = CboBorderBrush
        End With

        Padding = New Thickness(Padding.Left + 3, Padding.Top, Padding.Right, Padding.Bottom)

    End Sub


    ''' <summary>
    ''' Sets or gets the direction the ComboBox dropdown should open: Up, down or auto (default)
    ''' </summary>
    ''' 
    Public Property DropDownDirection As PopupPlacementMode = PopupPlacementMode.Auto



    ''' <summary>
    ''' Change opacity when control is disabled.
    ''' </summary>
    ''' 
    Private Sub Me_IsEnabledChanged() Handles Me.IsEnabledChanged
        Opacity = If(IsEnabled, 1, 0.6)
    End Sub


    ''' <summary>
    ''' Sets or gets the corner radius of the ComboBox
    ''' </summary>
    '''
    Public Property CornerRadius() As CornerRadius
        Get
            Return GetValue(CornerRadiusProperty)
        End Get
        Set(ByVal value As CornerRadius)
            SetValue(CornerRadiusProperty, value)
        End Set
    End Property

    Public Shared Shadows ReadOnly CornerRadiusProperty As DependencyProperty =
       DependencyProperty.Register("CornerRadius", GetType(CornerRadius), GetType(ComboPlus),
       New PropertyMetadata(New CornerRadius(8), AddressOf OnCornerRadiusChanged))

    Private Shared Sub OnCornerRadiusChanged(ByVal o As DependencyObject, ByVal e As DependencyPropertyChangedEventArgs)

        Dim myCtrl = CType(o, ComboPlus)
        If myCtrl.ToggleButtonBorder IsNot Nothing Then
            myCtrl.ToggleButtonBorder.CornerRadius = e.NewValue
        End If

    End Sub


    ''' <summary>
    ''' Sets or gets the background brush of the ComboBox
    ''' </summary>
    '''
    Public Property CboBackground() As Brush
        Get
            Return GetValue(CboBackgroundProperty)
        End Get
        Set(ByVal value As Brush)
            SetValue(CboBackgroundProperty, value)
        End Set
    End Property

    Public Shared Shadows ReadOnly CboBackgroundProperty As DependencyProperty =
       DependencyProperty.Register("CboBackground", GetType(Brush), GetType(ComboPlus),
       New PropertyMetadata(New BrushConverter().ConvertFrom("#FFDEF0FF"), AddressOf OnCboBackgroundChanged))

    Private Shared Sub OnCboBackgroundChanged(ByVal o As DependencyObject, ByVal e As DependencyPropertyChangedEventArgs)

        Dim myCtrl = CType(o, ComboPlus)
        If myCtrl.ToggleButtonBorder IsNot Nothing Then
            myCtrl.ToggleButtonBorder.Background = e.NewValue
        End If

    End Sub


    ''' <summary>
    ''' Sets or gets the border brush of the ComboBox
    ''' </summary>
    '''
    Public Property CboBorderBrush() As Brush
        Get
            Return GetValue(CboBorderBrushProperty)
        End Get
        Set(ByVal value As Brush)
            SetValue(CboBorderBrushProperty, value)
        End Set
    End Property

    Public Shared Shadows ReadOnly CboBorderBrushProperty As DependencyProperty =
       DependencyProperty.Register("CboBorderBrush", GetType(Brush), GetType(ComboPlus),
       New PropertyMetadata(New BrushConverter().ConvertFrom("#FF45A1FD"), AddressOf OnCboBorderBrushChanged))

    Private Shared Sub OnCboBorderBrushChanged(ByVal o As DependencyObject, ByVal e As DependencyPropertyChangedEventArgs)

        Dim myCtrl = CType(o, ComboPlus)
        If myCtrl.ToggleButtonBorder IsNot Nothing Then
            myCtrl.ToggleButtonBorder.BorderBrush = e.NewValue
        End If

    End Sub


End Class
