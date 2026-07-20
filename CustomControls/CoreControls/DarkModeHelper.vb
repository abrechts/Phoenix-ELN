Imports System.Windows

''' <summary>
''' Provides an inherited attached property that propagates dark-mode state down the element tree.
''' </summary>
''' 
Public Class DarkModeHelper

    ''' <summary>
    ''' Attached, inherited DP. Set it on a container element; all descendants read the same value.
    ''' </summary>
    ''' 
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

    ''' <summary>
    ''' Replaces the application level skin resource dictionary (SkinLight.xaml/SkinDark.xaml),
    ''' updating all brushes referenced via DynamicResource. Both skins define an identical
    ''' set of role-named brush keys.
    ''' </summary>
    ''' 
    Public Shared Sub ApplySkin(isDark As Boolean)

        Dim skinUri As New Uri("/CustomControls;component/Resources/" +
            If(isDark, "SkinDark.xaml", "SkinLight.xaml"), UriKind.Relative)

        Dim mergedDict = Application.Current.Resources.MergedDictionaries

        For i = 0 To mergedDict.Count - 1

            Dim src = mergedDict(i).Source

            If src IsNot Nothing AndAlso (src.OriginalString.EndsWith("SkinLight.xaml") OrElse
              src.OriginalString.EndsWith("SkinDark.xaml")) Then
                mergedDict(i) = New ResourceDictionary With {.Source = skinUri}
                Return
            End If

        Next

        mergedDict.Add(New ResourceDictionary With {.Source = skinUri})

    End Sub

End Class
