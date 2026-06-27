Imports System.Globalization
Imports System.Windows
Imports System.Windows.Data
Imports System.Windows.Media

''' <summary>
''' Provides an inherited attached property that propagates dark-mode state down the element tree,
''' and a converter for switching brush values between light and dark.
''' </summary>
Public Class DarkModeHelper

    ''' <summary>
    ''' Attached, inherited DP. Set it on a container element; all descendants read the same value.
    ''' </summary>
    Public Shared ReadOnly IsDarkModeProperty As DependencyProperty =
        DependencyProperty.RegisterAttached(
            "IsDarkMode",
            GetType(Boolean),
            GetType(DarkModeHelper),
            New FrameworkPropertyMetadata(False, FrameworkPropertyMetadataOptions.Inherits))

    Public Shared Function GetIsDarkMode(obj As DependencyObject) As Boolean
        Return DirectCast(obj.GetValue(IsDarkModeProperty), Boolean)
    End Function

    Public Shared Sub SetIsDarkMode(obj As DependencyObject, value As Boolean)
        obj.SetValue(IsDarkModeProperty, value)
    End Sub

End Class


''' <summary>
''' Converts a Boolean IsDarkMode value to a Brush.
''' ConverterParameter must be two color strings separated by '|': "LightColor|DarkColor"
''' e.g. ConverterParameter='#FF333333|#FFD4D4D4'
''' </summary>
Public Class DarkModeColorConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object,
                            culture As CultureInfo) As Object Implements IValueConverter.Convert
        Dim isDark = CBool(value)
        Dim parts = CStr(parameter).Split("|"c)
        Dim colorStr = If(isDark, parts(1), parts(0))
        Return New BrushConverter().ConvertFromString(colorStr)
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object,
                                culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class
