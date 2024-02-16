Imports ElnBase

Public Class dlgSyncProgress

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        AddHandler ServerSync.SyncProgress, AddressOf ServerSync_SyncProgress
        AddHandler ServerSync.SyncComplete, AddressOf ServerSync_SyncComplete

    End Sub


    Private Sub ServerSync_SyncProgress(percent As Integer)

        syncProgressBar.Value = percent

    End Sub


    Private Sub ServerSync_SyncComplete()

        Me.Close()

    End Sub


End Class
