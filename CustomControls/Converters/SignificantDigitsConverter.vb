Imports System.Globalization
Imports System.Windows.Data
Imports ElnBase

Public Class SignificantDigitsConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim myVal As Double = value
        Dim sigDigits As Integer = parameter

        Dim valStr = ELNCalculations.SignificantDigitsString(myVal, sigDigits)
        Return valStr

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        Return value

    End Function

End Class
