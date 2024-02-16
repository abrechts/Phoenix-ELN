Imports System.Globalization
Imports System.IO
Imports System.Windows
Imports System.Windows.Data
Imports System.Windows.Media
Imports System.Windows.Media.Imaging

Public Class BytesToBitmapImgConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim bmImageBytes As Byte() = value

        Using stream As New MemoryStream(bmImageBytes)

            stream.Seek(0, SeekOrigin.Begin)
            Dim bmImage As New BitmapImage()
            With bmImage
                .BeginInit()
                .CacheOption = BitmapCacheOption.OnLoad
                .StreamSource = stream
                .EndInit()
            End With
            Return bmImage

        End Using

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class


''' <summary>
''' Converts the provided image bytes to a BitmapImage and rotates by 90° if the second parameter is set to 1.
''' </summary>
''' 
Public Class BytesToBitmapImgRotateConverter

    Implements IMultiValueConverter

    Public Function Convert(values() As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IMultiValueConverter.Convert

        If values(0) Is DependencyProperty.UnsetValue Then
            Return Nothing
        End If

        Dim bmImageBytes As Byte() = values(0)
        Dim isRotated As Integer = values(1) 'i.e. in 90° steps

        If bmImageBytes.Length = 0 Then
            Return Nothing
        End If

        Using stream As New MemoryStream(bmImageBytes)

            stream.Seek(0, SeekOrigin.Begin)
            Dim bmImage As New BitmapImage()
            With bmImage
                .BeginInit()
                .CacheOption = BitmapCacheOption.OnLoad
                .Rotation = If(isRotated, Rotation.Rotate90, Rotation.Rotate0)
                .StreamSource = stream
                .EndInit()
            End With
            Return bmImage

        End Using

    End Function

    Public Function ConvertBack(value As Object, targetTypes() As Type, parameter As Object, culture As CultureInfo) As Object() Implements IMultiValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class

