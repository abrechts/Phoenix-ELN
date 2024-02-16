Imports System.Runtime.InteropServices
Imports System.Windows.Interop
Imports ElnBase

Public Class dlgRestoreProgress

    'For removing window Close icon.

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Friend Shared Function SetWindowLong(hwnd As IntPtr, index As Integer, value As Integer) As Integer
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Friend Shared Function GetWindowLong(hwnd As IntPtr, index As Integer) As Integer
    End Function


    Private _localDbPath As String
    Private _serverConnStr As String
    Private _serverDbGuid As String

    Public Sub New(serverConnStr As String, localDbPath As String, serverDbGUID As String)

        ' This call is required by the designer.
        InitializeComponent()

        _localDbPath = localDbPath
        _serverConnStr = serverConnStr
        _serverDbGUID = serverDbGUID

    End Sub


    Public Sub Me_Loaded() Handles Me.Loaded

        'Removes the window close icon
        Const GWL_STYLE As Integer = -16
        Const WS_SYSMENU As Integer = &H80000
        Dim hwnd = New WindowInteropHelper(Me).Handle
        Dim res = SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) And (Not WS_SYSMENU))

    End Sub


    Private Property Success As Boolean

    Private Async Sub Me_ContentRendered() Handles Me.ContentRendered

        ''async download
        Dim res = Await Task.Run(Function() RestoreFromServer.Restore(_serverConnStr, _localDbPath, _serverDbGuid))

        'when done: 
        If res = True Then
            blkInfo.Text = "Restore from server COMPLETE."
            progressBar1.Value = 100
            Success = True
        Else
            blkInfo.Text = "Restore from server FAILED!"
            progressBar1.Value = 0
            Success = False
        End If

        btnOK.IsEnabled = True
        progressBar1.IsIndeterminate = False

    End Sub


    Private Sub btnOk_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnOK.Click

        DialogResult = Success
        Me.Close()

    End Sub


End Class
