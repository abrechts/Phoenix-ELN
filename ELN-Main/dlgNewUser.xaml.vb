Imports System.Text.RegularExpressions
Imports ElnCoreModel

Public Class dlgNewUser

    ''' <summary>
    ''' Creates a wizard for entering new user data, or for editing current info.
    ''' </summary>
    ''' <param name="localContext">The ElnDataContext of the local SQLite database (for checking local user ID localDuplicates).</param>
    ''' <param name="serverContext">The ElnDataContext of the MySQL server database (for checking server user ID localDuplicates. 
    ''' Set to nothing, if no server present.</param>

    Public Sub New(localContext As ElnDataContext, serverContext As ElnDataContext)

        InitializeComponent()

        DbContext = localContext
        ServerDBContext = serverContext

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        txtUserID.Focus()

        If CurrentUser IsNot Nothing Then

            tabSettings.IsSelected = True
            btnClose.Content = "OK"

            With CurrentUser
                txtFirstName.Text = .FirstName
                txtLastName.Text = .LastName
                txtOrganization.Text = .CompanyName
                txtDepartment.Text = .DepartmentName
                txtSite.Text = .City
            End With

        End If

    End Sub


    ''' <summary>
    ''' Sets or gets the local ElnDataContext
    ''' </summary>
    ''' 
    Public Property DbContext As ElnDataContext


    ''' <summary>
    ''' Sets or gets the ElnDataContext of the MaSql server. Nothing, if no server present.
    ''' </summary>
    ''' 
    Public Property ServerDBContext As ElnDataContext


    ''' <summary>
    ''' Sets or gets the user data to be edited. If nothing, the creation of a new user is assumed.
    ''' instead of creating a new user.
    ''' </summary>
    ''' 
    Public Property CurrentUser As tblUsers = Nothing


    Private Sub Me_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown

        If e.Key = Key.Escape Then
            Me.Close()
        End If

    End Sub


    ' User-ID tab
    '----------------
    Private Sub Me_ContentRendered() Handles Me.ContentRendered

        txtUserID.Focus()
        txtUserID.SelectAll()

    End Sub


    ''' <summary>
    ''' Prevent special character input (only digits and letters allowed)
    ''' </summary>
    ''' 
    Private Sub txtUSerID_PreviewTxtInput(sender As Object, e As TextCompositionEventArgs) Handles txtUserID.PreviewTextInput

        Dim myRegex = New Regex("[^a-zA-Z0-9]+")
        e.Handled = myRegex.IsMatch(e.Text)

    End Sub


    ''' <summary>
    ''' Limit userID length
    ''' </summary>
    ''' 
    Private Sub txtUserID_TextChanged(ByVal sender As Object, ByVal e As TextChangedEventArgs) Handles txtUserID.TextChanged

        With txtUserID
            If .Text.Length > 10 Then
                MsgBox("The max. length of a user-ID is 10 characters!", MsgBoxStyle.Exclamation, "UserID")
                .Text = .Text.Substring(0, .Text.Length - 1)
            End If
        End With

        btnContinue.IsEnabled = (txtUserID.Text <> "") AndAlso txtUserID.Text.Length > 3

    End Sub


    ''' <summary>
    ''' Checks for username localDuplicates and proceeds to the next tab if ok.
    ''' </summary>
    '''
    Private Sub btnContinue_Click(ByVal sender As Object, ByVal e As RoutedEventArgs) Handles btnContinue.Click

        ' prevents creating a user-created demo user

        If LCase(txtUserID.Text) = "demo" Then
            MsgBox("Sorry, the userID 'demo' cannot be used!", MsgBoxStyle.Information + MsgBoxStyle.OkOnly, "UserID Check")
            txtUserID.Focus()
            txtUserID.SelectAll()
            Exit Sub
        End If

        ' check for userID duplicates

        Dim localDuplicates = From user In DbContext.tblUsers Where user.UserID.ToLower = txtUserID.Text.ToLower
        Dim serverDuplicates As New List(Of tblUsers)
        If ServerDBContext IsNot Nothing Then
            serverDuplicates = (From user In ServerDBContext.tblUsers Where user.UserID.ToLower = txtUserID.Text.ToLower).ToList
        End If

        If Not localDuplicates.Any AndAlso Not serverDuplicates.Any Then

            blkUserID.Text = txtUserID.Text
            blkExample.Text = blkUserID.Text + "-00001"
            generalTab.SelectedIndex = 1

        Else

            If localDuplicates.Any Then
                MsgBox("Sorry, this userID already exists on your machine." + vbCrLf +
                       "Please specify another one!", MsgBoxStyle.Information + MsgBoxStyle.OkOnly, "Duplicate")
            Else
                MsgBox("Sorry, this userID already exists on the." + vbCrLf +
                       "ELN server. Please specify another one!", MsgBoxStyle.Information + MsgBoxStyle.OkOnly, "Duplicate")
            End If

            txtUserID.Focus()
            txtUserID.SelectAll()
            Exit Sub

        End If

    End Sub


    'Confirm tab
    '--------------

    Private Sub btnBack_Click() Handles btnBack.Click

        generalTab.SelectedIndex = 0

    End Sub


    Private Sub btnAccept_Click(ByVal sender As Object, ByVal e As RoutedEventArgs) Handles btnAccept.Click

        generalTab.SelectedIndex = 2

    End Sub


    ' User settings tab
    ' -------------------

    Private Sub SettingsTxt_TextChanged() Handles txtFirstName.TextChanged, txtLastName.TextChanged, txtDepartment.TextChanged,
        txtSite.TextChanged, txtOrganization.TextChanged

        If txtFirstName.Text = "" OrElse txtLastName.Text = "" OrElse txtSite.Text = "" OrElse txtDepartment.Text = "" _
         OrElse txtOrganization.Text = "" Then
            btnClose.IsEnabled = False
        Else
            btnClose.IsEnabled = True
        End If

    End Sub


    Private Sub btnCancel_Click(ByVal sender As System.Object, ByVal e As System.Windows.RoutedEventArgs) Handles btnCancel.Click

        Me.DialogResult = False
        Me.Close()

    End Sub

    Private Sub btnClose_Click(ByVal sender As Object, ByVal e As RoutedEventArgs) Handles btnClose.Click

        Me.DialogResult = True
        Me.Close()

    End Sub


End Class
