Imports System.Runtime.InteropServices
Imports System.Windows
Imports System.Windows.Interop

Public Class dlgServerSyncIssue

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Friend Shared Function SetWindowLong(hwnd As IntPtr, index As Integer, value As Integer) As Integer
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Friend Shared Function GetWindowLong(hwnd As IntPtr, index As Integer) As Integer
    End Function

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub

    Private Sub Me_Loaded() Handles Me.Loaded

        'removes the window close icon
        Const GWL_STYLE As Integer = -16
        Const WS_SYSMENU As Integer = &H80000
        Dim hwnd = New WindowInteropHelper(Me).Handle
        Dim unused = SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) And (Not WS_SYSMENU))

    End Sub

    Private Sub btnRestore_Click(sender As Object, e As RoutedEventArgs) Handles btnRestore.Click

        Me.Close()

    End Sub

End Class
