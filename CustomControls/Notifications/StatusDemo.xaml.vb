Imports System.Globalization
Imports System.Windows
Imports System.Windows.Data
Imports System.Windows.Media

Public Class StatusDemo

    Public Shared Event RequestCreateFirstUser(sender As Object)
    Public Shared Event RequestRestoreServer(sender As Object)

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub


    Private Sub btnNewUser_Click() Handles btnCreateUser.Click

        RaiseEvent RequestCreateFirstUser(Me)

    End Sub


    Private Sub lnkRestoreFromServer_Click() Handles lnkRestore.MouseUp

        RaiseEvent RequestRestoreServer(Me)

    End Sub

End Class



Public Class RemainingDemoCountConverter

    Implements IValueConverter

    Public Shared Property MaxDemoCount As Integer

    Public Shared Property FactoryDemoCount As Integer


    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim totalCount As Integer = value
        Return (FactoryDemoCount + MaxDemoCount) - totalCount

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class


Public Class RemainingDemoBrushConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim totalCount As Integer = value
        Dim remainingCount = (RemainingDemoCountConverter.FactoryDemoCount + RemainingDemoCountConverter.MaxDemoCount) - totalCount

        If remainingCount <= 3 Then
            Return Brushes.Yellow
        Else
            Return Brushes.Transparent
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class


Public Class DemoVisibilityConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim userName As String = value
        Dim param As String = parameter

        If param = "invert" Then
            Return If(userName = "demo", Visibility.Collapsed, Visibility.Visible)
        Else
            Return If(userName = "demo", Visibility.Visible, Visibility.Collapsed)
        End If


    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class


Public Class DemoToBooleanConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim userID As String = value

        If parameter = "invert" Then
            Return userID <> "demo"
        Else
            Return userID = "demo"
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class

