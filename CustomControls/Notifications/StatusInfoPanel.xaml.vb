

Imports System.Globalization
Imports System.Windows
Imports System.Windows.Data
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel

Public Class StatusInfoPanel

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        AddHandler Protocol.WorkflowStateChanged, AddressOf Protocol_WorkflowStateChanged

    End Sub


    Private Sub Protocol_WorkflowStateChanged(sender As Object)

        Dim userEntry = CType(Me.DataContext, tblUsers)

        'refresh bindings
        Me.DataContext = Nothing
        Me.DataContext = userEntry

    End Sub


    Public Sub ShowAvailableUpdate(newVersion As String)

        pnlUpdateInfo.Visibility = Visibility.Visible
        icoUpdateAvailable.Visibility = Visibility.Visible
        icoNoUpdateAvailable.Visibility = Visibility.Collapsed
        blkNewVersion.Text = newVersion

    End Sub


    Private Sub lnkVersionInfo_Click() Handles lnkVersionInfo.PreviewMouseUp

        Dim info As New ProcessStartInfo("https://github.com/abrechts/Phoenix-ELN/releases") 'TODO: Add actual link target
        info.UseShellExecute = True
        Process.Start(info)

    End Sub

End Class


Public Class UnfinalizedCountConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim userExperiments As ICollection(Of tblExperiments) = value

        Dim count = (From exp In userExperiments Where exp.WorkflowState <> WorkflowStatus.Finalized).Count
        Return count

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class


Public Class UnfinalizedWarningVisibilityConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim userEntries As ICollection(Of tblExperiments) = value

        Dim countLimit As Integer = 7

        Dim count = (From exp In userEntries Where exp.WorkflowState <> WorkflowStatus.Finalized).Count

        If parameter = "" Then
            Return If(count > countLimit, Visibility.Visible, Visibility.Collapsed)
        Else    'invert
            Return If(count > countLimit, Visibility.Collapsed, Visibility.Visible)
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class


Public Class AppVersionConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim appVersionStr As String = value

        Dim appVersion = New Version(appVersionStr).ToString(3)
        Return appVersion

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class