Imports System.Windows.Threading

Class Application

    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.

    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        AddHandler Me.DispatcherUnhandledException, AddressOf App_DispatcherUnhandledException
        MyBase.OnStartup(e)
    End Sub


    Private Sub App_DispatcherUnhandledException(sender As Object, e As DispatcherUnhandledExceptionEventArgs)

        ' --> replace MsgBox by dialog with scrollable readonly textbox containing error stack inf

        Dim errText = e.Exception.Message + vbCrLf + vbCrLf

        Dim lstErrStack = e.Exception.StackTrace.Split(vbCrLf)
        'take first 5 items with double newlines
        errText += String.Join(vbCrLf + vbCrLf, lstErrStack.Take(5))

        Dim crashDlg As New dlgCrashReport
        With crashDlg
            .txtErrLog.Text = errText
            .ShowDialog()
        End With

        e.Handled = True
        Application.Current.Shutdown()

    End Sub

End Class
