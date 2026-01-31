Imports ElnBase
Imports ElnCoreModel

Public Class dlgReopen

    Private _userEntry As tblUsers

    Public Sub New(userEntry As tblUsers)

        ' This call is required by the designer.
        InitializeComponent()

        _userEntry = userEntry

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        passwordBox.Focus()

    End Sub


    Private Sub btnSign_Click() Handles btnUnlock.Click

        If ELNCryptography.GetSHA256Hash(passwordBox.Password) =
         _userEntry.PWHash Then
            DialogResult = True
            Me.Close()
        Else
            cbMsgBox.Display("Sorry, wrong user password. - Please try again!", MsgBoxStyle.Exclamation + MsgBoxStyle.OkOnly,
            "Password Error")
            passwordBox.Password = ""
            passwordBox.Focus()
        End If

    End Sub

End Class
