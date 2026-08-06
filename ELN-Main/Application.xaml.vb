Imports System.Windows
Imports System.Windows.Threading
Imports ElnBase

Class Application

    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.

    Protected Overrides Sub OnStartup(e As StartupEventArgs)

        AddHandler Me.DispatcherUnhandledException, AddressOf App_DispatcherUnhandledException
        MyBase.OnStartup(e)

        'MainWindow's constructor pays ~500ms to construct its real ElnDbContext. EF Core caches that compiled model per DbContext
        'type, so warming it up here against a disposable in-memory database outside a UI thread. Best-effort: if it fails for any
        'reason, MainWindow's constructor simply pays the cost itself, same as before this existed.

        Task.Run(Sub()
                     Try
                         Using warmup = New SQLiteContext(":memory:").ElnContext
                         End Using
                     Catch
                     End Try
                 End Sub)

        Dim mainWin As New MainWindow()
        Me.MainWindow = mainWin
        mainWin.Show()

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

    End Sub

End Class
