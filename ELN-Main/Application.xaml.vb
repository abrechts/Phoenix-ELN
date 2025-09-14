Imports System.Windows.Threading

Class Application

    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.

    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        AddHandler Me.DispatcherUnhandledException, AddressOf App_DispatcherUnhandledException
        MyBase.OnStartup(e)
    End Sub


    Private Sub App_DispatcherUnhandledException(sender As Object, e As DispatcherUnhandledExceptionEventArgs)
        MessageBox.Show("A system error occurred: " & e.Exception.Message, "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Error)
        e.Handled = True
    End Sub

End Class
