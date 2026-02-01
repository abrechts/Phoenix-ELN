Imports System.Windows.Input
Imports ElnBase
Imports ElnCoreModel

Public Class dlgServerConflict

    ' The parent server dbInfo entry of any of the local conflicting users
    Dim _serverDbInfoEntry As tblDatabaseInfo

    Public Sub New(serverDbInfoEntry As tblDatabaseInfo)

        ' This call is required by the designer.
        InitializeComponent()
        _serverDbInfoEntry = serverDbInfoEntry

    End Sub


    ''' <summary>
    ''' If false, then the Rename strategy was selected.
    ''' </summary>
    ''' 
    Public Property UseRestoreStrategy As Boolean


    ''' <summary>
    ''' Gets if the provided password matches any of the parent server dbInfo users passwords.
    ''' </summary>
    ''' <param name="serverDbInfoEntry"></param>
    ''' 
    Private Function IsUserPasswordValid(conflictingLocalUsers As IEnumerable(Of tblUsers)) As Boolean

        For Each user In _serverDbInfoEntry.tblUsers
            Dim pwHash = user.PWHash
            If pwHash = ELNCryptography.GetSHA256Hash(pwUserPassword.Password) Then
                Return True
            End If
        Next

        cbMsgBox.Display("Wrong password.- Use the personal password" + vbCrLf +
               "originally used for finalizing your" + vbCrLf +
               "experiments now present on the server.", MsgBoxStyle.Exclamation, "Restore")
        Me.ForceCursor = False
        Me.Cursor = Cursors.Arrow

        Return False

    End Function


    Private Sub btnOK_Click() Handles btnOK.Click

        If rdoRestore.IsChecked AndAlso IsUserPasswordValid(_serverDbInfoEntry.tblUsers) Then

            UseRestoreStrategy = rdoRestore.IsChecked
            DialogResult = True
            Me.Close()

        Else

            UseRestoreStrategy = False
            DialogResult = True
            Me.Close()

        End If

    End Sub


    Private Sub btnCancel_Click() Handles btnCancel.Click

        DialogResult = False

    End Sub


End Class
