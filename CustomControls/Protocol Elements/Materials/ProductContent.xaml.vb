

Imports System.ComponentModel
Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Media
Imports ElnCoreModel

Public Class ProductContent

    Public Shared Event RequestEditProductPlaceholder(sender As Object, parentListBoxItem As ListBoxItem)


    Public Sub New()

        InitializeComponent()

    End Sub


    Private Sub lnkEditProduct_Click() Handles lnkEditProduct.MouseUp

        Dim parentListBoxItem = WPFToolbox.FindVisualParent(Of ListBoxItem)(Me)
        RaiseEvent RequestEditProductPlaceholder(Me, parentListBoxItem)

    End Sub

End Class



Public Class ProductIndexConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim index As Integer = value

        Select Case index
            Case 0
                Return "A:"
            Case 1
                Return "B:"
            Case 2
                Return "C:"
            Case Else
                Return ""
        End Select

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class


Public Class ElementalFormulaConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim efString As String = value
        If efString <> "" Then
            Return WPFToolbox.Convert2NumericSubscriptBlock(efString, New FontFamily("SegoeUI"), 13)
        Else
            Return Nothing
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class


Public Class PlaceholderInfoConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim placeholderInfo As String = value

        If value <> "" Then
            Dim info = placeholderInfo.Split("/"c)
            If info.Length = 3 Then
                If parameter = "prodIndex" Then
                    Dim indexConv As New ProductIndexConverter
                    Dim label = indexConv.Convert(Val(info(1)), Nothing, Nothing, Nothing)
                    Return label
                ElseIf parameter = "prodYield" Then
                    Return info(2)
                End If
            End If
        End If

        Return Nothing

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class

