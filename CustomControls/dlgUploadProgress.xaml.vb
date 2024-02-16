Imports System.ComponentModel
Imports ElnBase

Public Class dlgUploadProgress

    Private _localContext As ElnDbContext
    Private _serverContext As ElnDbContext

    Public Sub New(localContext As ElnDbContext, serverContext As ElnDbContext)

        ' This call is required by the designer.
        InitializeComponent()

        _localContext = localContext
        _serverContext = serverContext

    End Sub


    Private Async Sub Me_ContentRendered() Handles Me.ContentRendered

        ''async upload
        Dim res = Await Task.Run(Function() MySqlBulkUpload.UploadSqliteToMySQL(_localContext, _serverContext))

        'when done: 
        If res = True Then
            blkInfo.Text = "Initial server data upload COMPLETE. Synchronization successfully established."
            progressBar1.Value = 100
        Else
            blkInfo.Text = "Initial data upload FAILED! Currently no server sync possible ... "
            progressBar1.Value = 0
        End If

        btnOK.IsEnabled = True
        progressBar1.IsIndeterminate = False

    End Sub


    'effectively disables the close icon while processing

    Private Sub Me_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If Not btnOK.IsEnabled Then
            e.Cancel = True
        End If
    End Sub


    Private Sub btnOk_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnOK.Click

        Me.Close()

    End Sub


End Class
