
Imports System.Globalization
Imports System.Windows.Data
Imports ElnBase.ELNEnumerations


Public Class ExpIsUnfinalizedConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim expStatus As WorkflowStatus = value

        Return (expStatus = WorkflowStatus.InProgress OrElse expStatus = WorkflowStatus.Unlocked)

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class
