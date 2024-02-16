Imports System.Text.RegularExpressions
Imports ElnBase
Imports ElnCoreModel

Public Class dlgChangeUsername

    Private _dupUserList As List(Of tblUsers)
    Private _dupServerUser As tblUsers
    Private _localDbGuid As String

    Public Sub New(dupUserList As List(Of tblUsers), dupUserEntry As tblUsers, localDbGuid As String)

        ' This call is required by the designer.
        InitializeComponent()

        _dupUserList = dupUserList
        _dupServerUser = dupUserEntry
        _localDbGuid = localDbGuid

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        With _dupServerUser
            blkDuplicates.Text = " • '" + .UserID + "' (" + .FirstName + " " + .LastName + ") "
        End With

    End Sub


    Private Function IsDuplicate(proposedID As String) As Boolean

        proposedID = proposedID.ToLower
        Dim duplicateUsers = From user In _dupUserList Where user.UserID.ToLower = proposedID AndAlso user.DatabaseID <> _localDbGuid

        Return duplicateUsers.Any

    End Function


    Private Sub txtUsername_PreviewTxtInput(sender As Object, e As TextCompositionEventArgs) Handles txtUsername.PreviewTextInput

        Dim myRegex = New Regex("[^a-zA-Z0-9]+")
        e.Handled = myRegex.IsMatch(e.Text)

    End Sub


    Private Sub btnOk_Click() Handles btnOK.Click

        If IsDuplicate(txtUsername.Text) Then
            MsgBox("This user-ID already exists on the server." + vbCrLf +
                   "Please try again!", MsgBoxStyle.Information, "Duplicate UserI-D")
            Exit Sub
        End If

        DialogResult = True

    End Sub


    Private Sub btnCancel_Click() Handles btnCancel.Click

        DialogResult = False

    End Sub


End Class
