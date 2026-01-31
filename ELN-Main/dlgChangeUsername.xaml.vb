Imports System.Text.RegularExpressions
Imports ElnBase
Imports ElnCoreModel
Imports CustomControls

Public Class dlgChangeUsername

    Private _serverDbContext As ElnDbContext
    Private _dupServerUser As tblUsers
    Private _localDbGuid As String

    Public Sub New(serverContext As ElnDbContext, dupUserEntry As tblUsers)

        ' This call is required by the designer.
        InitializeComponent()

        _serverDbContext = serverContext
        _dupServerUser = dupUserEntry

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        With _dupServerUser
            blkDuplicates.Text = " • '" + .UserID + "' (" + .FirstName + " " + .LastName + ") "
        End With

    End Sub


    Private Function IsDuplicate(proposedID As String) As Boolean

        proposedID = proposedID.ToLower
        Dim duplicateServerUsers = From user In _serverDbContext.tblUsers Where user.UserID.ToLower = proposedID
        Dim duplicateLocalUsers = From user In MainWindow.DBContext.tblUsers Where user.UserID.ToLower = proposedID

        Return duplicateServerUsers.Any OrElse duplicateLocalUsers.Any

    End Function


    Private Sub txtUsername_PreviewTxtInput(sender As Object, e As TextCompositionEventArgs) Handles txtUsername.PreviewTextInput

        Dim myRegex = New Regex("[^a-zA-Z0-9]+")
        e.Handled = myRegex.IsMatch(e.Text)

    End Sub


    Private Sub btnOk_Click() Handles btnOK.Click

        If IsDuplicate(txtUsername.Text) Then
            cbMsgBox.Display("This user-ID already exists - please try again!", MsgBoxStyle.Information, "Duplicate UserI-D")
            Exit Sub
        End If

        DialogResult = True

    End Sub


    Private Sub btnCancel_Click() Handles btnCancel.Click

        DialogResult = False

    End Sub


End Class
