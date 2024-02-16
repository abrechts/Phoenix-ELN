Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Threading
Imports ElnBase
Imports ElnCoreModel
Imports GongSolutions.Wpf.DragDrop

Public Class ExperimentTree

    Public Event ExperimentSelected(sender As Object, selectedExperiment As tblExperiments)

    Public Event RequestAddExperiment(sender As Object, projectEntry As tblProjects)


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        'initially selects the currently selected experiment of the last session

        If Me.DataContext IsNot Nothing Then
            If SelectLastSelectedExperiment(CType(Me.DataContext, tblUsers)) Is Nothing Then
                'not available: select first user experiment
                SelectExperiment(CType(Me.DataContext, tblUsers).tblExperiments.Last)
            End If
        End If

    End Sub


    Private Sub projectHeader_RequestAddExperiment(sender As Object, projectEntry As tblProjects)

        RaiseEvent RequestAddExperiment(Me, projectEntry)

    End Sub


    Private Sub projectHeader_RequestDeleteProject(sender As Object, projectEntry As tblProjects)
        DeleteProject(projectEntry)
    End Sub


    ''' <summary>
    ''' Adds a new project.
    ''' </summary>
    ''' <returns>The created tblProjects.</returns>
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

        Dim projectConv As ProjectsCollectionViewConverter = FindResource("projectsCollectionViewConv")
        projectConv.View.Refresh()   'navTree.Items.Refresh() does not work here
        navTree.UpdateLayout()

        'Set project tree title to edit mode
        Dim tvItem As TreeViewItem = navTree.ItemContainerGenerator.ContainerFromItem(newProject)
        Dim projHeader = WPFToolbox.FindVisualChild(Of ProjectTreeHeader)(Me)
        If projHeader IsNot Nothing Then
            projHeader.BeginTitleEdit()
        End If

        Return newProject

    End Function


    ''' <summary>
    ''' Deletes the specified project if it contains no experiments.
    ''' </summary>
    ''' 
    Private Sub DeleteProject(projectEntry As tblProjects)

        If Not projectEntry.tblExperiments.Any Then

            projectEntry.User.tblProjects.Remove(projectEntry)

            Dim projectConv As ProjectsCollectionViewConverter = FindResource("projectsCollectionViewConv")
            projectConv.View.Refresh() 'navTree.Items.Refresh() does not work here for some reason

            UpdateProjectSequenceNumbers()

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
    ''' Opens the TreeView path to the TreeViewItem containing the specified tblExperiments and selects it.
    ''' </summary>
    ''' <param name="expEntry">The target tblExperiments entity.</param>
    ''' <param name="populateContent">Optional: Set to false if the experiment navigation 
    ''' tree node is to be selected only, without populating the experiment content. Typical use 
    ''' is to select a pinned experiment already present in the experiment tab. </param>
    ''' 
    Public Sub SelectExperiment(expEntry As tblExperiments, Optional populateContent As Boolean = True)

        If expEntry IsNot Nothing Then

            'unselect current experiment if present
            If LastCurrentExperiment IsNot Nothing Then
                LastCurrentExperiment.IsCurrent = 0
            End If

            'expand parent project node
            expEntry.Project.IsNodeExpanded = 1
            WPFToolbox.WaitForPriority(DispatcherPriority.ContextIdle)  'wait for node to expand

            'select experiment child node, which is subsequently scrolled into view by TreeViewItem_Selected
            expEntry.IsCurrent = 1

            'workaround: deselects first protocol item, which is auto-selected if originally no selection is present for unknown reasons
            Dim firstItem = (From item In expEntry.tblProtocolItems Order By item.SequenceNr Ascending).FirstOrDefault
            If firstItem IsNot Nothing Then
                firstItem.IsSelected = 0
            End If

            LastCurrentExperiment = expEntry

            If populateContent Then
                RaiseEvent ExperimentSelected(Me, expEntry)
            End If

        End If

    End Sub


    ''' <summary>
    ''' Selects the experiment which was last selected. Typically used during application startup for restoring last state.
    ''' </summary>
    ''' <param name="userEntry">The parent user tblUsers entity.</param>
    ''' <returns>The last selected tblExperiments, or nothing if not available.</returns>
    ''' 
    Public Function SelectLastSelectedExperiment(userEntry As tblUsers) As tblExperiments

        Dim lastExp = (From exp In userEntry.tblExperiments Where exp.IsCurrent = 1).FirstOrDefault

        If lastExp IsNot Nothing Then
            SelectExperiment(lastExp, populateContent:=False)    'exp already populated by databinding
        End If

        Return lastExp

    End Function


    Private Property LastCurrentExperiment As tblExperiments


    Private Sub TreeViewItem_Selected(sender As Object, e As RoutedEventArgs)

        Dim entityObj As Object = e.OriginalSource.header

        If TypeOf entityObj Is tblExperiments Then

            Dim expEntry = CType(entityObj, tblExperiments)
            expEntry.IsCurrent = 1
            If LastCurrentExperiment IsNot Nothing AndAlso LastCurrentExperiment IsNot expEntry Then
                LastCurrentExperiment.IsCurrent = 0
            End If
            LastCurrentExperiment = expEntry

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

            Dim targetProj As tblProjects = Nothing
            If TypeOf .TargetItem Is tblProjects Then
                targetProj = .TargetItem
            ElseIf TypeOf .TargetItem Is tblExperiments Then
                targetProj = CType(.TargetItem, tblExperiments).Project
            End If

            If TypeOf .Data Is tblExperiments Then

                Dim parentProj = CType(.Data, tblExperiments).Project
                If targetProj IsNot parentProj AndAlso TypeOf .TargetItem Is tblProjects Then
                    .Effects = DragDropEffects.Move
                    .DropTargetAdorner = DropTargetAdorners.Highlight
                Else
                    .Effects = DragDropEffects.None
                    .DropTargetAdorner = Nothing
                End If

            ElseIf TypeOf .Data Is tblProjects AndAlso TypeOf .TargetItem IsNot tblExperiments Then

                If targetProj IsNot .Data Then

                    If .InsertIndex = .DragInfo.SourceIndex + 1 Then
                        .DropTargetAdorner = Nothing
                        Exit Sub
                    End If

                    Dim insertAtEnd = .TargetItem Is Nothing AndAlso .DropPosition.Y > 25
                    .Effects = DragDropEffects.Move
                    If TypeOf .TargetItem Is tblProjects OrElse insertAtEnd Then
                        .DropTargetAdorner = DropTargetAdorners.Insert
                    Else
                        .DropTargetAdorner = Nothing
                    End If

                Else
                    .Effects = DragDropEffects.None
                    .DropTargetAdorner = Nothing
                End If

            End If

        End With

    End Sub


    Private Sub IDropTarget_Drop(dropInfo As IDropInfo) Implements IDropTarget.Drop

        Dim navTree = CType(dropInfo.VisualTarget, TreeView)

        If TypeOf dropInfo.Data Is tblExperiments Then

            'drop experiment into new project
            '---------------------------------

            Dim dragExperiment = CType(dropInfo.Data, tblExperiments)
            Dim targetProject As tblProjects
            If TypeOf dropInfo.TargetItem Is tblExperiments Then
                targetProject = CType(dropInfo.TargetItem, tblExperiments).Project
            ElseIf TypeOf dropInfo.TargetItem Is tblProjects Then
                targetProject = CType(dropInfo.TargetItem, tblProjects)
            Else
                Exit Sub
            End If

            dragExperiment.Project.tblExperiments.Remove(dragExperiment)
            targetProject.tblExperiments.Add(dragExperiment)
            dragExperiment.Project = targetProject

            navTree.Items.Refresh()


        ElseIf TypeOf dropInfo.Data Is tblProjects Then

            'move project folder
            '-------------------

            Dim isAboveFirst As Boolean = False
            Dim dragProject = CType(dropInfo.Data, tblProjects)
            Dim targetProject As tblProjects

            If TypeOf dropInfo.TargetItem Is tblProjects Then
                targetProject = CType(dropInfo.TargetItem, tblProjects)

            ElseIf TypeOf dropInfo.TargetItem Is tblExperiments Then
                targetProject = CType(dropInfo.TargetItem, tblExperiments).Project

            ElseIf dropInfo.TargetItem Is Nothing Then
                'project was dropped below last TreeViewItem
                targetProject = Nothing
                If dropInfo.DropPosition.Y < 25 Then
                    isAboveFirst = True
                End If
            Else
                Exit Sub
            End If

            If targetProject IsNot Nothing Then

                'insert: update sequence numbers

                Dim origPos = dragProject.SequenceNr
                Dim insertPos = targetProject.SequenceNr

                If origPos > insertPos Then
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

            Else

                'dropped into empty space on top or below TreeViewItems

                If isAboveFirst Then

                    'insert above first
                    For Each proj In dragProject.User.tblProjects   'insert item
                        If proj.SequenceNr > dragProject.SequenceNr Then
                            proj.SequenceNr -= 1
                        End If
                    Next
                    dragProject.SequenceNr = dragProject.User.tblProjects.Count - 1

                Else

                    'insert below last
                    For Each proj In dragProject.User.tblProjects   'insert item
                        If proj.SequenceNr < dragProject.SequenceNr Then
                            proj.SequenceNr += 1
                        End If
                    Next
                    dragProject.SequenceNr = 0

                End If

            End If

            Dim projectConv As ProjectsCollectionViewConverter = navTree.FindResource("projectsCollectionViewConv")
            projectConv.View.Refresh()

        End If

    End Sub


    Private Sub IDropTarget_DragEnter(dropInfo As IDropInfo) Implements IDropTarget.DragEnter


    End Sub


    Private Sub IDropTarget_DragLeave(dropInfo As IDropInfo) Implements IDropTarget.DragLeave

    End Sub

End Class