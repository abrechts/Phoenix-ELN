﻿Imports System.Globalization
Imports System.Windows
Imports System.Windows.Data
Imports ElnCoreModel

Public Class ExpTabHeader

    Public Shared Event PinStateChanged(sender As Object, targetExp As tblExperiments)

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        '     Dim currExp = CType(Me.DataContext, tblExperiments)

        If CType(Me.DataContext, tblExperiments).DisplayIndex = -2 Then
            blkExpID.FontStyle = FontStyles.Italic
            icoPin.Content = "X"
        End If

    End Sub

    Private Sub Me_PreviewMouseLeftButtonDown(sender As Object, e As RoutedEventArgs) Handles Me.PreviewMouseLeftButtonDown

        If btnPin.IsMouseOver Then
            Dim expEntry = CType(DataContext, tblExperiments)
            RaiseEvent PinStateChanged(Me, expEntry)
            e.Handled = True
        End If

    End Sub

End Class


Public Class PinnedEnabledConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim displayIndex As Integer = value
        Return If(displayIndex = -1 OrElse displayIndex > 0, Visibility.Visible, Visibility.Collapsed)

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class


Public Class PinToolTipConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim displayIndex As Integer = value

        Select Case displayIndex
            Case -2
                Return "Close server experiment"
            Case -1
                Return "Undo Pin"
            Case 0
                Return "Keep experiment (pin)"
            Case Else
                Return "Release pinned experiment"
        End Select

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class
