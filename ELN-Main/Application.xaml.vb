Imports System.Windows
Imports System.Windows.Threading
Imports ElnBase

Class Application

    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.

    Protected Overrides Sub OnStartup(e As StartupEventArgs)

        AddHandler Me.DispatcherUnhandledException, AddressOf App_DispatcherUnhandledException
        MyBase.OnStartup(e)

        'Display a splash screen to hide initially white application window
        '-------------------------------------------------------------------

        'The splash's marquee animation needs its own thread's Dispatcher pumping to render the progress animation smoothly.

        Dim splash As SplashWindow = Nothing
        Dim splashDispatcher As Dispatcher = Nothing
        Dim splashReady As New System.Threading.ManualResetEventSlim(False)

        Dim splashThread As New System.Threading.Thread(
            Sub()
                splash = New SplashWindow()
                splash.Show()
                splashDispatcher = Dispatcher.CurrentDispatcher
                splashReady.Set()
                Dispatcher.Run()
            End Sub)
        splashThread.SetApartmentState(System.Threading.ApartmentState.STA)
        splashThread.IsBackground = True
        splashThread.Name = "SplashThread"
        splashThread.Start()
        splashReady.Wait()

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

        'MainWindow's own constructor already decided where it belongs on screen (a saved position, or
        'CenterScreen). Stash that, then move it off the visible desktop for its initial Show() - Loaded
        'and ContentRendered still fire completely normally this way, since the window genuinely is being
        'shown and rendered, just outside all connected monitor's viewports, so there's nothing for the user to see.

        Dim intendedLeft As Double
        Dim intendedTop As Double

        If mainWin.WindowStartupLocation = WindowStartupLocation.CenterScreen Then
            intendedLeft = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - mainWin.Width) / 2
            intendedTop = SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height - mainWin.Height) / 2
        Else
            intendedLeft = mainWin.Left
            intendedTop = mainWin.Top
        End If

        mainWin.WindowStartupLocation = WindowStartupLocation.Manual
        mainWin.Left = SystemParameters.VirtualScreenLeft - mainWin.Width - 100
        mainWin.Top = SystemParameters.VirtualScreenTop - mainWin.Height - 100

        AddHandler mainWin.ContentRendered, Sub()
                                                 mainWin.Left = intendedLeft
                                                 mainWin.Top = intendedTop
                                                 splashDispatcher.Invoke(Sub() splash.Close())
                                                 splashDispatcher.InvokeShutdown()

                                                'force window explicitly to foreground,
                                                'so it doesn't end up sitting visible-but-behind other windows
                                                mainWin.Activate()

                                            End Sub

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
