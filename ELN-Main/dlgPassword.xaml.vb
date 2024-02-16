
Imports System.ComponentModel
Imports ElnBase

Public Class dlgPassword

    ''' <summary>
    ''' Creates a dialog for entering or updating the user password
    ''' </summary>
    ''' <param name="origPasswordHash">Optional: If the current password hash is specified, the dialog adapts 
    ''' from the new password UI to the change password UI.</param>
    ''' 
    Public Sub New(Optional origPasswordHash As String = Nothing)

        ' This call is required by the designer.
        InitializeComponent()

        OriginalPWHash = origPasswordHash

        If OriginalPWHash IsNot Nothing Then

            blkTitle.Text = "Change Password"
            blkLblCurrent.Visibility = Visibility.Visible
            pwBoxCurrent.Visibility = Visibility.Visible
            btnCancel.Visibility = Visibility.Visible

            blkInfo.Visibility = Visibility.Collapsed
            pwBoxNew.IsEnabled = False
            pwBoxConfirm.IsEnabled = False

        End If

    End Sub


    ''' <summary>
    ''' Sets or gets the SHA-256 hash of the new password.  
    ''' </summary>
    ''' 
    Public Property NewPasswordHash As String = Nothing


    ''' <summary>
    ''' Sets or gets the hint set for the new password.  
    ''' </summary>
    ''' 
    Public Property NewPasswordHint As String = ""


    ''' <summary>
    ''' Sets or gets the SHA-256 hash the password to be modified.  
    ''' </summary>
    ''' 
    Private Property OriginalPWHash As String = Nothing



    Private Sub Me_Rendered() Handles Me.ContentRendered

        If pwBoxCurrent.IsVisible Then
            pwBoxCurrent.Focus()
        Else
            pwBoxNew.Focus()
        End If
        txtHint.Text = ""

    End Sub


    Private Function GetPasswordHash(password As String) As String

        Return ELNCryptography.GetSHA256Hash(password)

    End Function


    Private Sub pwBoxCurrent_KeyUp(ByVal sender As Object, ByVal e As RoutedEventArgs) Handles pwBoxCurrent.KeyUp

        If GetPasswordHash(pwBoxCurrent.Password) <> OriginalPWHash Then

            imgCurrOk.Visibility = Visibility.Collapsed
            btnOK.IsEnabled = False
            pwBoxNew.IsEnabled = False
            pwBoxConfirm.IsEnabled = False

        Else

            pwBoxNew.IsEnabled = True
            pwBoxConfirm.IsEnabled = True
            imgCurrOk.Visibility = Visibility.Visible
            btnOK.IsEnabled = imgConfirmOk.IsVisible

        End If

    End Sub


    Private Sub pwBoxNew_KeyUp(ByVal sender As Object, ByVal e As RoutedEventArgs) Handles pwBoxNew.KeyUp

        If pwBoxNew.Password.Length >= 8 Then

            imgNewOK.Visibility = Visibility.Visible
            pwBoxNew.IsEnabled = True
            pwBoxConfirm.IsEnabled = True

        Else

            imgNewOK.Visibility = Visibility.Collapsed

        End If

    End Sub


    Private Sub confirmPW_KeyUp(ByVal sender As Object, ByVal e As RoutedEventArgs) Handles pwBoxConfirm.KeyUp,
    pwBoxNew.KeyUp

        If pwBoxNew.Password = pwBoxConfirm.Password AndAlso pwBoxNew.Password.Length >= 8 Then

            imgConfirmOk.Visibility = Visibility.Visible
            If imgCurrOk.IsVisible OrElse Not pwBoxCurrent.IsVisible Then
                btnOK.IsEnabled = True
            Else
                btnOK.IsEnabled = False
            End If

        Else

            imgConfirmOk.Visibility = Visibility.Collapsed
            btnOK.IsEnabled = False

        End If

    End Sub


    Private Sub pw_textChanged() Handles pwBoxNew.KeyUp, pwBoxConfirm.KeyUp

        If pwBoxNew.Password = pwBoxConfirm.Password AndAlso pwBoxNew.Password.Length >= 8 Then
            btnOK.IsEnabled = True
        Else
            btnOK.IsEnabled = False
        End If

    End Sub


    Private Sub btnOK_Click() Handles btnOK.Click

        NewPasswordHash = GetPasswordHash(pwBoxNew.Password)
        NewPasswordHint = txtHint.Text

        DialogResult = True
        Me.Close()

    End Sub


    Private Sub btnCancel_Click() Handles btnCancel.Click

        DialogResult = False
        Me.Close()

    End Sub


End Class
