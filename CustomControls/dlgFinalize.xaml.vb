Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media.Animation
Imports ElnBase
Imports ElnCoreModel


Partial Public Class dlgFinalize

    Private _userEntry As tblUsers
    Private _falseCounter As Integer = 0

    Public Sub New(userEntry As tblUsers)

        ' This call is required by the designer.
        InitializeComponent()

        _userEntry = userEntry

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        'is set to 0.85 opacity in designer to be visible there
        pnlPasswordHint.Opacity = 0

        With _userEntry
            blkName.Text = .FirstName + " " + .LastName
            blkCompany.Text = .CompanyName
            blkDeptSite.Text = .DepartmentName + " (" + .City + ")"
        End With

    End Sub


    Private Sub Me_Rendered() Handles Me.ContentRendered

        With _userEntry
            blkHint.Text = If(IsNothing(.PWHint) OrElse .PWHint = "", "No password hint was specified!", .PWHint)
        End With

        If _userEntry.UserID = "demo" Then
            blkDemoPW.Visibility = Visibility.Visible
        Else
            blkDemoPW.Visibility = Visibility.Collapsed
        End If
        passwordBox.Focus()

    End Sub


    Private Sub btnSign_Click() Handles btnSign.Click

        If ELNCryptography.GetSHA256Hash(passwordBox.Password) = _userEntry.PWHash Then
            DialogResult = True
            Me.Close()
        Else
            MsgBox("Sorry, wrong user password. - Please try again!", MsgBoxStyle.Exclamation + MsgBoxStyle.OkOnly,
            "Password Error")
            passwordBox.Password = ""
            passwordBox.Focus()
            _falseCounter += 1
            If _falseCounter >= 2 Then
                FadeInHint()
            End If
        End If

    End Sub


    Private Sub FadeInHint()

        Dim animOpacity As New DoubleAnimation

        If pnlPasswordHint.Opacity = 0.85 Then
            Exit Sub
        End If

        With animOpacity
            .From = 0
            .To = 0.85
            .DecelerationRatio = 0.3
            .Duration = New Duration(TimeSpan.FromSeconds(0.8))
            .FillBehavior = FillBehavior.HoldEnd
        End With

        pnlPasswordHint.BeginAnimation(UserControl.OpacityProperty, animOpacity)

    End Sub

End Class
