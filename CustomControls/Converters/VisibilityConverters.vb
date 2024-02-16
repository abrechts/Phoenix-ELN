Imports System.Globalization
Imports System.Windows
Imports System.Windows.Data

Public Class StringToVisibilityConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim testStr As String = value
        Return If(testStr = "", Visibility.Collapsed, Visibility.Visible)


    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class


Public Class NothingToVisibilityConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim testObj As Object = value
        Return If(testObj Is Nothing, Visibility.Collapsed, Visibility.Visible)

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class


Public Class IntegerToVisibilityConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim testVal As Integer = value
        If parameter <> "invert" Then
            Return If(testVal = 0, Visibility.Collapsed, Visibility.Visible)
        Else
            Return If(testVal = 0, Visibility.Visible, Visibility.Collapsed)
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class


Public Class BooleanToVisibilityConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim testVal As Boolean = value
        If parameter <> "invert" Then
            Return If(Not testVal, Visibility.Collapsed, Visibility.Visible)
        Else
            Return If(Not testVal, Visibility.Visible, Visibility.Collapsed)
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class


Public Class IntegerToHiddenConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim testVal As Integer = value
        If parameter <> "invert" Then
            Return If(testVal = 0, Visibility.Hidden, Visibility.Visible)
        Else
            Return If(testVal = 0, Visibility.Visible, Visibility.Hidden)
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class


Public Class IntegerToBooleanConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim testVal As Integer = value
        If parameter <> "invert" Then
            Return testVal <> 0
        Else
            Return testVal = 0
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        Dim boolVal = value
        Return If(boolVal = True, 1, 0)

    End Function

End Class



Public Class ReagentDetailsEnabledConverter

    Implements IMultiValueConverter

    Public Function Convert(values() As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IMultiValueConverter.Convert

        If values(0) IsNot DependencyProperty.UnsetValue AndAlso values(1) IsNot DependencyProperty.UnsetValue Then

            Dim molweight As Double = values(0)
            Dim molarity As Double? = values(1)
            Dim cboMwMolarIndex As Integer = values(2)

            If (molweight <= 0 AndAlso cboMwMolarIndex = 0) OrElse (cboMwMolarIndex = 1 AndAlso
                (molarity Is Nothing OrElse molarity <= 0)) Then
                Return False
            Else
                Return True
            End If

        Else
            Return False
        End If

    End Function

    Public Function ConvertBack(value As Object, targetTypes() As Type, parameter As Object, culture As CultureInfo) As Object() Implements IMultiValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class
