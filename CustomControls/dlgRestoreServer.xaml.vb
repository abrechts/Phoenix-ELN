Imports System.Windows
Imports System.Windows.Input
Imports ElnBase
Imports ElnCoreModel

Partial Public Class dlgRestoreServer

    Private _ServerEntity As ElnDbContext

    Private Enum MessageType As Integer
        None
        ErrorPresent
        BackupPresent
    End Enum


    Public Property TargetDbInfoEntry As tblDatabaseInfo


    Public Sub New(serverEntity As ElnDbContext)

        ' This call is required by the designer.
        InitializeComponent()

        _ServerEntity = serverEntity
        blkSearchInfo.Visibility = Visibility.Collapsed

    End Sub


    Private Sub btnContinue_Click() Handles btnContinue.Click

        generalTab.SelectedItem = tabBrowse
        WPFToolbox.WaitForPriority(Windows.Threading.DispatcherPriority.Background)
        txtUserID.SelectAll()
        txtUserID.Focus()

    End Sub


    Private Async Sub btnGoToRestore_Click() Handles btnGoToRestore.Click

        If LCase(txtUserID.Text) = "demo" Then
            MsgBox("Sorry, the sandbox 'demo' user can't be restored!" + vbCrLf +
                   "Please specify another username.", MsgBoxStyle.Information, "Restore")
            Exit Sub
        End If

        Me.Cursor = Cursors.Wait
        Me.ForceCursor = True

        If Await Task.Run(Function() ServerSync.IsServerConnAvailable()) Then

            Dim userEntry = (From user In _ServerEntity.tblUsers Where user.UserID = txtUserID.Text).FirstOrDefault

            If IsNothing(userEntry) Then

                'userName not found
                MsgBox("Sorry, the specified userID was not found on  " + vbCrLf +
                "the server. - Please try again!", MsgBoxStyle.Exclamation, "Server")

            Else

                'database info was found -> check password
                Dim pwHash = userEntry.PWHash
                If pwHash <> ELNCryptography.GetSHA256Hash(pwBox.Password) Then
                    MsgBox("Wrong password.- Use your personal Phoenix " + vbCrLf +
                           "ELN password used for finalizing your" + vbCrLf +
                           "experiments.", MsgBoxStyle.Information, "Restore")
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

            MsgBox("Cannot proceed: Your server currently is unavailable! ", MsgBoxStyle.Exclamation, "Server")
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
