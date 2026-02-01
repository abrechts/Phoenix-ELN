Imports ElnCoreModel
Imports System.Windows
Imports System.Windows.Controls

Public Class ProjectTreeHeader

    Public Event RequestAddExperiment(sender As Object, projectEntry As tblProjects)

    Public Event RequestAddFolder(sender As Object, projectEntry As tblProjects)

    Public Event RequestDeleteProject(sender As Object, projectEntry As tblProjects)


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Public Sub BeginTitleEdit()

        lblTitle.BeginEdit()

    End Sub


    Private Sub editLabel_RequestValidation(sender As Object, testTitle As String, ByRef isValid As Boolean) Handles lblTitle.RequestValidation

        'Check for duplicate project titles and reset to original unique one if duplicate 

        WPFToolbox.WaitForPriority(Threading.DispatcherPriority.ContextIdle)

        If testTitle = "" Then

            'check for empty project title

            cbMsgBox.Display("Please enter a project title!", MsgBoxStyle.OkOnly + MsgBoxStyle.Exclamation, "Validation")
            isValid = False

        Else

            'check for duplicates

            Dim projectEntry = TryCast(DataContext, tblProjects)
            If projectEntry IsNot Nothing Then

                testTitle = Trim(testTitle.ToLower)

                Dim duplicates = From proj In projectEntry.User.tblProjects Where Trim(proj.Title.ToLower) = testTitle
                If duplicates.Count > 1 Then
                    cbMsgBox.Display("A duplicate project name was entered." + vbCrLf +
                                "Please enter another title ...", MsgBoxStyle.OkOnly + MsgBoxStyle.Exclamation, "Validation")
                    isValid = False
                Else
                    isValid = True
                End If

            Else

                isValid = False

            End If

        End If

    End Sub


    Private Sub Me_ContextMenuOpening(sender As Object, e As RoutedEventArgs) Handles Me.ContextMenuOpening

        Dim projectEntry = CType(DataContext, tblProjects)

        'don't open context menu if TreeViewItem is collapsed
        Dim tvItem = WPFToolbox.FindVisualParent(Of TreeViewItem)(Me)
        If Not tvItem.IsExpanded Then
            e.Handled = True
            Exit Sub
        End If

        'disable delete project if it contains experiments
        mnuDeleteProject.IsEnabled = (projectEntry.tblExperiments.Count = 0)

        'disable folder addition if project has no title
        mnuAddSubProject.IsEnabled = (projectEntry.Title <> "")

    End Sub


    Private Sub mnuAddSubProject_Click() Handles mnuAddSubProject.Click

        Dim projectEntry = CType(DataContext, tblProjects)
        RaiseEvent RequestAddFolder(Me, projectEntry)

    End Sub


    Private Sub mnuClearProject_Click(sender As Object, e As RoutedEventArgs) Handles mnuDeleteProject.Click

        lblTitle.EditText = Space(12)  'dummy to suppress empty title validation blocking after delete
        lblTitle.EndEdit()

        Dim projectEntry = CType(DataContext, tblProjects)
        RaiseEvent RequestDeleteProject(Me, projectEntry)

    End Sub

End Class
