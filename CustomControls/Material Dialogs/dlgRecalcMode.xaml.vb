Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports System.Windows
Imports System.Windows.Interop

Public Class dlgRecalcMode

    'This interop provides the infrastructure for removing the 'close' icon from the window

    Private Const GWL_STYLE As Integer = -16
    Private Const WS_SYSMENU As Integer = &H80000

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function GetWindowLong(ByVal hWnd As IntPtr, ByVal nIndex As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SetWindowLong(ByVal hWnd As IntPtr, ByVal nIndex As Integer, ByVal dwNewLong As Integer) As Integer
    End Function


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        'Removes the window 'close' icon

        Dim hwnd = New WindowInteropHelper(Me).Handle
        Dim res = SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) And Not WS_SYSMENU)

    End Sub


    Private Sub btnOK_Click() Handles btnOK.Click

        Me.Close()

    End Sub

End Class
