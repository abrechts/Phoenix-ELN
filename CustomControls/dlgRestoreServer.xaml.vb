Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Input
Imports System.Windows.Threading
Imports ElnBase
Imports ElnCoreModel

Partial Public Class dlgRestoreServer

    Implements INotifyPropertyChanged

    Private _serverContext As ElnDbContext

    Private Enum MessageType As Integer
        None
        ErrorPresent
        BackupPresent
    End Enum


    ' Caps lock indicator for PasswordBox
    '--------------------------------------

    Private _isCapsLockOn As Boolean

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Public Property IsCapsLockOn As Boolean
        Get
            Return _isCapsLockOn
        End Get
        Set(value As Boolean)
            If value = _isCapsLockOn Then Return
            _isCapsLockOn = value
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(IsCapsLockOn)))
        End Set
    End Property

    Private Sub UpdateCapsLockState()
        IsCapsLockOn = Keyboard.IsKeyToggled(Key.CapsLock)
    End Sub

    Private Sub PwdBox_GotKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs) Handles pwBox.GotKeyboardFocus
        UpdateCapsLockState()
    End Sub

    Private Sub PwdBox_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs) Handles pwBox.LostKeyboardFocus
        IsCapsLockOn = False
    End Sub

    Private Sub PwdBox_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles pwBox.PreviewKeyDown
        If e.Key = Key.CapsLock Then
            ' CapsLock toggles after the key event; update on dispatcher
            Dispatcher.BeginInvoke(
                CType(Sub() UpdateCapsLockState(), Action),
                DispatcherPriority.Background)
        End If
    End Sub

    '-------------------------------------



    Public Property TargetDbInfoEntry As tblDatabaseInfo


    Public Sub New(serverContext As ElnDbContext)

        ' This call is required by the designer.
        InitializeComponent()

        _serverContext = serverContext
        blkSearchInfo.Visibility = Visibility.Collapsed

    End Sub


    Private Sub btnContinue_Click() Handles btnContinue.Click

        generalTab.SelectedItem = tabBrowse
        WPFToolbox.WaitForPriority(Threading.DispatcherPriority.Background)
        txtUserID.SelectAll()
        txtUserID.Focus()

    End Sub


    Private Sub btnGoToRestore_Click() Handles btnGoToRestore.Click

        If LCase(txtUserID.Text) = "demo" Then
            cbMsgBox.Display("Sorry, the sandbox 'demo' user can't be restored!" + vbCrLf +
                   "Please specify another username.", MsgBoxStyle.Information, "Restore")
            Exit Sub
        End If

        Me.Cursor = Cursors.Wait
        Me.ForceCursor = True

        If My.Settings.IsServerSpecified AndAlso Not ServerSync.IsServerConnectionLost AndAlso
        _serverContext.Database.CanConnect Then

            Dim userEntry = (From user In _serverContext.tblUsers Where user.UserID = txtUserID.Text).FirstOrDefault

            If IsNothing(userEntry) Then

                'userName not found
                cbMsgBox.Display("Sorry, the specified userID was not found on  " + vbCrLf +
                "the server. - Please try again!", MsgBoxStyle.Exclamation, "Server")

            Else

                'database info was found -> check password
                Dim pwHash = userEntry.PWHash
                If pwHash <> ELNCryptography.GetSHA256Hash(pwBox.Password) Then
                    cbMsgBox.Display("Wrong password.- Use your personal Phoenix " + vbCrLf +
                           "ELN password used for finalizing your" + vbCrLf +
                           "experiments.", MsgBoxStyle.Exclamation, "Restore")
                    Me.ForceCursor = False
                    Me.Cursor = Cursors.Arrow
                    Exit Sub
                End If

                TargetDbInfoEntry = userEntry.Database
                Dim userList As String = ""
                For Each user In TargetDbInfoEntry.tblUsers
                    userList += " ● " + user.FirstName + " " + user.LastName + " (" + user.UserID + ")" + vbCrLf
                Next

                blkUsers.Text = userList
                generalTab.SelectedItem = tabRestore

            End If

        Else

            cbMsgBox.Display("Cannot proceed: Your server currently is unavailable! ", MsgBoxStyle.Exclamation, "Server")
            Me.Close()

        End If

        Me.ForceCursor = False
        Me.Cursor = Cursors.Arrow


    End Sub


    Private Sub btnRestore_Click(ByVal sender As System.Object, ByVal e As RoutedEventArgs) Handles btnRestore.Click

        Me.Close()

    End Sub


    Private Sub btnGotoBrowse_Click(ByVal sender As System.Object, ByVal e As System.Windows.RoutedEventArgs)
        generalTab.SelectedItem = tabBrowse
    End Sub


    Private Sub btnBack_Click(ByVal sender As System.Object, ByVal e As RoutedEventArgs) Handles btnBack2.Click
        generalTab.SelectedItem = tabIntro
    End Sub


    Private Sub btnCancel_Click(ByVal sender As System.Object, ByVal e As RoutedEventArgs) Handles btnCancel.Click, btnCancelFinal.Click

        TargetDbInfoEntry = Nothing
        Me.Close()

    End Sub


End Class
