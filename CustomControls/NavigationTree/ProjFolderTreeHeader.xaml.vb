Imports ElnCoreModel
Imports System.Windows
Imports System.Windows.Controls


Public Class ProjFolderTreeHeader

    Public Event RequestAddExperiment(sender As Object, folderEntry As tblProjFolders)
    Public Event RequestDeleteFolder(sender As Object, folderEntry As tblProjFolders)
    Public Event RequestArchiveFolder(sender As Object, folderEntry As tblProjFolders)


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Public Sub BeginTitleEdit()

        lblTitle.BeginEdit()

    End Sub


    Private Sub btnAddExp_Click(sender As Object, e As RoutedEventArgs) Handles btnAddExp.PreviewMouseUp

        RaiseEvent RequestAddExperiment(Me, CType(DataContext, tblProjFolders))

    End Sub


    ''' <summary>
    ''' Check for duplicate group titles
    ''' </summary>
    ''' 
    Private Sub editLabel_RequestValidation(sender As Object, testTitle As String, ByRef isValid As Boolean) Handles lblTitle.RequestValidation

        WPFToolbox.WaitForPriority(Threading.DispatcherPriority.ContextIdle)

        If testTitle = "" Then

            'check for empty project title

            cbMsgBox.Display("Please enter a group title!", MsgBoxStyle.OkOnly + MsgBoxStyle.Exclamation, "Validation")
            isValid = False

        Else

            'check for duplicates

            Dim folderEntry = TryCast(DataContext, tblProjFolders)
            If folderEntry IsNot Nothing Then

                testTitle = Trim(testTitle.ToLower)

                Dim duplicates = From folder In folderEntry.Project.tblProjFolders Where
                    Trim(folder.FolderName).Equals(testTitle, StringComparison.OrdinalIgnoreCase)

                If duplicates.Count > 1 Then
                    cbMsgBox.Display("This group title already exists in the current." + vbCrLf +
                           "project. Please choose another one ...", MsgBoxStyle.OkOnly + MsgBoxStyle.Exclamation, "Validation")
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

        Dim folderEntry = CType(DataContext, tblProjFolders)

        'don't open context menu if TreeViewItem is collapsed
        Dim tvItem = WPFToolbox.FindVisualParent(Of TreeViewItem)(Me)
        If Not tvItem.IsExpanded Then
            e.Handled = True
            Exit Sub
        End If

        'disable delete project if it contains experiments
        mnuDeleteFolder.IsEnabled = (folderEntry.tblExperiments.Count = 0) AndAlso (folderEntry.Project.tblProjFolders.Count > 1)

        'disble archive project if it contains no experiments
        mnuArchiveFolder.IsEnabled = (folderEntry.tblExperiments.Count > 0)

    End Sub


    Private Sub mnuDeleteFolder_Click(sender As Object, e As RoutedEventArgs) Handles mnuDeleteFolder.Click

        lblTitle.EditText = Space(12)  'dummy to suppress empty title validation blocking after delete
        lblTitle.EndEdit()

        Dim folderEntry = CType(DataContext, tblProjFolders)
        RaiseEvent RequestDeleteFolder(Me, folderEntry)

    End Sub


    Private Sub mnuArchiveFolder_Click(sender As Object, e As RoutedEventArgs) Handles mnuArchiveFolder.Click

        Dim folderEntry = CType(DataContext, tblProjFolders)
        RaiseEvent RequestArchiveFolder(Me, folderEntry)

    End Sub

End Class
