Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Threading
Imports ElnCoreModel
Imports GongSolutions.Wpf.DragDrop


Public Class ExperimentTree

    Public Event ExperimentSelected(sender As Object, selectedExperiment As tblExperiments)

    Public Event RequestAddExperiment(sender As Object, projFolderEntry As tblProjFolders)


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Private Sub projectHeader_RequestDeleteProject(sender As Object, projectEntry As tblProjects)

        DeleteProject(projectEntry)

    End Sub


    Private Sub projectHeader_RequestAddFolder(sender As Object, projectEntry As tblProjects)

        AddExpGroup(projectEntry)

    End Sub


    Private Sub folderHeader_RequestDeleteFolder(sender As Object, folderEntry As tblProjFolders)

        DeleteExpGroup(folderEntry)

    End Sub


    Private Sub folderHeader_RequestAddExperiment(sender As Object, projFolderEntry As tblProjFolders)

        RaiseEvent RequestAddExperiment(Me, projFolderEntry)

    End Sub




    ''' <summary>
    ''' Adds a new project.
    ''' </summary>
    ''' 
    Public Function AddProject() As tblProjects

        Dim currUser = CType(Me.DataContext, tblUsers)
        Dim newSequenceNr = GetMaxProjectSequenceNr(currUser) + 1

        Dim newProject As New tblProjects
        With newProject
            .GUID = Guid.NewGuid.ToString("D")
            .User = currUser
            .Title = "" '"Project " + newSequenceNr.ToString
            .IsNodeExpanded = 1
            .SequenceNr = newSequenceNr
        End With

        currUser.tblProjects.Add(newProject)

        ProjectFolders.Add(newProject, ProjectFolders.DefaultFolderTitle, ExperimentContent.DbContext)

        Dim projectConv As ProjectsCollectionViewConverter = FindResource("projectsCollectionViewConv")
        projectConv.View.Refresh()   'navTree.Items.Refresh() does not work here
        navTree.UpdateLayout()

        'Set project tree title to edit mode
        Dim projHeader = WPFToolbox.FindVisualChild(Of ProjectTreeHeader)(Me)
        projHeader?.BeginTitleEdit()

        ExperimentContent.DbContext.SaveChanges()

        Return newProject

    End Function


    ''' <summary>
    ''' Adds a new experiment group subfolder to the project containing the current experiment. 
    ''' </summary>
    ''' 
    Public Sub AddExpGroup(Optional currProject As tblProjects = Nothing)

        '  parent project not specified: get it from currently selected experiment

        If currProject Is Nothing Then

            Dim currUser = CType(Me.DataContext, tblUsers)
            Dim currExp = (From exp In currUser.tblExperiments Where exp.IsCurrent = 1).FirstOrDefault

            If currExp IsNot Nothing Then
                currProject = currExp.Project
            Else
                Exit Sub
            End If

        End If

        Dim newFolder = ProjectFolders.Add(currProject, "", ExperimentContent.DbContext)

        RefreshItems()

        Dim projTvi As TreeViewItem = navTree.ItemContainerGenerator.ContainerFromItem(currProject)
        navTree.UpdateLayout()

        Dim projFolderHeader = WPFToolbox.FindVisualChild(Of ProjFolderTreeHeader)(projTvi) 'takes first one encountered
        projFolderHeader?.BeginTitleEdit()

        ExperimentContent.DbContext.SaveChanges()


    End Sub


    ''' <summary>
    ''' Deletes the specified project if it contains no experiments.
    ''' </summary>
    ''' 
    Private Sub DeleteProject(projectEntry As tblProjects)

        If projectEntry.tblExperiments.Count = 0 Then

            projectEntry.User.tblProjects.Remove(projectEntry)
            UpdateProjectSequenceNumbers()

            Dim projectConv As ProjectsCollectionViewConverter = FindResource("projectsCollectionViewConv")
            projectConv.View.Refresh() 'navTree.Items.Refresh() does not work here for some reason

            ExperimentContent.DbContext.SaveChanges()

        End If

    End Sub


    ''' <summary>
    ''' Deletes the specified project folder
    ''' </summary>
    ''' 
    Private Sub DeleteExpGroup(groupEntry As tblProjFolders)

        If groupEntry.tblExperiments.Count = 0 AndAlso groupEntry.Project.tblProjFolders.Count > 1 Then

            groupEntry.Project.tblProjFolders.Remove(groupEntry)

            UpdateFolderSequenceNumbers(groupEntry.Project)
            navTree.Items.Refresh()

            ExperimentContent.DbContext.SaveChanges()

        End If

    End Sub


    Public Sub RefreshItems()

        navTree.Items.Refresh()

    End Sub


    ''' <summary>
    ''' Gets the currently highest project sequence number of the current user.  
    ''' </summary>
    ''' <returns>Highest sequence number, or -1 if no projects present so far.</returns>
    ''' 
    Private Function GetMaxProjectSequenceNr(userEntry As tblUsers) As Integer

        If userEntry.tblProjects.Count > 0 Then
            Return Aggregate proj In userEntry.tblProjects Into Max(proj.SequenceNr)
        Else
            Return -1   'first item
        End If

    End Function


    ''' <summary>
    ''' Re-assigns the project item sequence numbers towards a contiguous sequence, e.g. to
    ''' remove sequence numbering gaps after an item deletion.
    ''' </summary>
    ''' 
    Private Sub UpdateProjectSequenceNumbers()

        Dim currUser = CType(Me.DataContext, tblUsers)

        Dim pos = 0
        Dim projItems = From proj In currUser.tblProjects Order By proj.SequenceNr Ascending
        For Each item In projItems
            item.SequenceNr = pos
            pos += 1
        Next

    End Sub


    ''' <summary>
    '''  Re-assigns the folder sequence numbers within the specified project towards a contiguous sequence
    ''' </summary>
    '''
    Private Sub UpdateFolderSequenceNumbers(parentProject As tblProjects)

        Dim pos = 0
        Dim folderItems = From folder In parentProject.tblProjFolders Order By folder.SequenceNr Ascending
        For Each item In folderItems
            item.SequenceNr = pos
            pos += 1
        Next

    End Sub


    ''' <summary>
    ''' Opens the TreeView path to the TreeViewItem containing the specified tblExperiments and selects it.
    ''' </summary>
    ''' <param name="expEntry">The target tblExperiments entity.</param>
    ''' <param name="populateContent">Optional: Set to false if the experiment navigation 
    ''' tree node is to be selected only, without populating the experiment content. Typical use 
    ''' is to select a pinned experiment already present in the experiment tab. </param>
    ''' 
    Public Sub SelectExperiment(expEntry As tblExperiments, Optional populateContent As Boolean = True)

        If expEntry IsNot Nothing Then

            'unselect current user experiment
            Dim selExp = From exp In expEntry.User.tblExperiments Where exp.IsCurrent = 1
            For Each exp In selExp
                exp.IsCurrent = 0
            Next

            'expand parent project node
            If expEntry.Project.IsNodeExpanded = 0 Then
                expEntry.Project.IsNodeExpanded = 1
                WPFToolbox.WaitForPriority(DispatcherPriority.ContextIdle)  'wait for node to expand
            End If

            'select experiment child node, which is subsequently scrolled into view by TreeViewItem_Selected
            expEntry.IsCurrent = 1

            'workaround: deselects first protocol item, which is auto-selected if originally no selection is present for unknown reasons
            Dim firstItem = (From item In expEntry.tblProtocolItems Order By item.SequenceNr Ascending).FirstOrDefault
            If firstItem IsNot Nothing Then
                firstItem.IsSelected = 0
            End If

            If populateContent Then
                RaiseEvent ExperimentSelected(Me, expEntry)
            End If

            'wait for content to load into tabControl presenter
            WPFToolbox.WaitForPriority(DispatcherPriority.Loaded)

        End If

    End Sub


    Private Sub TreeViewItem_Selected(sender As Object, e As RoutedEventArgs)

        Dim entityObj As Object = e.OriginalSource.header

        If TypeOf entityObj Is tblExperiments Then

            Dim expEntry = CType(entityObj, tblExperiments)

            'unselect current user experiment
            Dim selExp = From exp In expEntry.User.tblExperiments Where exp.IsCurrent = 1
            For Each exp In selExp
                exp.IsCurrent = 0
            Next

            'mark selected experiment
            expEntry.IsCurrent = 1

            CType(e.OriginalSource, TreeViewItem).BringIntoView()
            RaiseEvent ExperimentSelected(Me, expEntry)

        End If

    End Sub


    Private Sub TreeViewItem_Unselected(sender As Object, e As RoutedEventArgs)

    End Sub

End Class



Public Class NavTreeDropHandler

    '-------------------------------------------------------------------------
    ' Drag & Drop logic (Gong: https://github.com/punker76/gong-wpf-dragdrop) 
    ' (implemented with NuGet gong-wpf-dragdrop package)
    '--------------------------------------------------------------------------

    Implements IDropTarget

    Private Sub IDropTarget_DragOver(dropInfo As IDropInfo) Implements IDropTarget.DragOver

        With dropInfo

            If TypeOf .Data Is tblExperiments Then

                ' drag experiment node
                '---------------------

                Dim expFolder = CType(.Data, tblExperiments).ProjFolder

                If .TargetItem IsNot expFolder AndAlso TypeOf .TargetItem Is tblProjFolders Then
                    .Effects = DragDropEffects.Move
                    .DropTargetAdorner = DropTargetAdorners.Highlight
                End If

            ElseIf TypeOf .Data Is tblProjFolders Then

                ' drag project subfolder node
                '----------------------------

                If TypeOf .TargetItem Is tblProjFolders AndAlso .TargetItem IsNot .Data Then

                    'drag over another project node

                    Dim srcFolder = CType(.Data, tblProjFolders)
                    Dim targetFolder = CType(.TargetItem, tblProjFolders)
                    Dim folderCount = targetFolder.Project.tblProjFolders.Count

                    'prevent inserting into another project
                    If targetFolder.Project IsNot srcFolder.Project Then
                        Exit Sub
                    End If

                    ''prevent inserting below itself
                    'If targetFolder.SequenceNr = srcFolder.SequenceNr - 1 Then
                    '    Exit Sub
                    'End If

                    If (.InsertPosition = RelativeInsertPosition.BeforeTargetItem AndAlso (targetFolder.IsNodeExpanded = 1 _
                      OrElse targetFolder.SequenceNr = folderCount - 1)) Then
                        .Effects = DragDropEffects.Move
                        .DropTargetAdorner = DropTargetAdorners.Insert

                    ElseIf .InsertPosition = RelativeInsertPosition.AfterTargetItem AndAlso targetFolder.IsNodeExpanded = 0 Then
                        .Effects = DragDropEffects.Move
                        .DropTargetAdorner = DropTargetAdorners.Insert

                    End If

                ElseIf TypeOf .TargetItem Is tblExperiments Then

                    'drag over last experiment node of last project folder (i.e. for append)

                    Dim targetExp = CType(dropInfo.TargetItem, tblExperiments)
                    Dim lastFolderExp = (From exp In targetExp.ProjFolder.tblExperiments Order By exp.ExperimentID Descending).Last

                    If targetExp.ProjFolder.SequenceNr = 0 AndAlso targetExp Is lastFolderExp Then

                        If .InsertPosition = RelativeInsertPosition.AfterTargetItem Then
                            .DropTargetAdorner = DropTargetAdorners.Insert
                            .Effects = DragDropEffects.Move
                        End If

                    End If

                End If

            ElseIf TypeOf .Data Is tblProjects Then

                ' drag project node
                '-------------------

                If TypeOf .TargetItem Is tblProjects AndAlso .TargetItem IsNot .Data Then

                    ''prevent inserting below itself
                    'If .InsertIndex = .DragInfo.SourceIndex + 1 Then
                    '    .DropTargetAdorner = Nothing
                    '    Exit Sub
                    'End If

                    If .InsertPosition = RelativeInsertPosition.BeforeTargetItem Then
                        .DropTargetAdorner = DropTargetAdorners.Insert
                        .Effects = DragDropEffects.Move
                    End If

                ElseIf .TargetItem Is Nothing Then

                    'allow insertion below expanded last project folder
                    .DropTargetAdorner = DropTargetAdorners.Highlight
                    .Effects = DragDropEffects.Move

                End If

            End If

        End With

    End Sub


    Private Sub IDropTarget_Drop(dropInfo As IDropInfo) Implements IDropTarget.Drop

        Dim navTree = CType(dropInfo.VisualTarget, TreeView)

        If TypeOf dropInfo.Data Is tblExperiments Then

            'drop experiment into new project folder
            '---------------------------------------

            Dim dragExperiment = CType(dropInfo.Data, tblExperiments)
            Dim targetFolder = CType(dropInfo.TargetItem, tblProjFolders)

            dragExperiment.ProjFolder.tblExperiments.Remove(dragExperiment)
            targetFolder.tblExperiments.Add(dragExperiment)
            dragExperiment.Project = targetFolder.Project
            dragExperiment.ProjFolder = targetFolder

            navTree.Items.Refresh()

            'Set auto-save point
            Dim currExpContent = WPFToolbox.FindVisualChild(Of ExperimentContent)(ExperimentContent.TabExperimentsPresenter)
            currExpContent.pnlProtocol.AutoSave(allowFinalized:=True)


        ElseIf TypeOf dropInfo.Data Is tblProjFolders Then 'AndAlso TypeOf dropInfo.TargetItem Is tblProjFolders Then

            'drop project subfolder
            '----------------------

            Dim dragFolder = CType(dropInfo.Data, tblProjFolders)

            If TypeOf dropInfo.TargetItem Is tblProjFolders Then

                'insert: update sequence numbers

                Dim targetFolder = CType(dropInfo.TargetItem, tblProjFolders)
                Dim origPos = dragFolder.SequenceNr
                Dim insertPos = targetFolder.SequenceNr
                Dim folderCount = targetFolder.Project.tblProjFolders.Count

                If targetFolder.IsNodeExpanded = 0 AndAlso (targetFolder.SequenceNr < folderCount - 1) Then
                    insertPos -= 1
                End If

                If origPos > insertPos Then
                    insertPos += 1
                End If

                For Each folder In dragFolder.Project.tblProjFolders   'remove item
                    If folder.SequenceNr > origPos Then
                        folder.SequenceNr -= 1
                    End If
                Next

                For Each folder In dragFolder.Project.tblProjFolders  'insert item
                    If folder.SequenceNr >= insertPos Then
                        folder.SequenceNr += 1
                    End If
                Next

                dragFolder.SequenceNr = insertPos

            ElseIf TypeOf dropInfo.TargetItem Is tblExperiments Then

                ' append to the end of the collection if last node expanded

                For Each folder In dragFolder.Project.tblProjFolders
                    If folder.SequenceNr < dragFolder.SequenceNr Then
                        folder.SequenceNr += 1
                    End If
                Next
                dragFolder.SequenceNr = 0 ' bottom position

            End If

            navTree.Items.Refresh()

            ExperimentContent.DbContext.SaveChanges()   'auto-save only possible on experiment level


        ElseIf TypeOf dropInfo.Data Is tblProjects Then

            'drop project
            '------------

            Dim dragProject = CType(dropInfo.Data, tblProjects)
            Dim origPos = dragProject.SequenceNr

            If TypeOf dropInfo.TargetItem Is tblProjects Then

                'insert: update sequence numbers

                Dim targetProject = CType(dropInfo.TargetItem, tblProjects)
                Dim insertPos = targetProject.SequenceNr

                If origPos > insertPos AndAlso dropInfo.InsertPosition = RelativeInsertPosition.BeforeTargetItem Then
                    insertPos += 1
                End If

                For Each proj In dragProject.User.tblProjects   'remove item
                    If proj.SequenceNr > origPos Then
                        proj.SequenceNr -= 1
                    End If
                Next

                For Each proj In dragProject.User.tblProjects   'insert item
                    If proj.SequenceNr >= insertPos Then
                        proj.SequenceNr += 1
                    End If
                Next

                dragProject.SequenceNr = insertPos  'assign item


            ElseIf dropInfo.TargetItem Is Nothing Then

                ' append to the end of the collection

                For Each proj In dragProject.User.tblProjects
                    If proj.SequenceNr < dragProject.SequenceNr Then
                        proj.SequenceNr += 1
                    End If
                Next

                dragProject.SequenceNr = 0 ' bottom position

            End If

            Dim projectConv As ProjectsCollectionViewConverter = navTree.FindResource("projectsCollectionViewConv")
            projectConv.View.Refresh()

            ExperimentContent.DbContext.SaveChanges()   'auto-save only possible on experiment level

        End If

    End Sub


    Private Sub IDropTarget_DragEnter(dropInfo As IDropInfo) Implements IDropTarget.DragEnter


    End Sub


    Private Sub IDropTarget_DragLeave(dropInfo As IDropInfo) Implements IDropTarget.DragLeave


    End Sub


    Public Sub DropHint(dropHintInfo As IDropHintInfo) Implements IDropTarget.DropHint

    End Sub

End Class