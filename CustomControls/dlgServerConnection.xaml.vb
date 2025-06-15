
Imports System.Windows
Imports System.Windows.Input
Imports ElnBase
Imports ElnCoreModel


Public Class dlgServerConnection

    ''' <summary>
    ''' Raises an event when the server context was created.
    ''' </summary>
    ''' 
    Public Shared Event ServerContextCreated(dbContext As ElnDataContext)

    Public Shared Property NewServerContext As ElnDbContext
    Private _LocalContext As ElnDbContext


    Public Sub New(localContext As ElnDbContext, currServerContext As ElnDbContext)

        'This call is required by the designer.
        InitializeComponent()

        txtCustomPort.Visibility = Visibility.Hidden
        rdoDefaultPort.Content = "Default"

        With My.Settings
            txtServerPath.Text = .ServerName
            txtUserID.Text = .ServerDbUserName
            txtCustomPort.Text = .ServerPort.ToString
            txtPassword.Password = .ServerDbPassword
            If .ServerPort <> 3306 Then
                rdoCustomPort.IsChecked = True
            End If
        End With

        _LocalContext = localContext

        btnConnect.IsEnabled = currServerContext Is Nothing

        btnDisconnect.IsEnabled = currServerContext IsNot Nothing OrElse
                                  (currServerContext Is Nothing AndAlso My.Settings.IsServerSpecified AndAlso
                                   Not My.Settings.IsServerOffByUser)

        If Not btnDisconnect.IsEnabled Then
            btnDisconnect.Opacity = 0.7
        End If

    End Sub


    Private Sub lnkDownload_Click() Handles lnkDownload.MouseUp

        Dim info As New ProcessStartInfo("https://github.com/abrechts/Phoenix-ELN-Server-Package/releases")
        info.UseShellExecute = True
        Process.Start(info)

    End Sub


    Private Sub rdoCustomPort_Changed() Handles rdoCustomPort.Checked, rdoCustomPort.Unchecked

        If rdoCustomPort.IsChecked Then
            txtCustomPort.Visibility = Visibility.Visible
        Else
            txtCustomPort.Visibility = Visibility.Hidden
        End If

    End Sub


    Private Sub rdoCustomPort_PreviewMouseUp() Handles rdoCustomPort.PreviewMouseUp

        txtCustomPort.Visibility = Visibility.Visible
        txtCustomPort.Focus()
        txtCustomPort.SelectAll()
        rdoCustomPort.IsChecked = True

    End Sub


    ''' <summary>
    ''' Allow only positive integer text input for port number
    ''' </summary>
    ''' 
    Private Sub txtCustomPort_PreviewTextInput(sender As Object, e As TextCompositionEventArgs) Handles txtCustomPort.PreviewTextInput

        Dim inputStr = e.Text

        If inputStr.Contains("."c) OrElse inputStr.Contains(","c) OrElse inputStr.Contains("-"c) Then
            e.Handled = True
            Exit Sub
        End If

        Dim res = From myChar In inputStr Where Not Char.IsDigit(myChar)
        If res.Any Then
            e.Handled = True
        End If

    End Sub


    Private Sub txtCustomPort_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles txtCustomPort.PreviewKeyDown

        'TextInput does not fire on SpaceBar ...
        If e.Key = Key.Space Then
            e.Handled = True
        End If

    End Sub


    Private Sub btnDisconnect_Click() Handles btnDisconnect.Click

        Dim res = MsgBox("Do you really want to disconnect from the server?", MsgBoxStyle.OkCancel + MsgBoxStyle.Exclamation, "Server Connection")
        If res = MsgBoxResult.Ok Then

            My.Settings.IsServerOffByUser = True
            DialogResult = True
            RaiseEvent ServerContextCreated(Nothing)

        End If

    End Sub


    Private Sub btnConnect_Click() Handles btnConnect.Click

        'in case of replacing an already established server sync:
        If ServerSync.IsSynchronizing Then
            MsgBox("A server synchronization currently is in progress." + vbCrLf +
                   "Please try again in a moment ...", MsgBoxStyle.Information, "Server Connection")
            Exit Sub
        End If

        'check connection and create secure connection string
        Me.Cursor = Cursors.Wait

        'synchronous
        DbUpgradeServer.IsUpgradeCheckRequired = True
        NewServerContext = ServerSync.CreateMySQLContext(txtServerPath.Text, txtUserID.Text, txtPassword.Password, My.Settings.ServerPort,
            _LocalContext.tblDatabaseInfo.First)

        If NewServerContext IsNot Nothing Then

            With My.Settings
                .ServerName = txtServerPath.Text
                .ServerDbUserName = txtUserID.Text
                .ServerDbPassword = txtPassword.Password
                .ServerPort = If(rdoCustomPort.IsChecked, Val(txtCustomPort.Text), 3306)
                .IsServerSpecified = True
                .IsServerOffByUser = False
            End With

            DialogResult = True
            RaiseEvent ServerContextCreated(NewServerContext)

            Me.Close()

        Else

            'wrong connection data, password, etc. (does not close dialog)
            MsgBox(ServerSync.CreationErrorMessage, MsgBoxStyle.Exclamation, "Server error")
            Me.Cursor = Cursors.Arrow

        End If

    End Sub


    Private Sub btnCancel_Click() Handles btnCancel.Click

        DialogResult = False
        NewServerContext = Nothing
        Me.Close()

    End Sub


End Class
