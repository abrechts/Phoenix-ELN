Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Threading
Imports ElnCoreModel
Imports GongSolutions.Wpf.DragDrop


Public Class ExperimentTree

    Public Event ExperimentSelected(sender As Object, selectedExperiment As tblExperiments)

    Public Event RequestAddExperiment(sender As Object, projFolderEntry As tblProjFolders)

    ' Raised instead of scrolling directly, since "the currently displayed experiment" is
    ' MainWindow-level state (which tab is open) that ExperimentTree has no access to.

    Public Event RequestLocateCurrentExperiment(sender As Object)

    ' Raised instead of handling the switch directly, since reacting to a user change
    ' (ApplyAllDataBindings etc.) is MainWindow-level state well beyond the tree itself.

    Public Event SelectedUserChanged(sender As Object, newUser As tblUsers)


    ' Prevents TreeViewItem_Selected from raising a redundant, reentrant ExperimentSelected event
    ' when SelectExperiment already raises it explicitly (see ScrollExperimentIntoView).

    Private _suppressTreeSelectedEvent As Boolean = False


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Private Sub btnAddProject_Click(sender As Object, e As RoutedEventArgs)

        AddProject()

    End Sub


    Private Sub btnAddFolder_Click(sender As Object, e As RoutedEventArgs)

        AddExpGroup()

    End Sub


    Private Sub btnSortProjects_Click(sender As Object, e As RoutedEventArgs)

        Dim projectConv As ProjectsCollectionViewConverter = FindResource("projectsCollectionViewConv")
        projectConv.SetSortMode(btnSortProjects.IsChecked = True)

    End Sub


    Private Sub btnNavMenu_Click(sender As Object, e As RoutedEventArgs)

        If Not navMenuPopup.IsOpen Then RefreshNavMenuSkin()
        navMenuPopup.IsOpen = Not navMenuPopup.IsOpen

    End Sub


    ' Popup content is logically disconnected from the tree that ApplySkin's resource-dictionary
    ' swap invalidates, so every DynamicResource binding inside navMenuPopup freezes at whichever
    ' skin was active when the popup was first realized. Re-applying SetResourceReference forces
    ' each one to look up the currently active skin fresh. Add new menu items' brushes/icons here too.

    Private Sub RefreshNavMenuSkin()

        navMenuBorder.SetResourceReference(Border.BackgroundProperty, "InnerPanelBackground")
        navMenuBorder.SetResourceReference(Border.BorderBrushProperty, "TreePanelBorder")

        ' Foreground is refreshed by reapplying the Style rather than SetResourceReference: a local
        ' value permanently outranks Style triggers, which would silence NavToolbarTextButtonStyle's
        ' dark+hover MultiDataTrigger (Foreground -> Black, needed since the hover highlight itself
        ' stays light-colored in dark mode). Reapplying the Style re-evaluates its own DynamicResource
        ' Setter fresh while leaving the trigger free to still override it on hover.
        RefreshButtonStyle(btnCollapseAll)
        RefreshButtonStyle(btnExpandAll)
        RefreshButtonStyle(btnLocateExperiment)
        RefreshButtonStyle(btnFocusExperiment)

        iconCollapseAll.SetResourceReference(DarkModeHelper.BaseContentProperty, "CollapseAllIcon")
        iconExpandAll.SetResourceReference(DarkModeHelper.BaseContentProperty, "ExpandAllIcon")
        iconLocateExperiment.SetResourceReference(DarkModeHelper.BaseContentProperty, "LocateExperimentIcon")
        iconFocusExperiment.SetResourceReference(DarkModeHelper.BaseContentProperty, "FocusExperimentIcon")

    End Sub


    Private Sub RefreshButtonStyle(btn As Button)

        Dim navTextButtonStyle = TryCast(Me.TryFindResource("NavToolbarTextButtonStyle"), Style)
        btn.Style = Nothing
        btn.Style = navTextButtonStyle

    End Sub


    Private Sub btnCollapseAll_Click(sender As Object, e As RoutedEventArgs)

        navMenuPopup.IsOpen = False
        CollapseExperiments()

    End Sub


    Private Sub btnLocateExperiment_Click(sender As Object, e As RoutedEventArgs)

        navMenuPopup.IsOpen = False
        RaiseEvent RequestLocateCurrentExperiment(Me)

    End Sub


    Private Sub btnFocusExperiment_Click(sender As Object, e As RoutedEventArgs)

        navMenuPopup.IsOpen = False
        CollapseExperiments()
        RaiseEvent RequestLocateCurrentExperiment(Me)

    End Sub


    Private Sub btnExpandAll_Click(sender As Object, e As RoutedEventArgs)

        navMenuPopup.IsOpen = False
        ExpandExperiments()

    End Sub


    Private Sub cboUsers_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)

        If cboUsers.SelectedItem IsNot Nothing Then
            RaiseEvent SelectedUserChanged(Me, CType(cboUsers.SelectedItem, tblUsers))
        End If

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


    Private Sub folderHeader_RequestArchiveFolder(sender As Object, folderEntry As tblProjFolders)

        ArchiveExpGroup(folderEntry)

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

        'Set project tree title to edit mode. Look up newProject's own container specifically (rather than
        'grabbing the first ProjectTreeHeader found anywhere under Me) and realize it first if needed - under
        'virtualization, a blind visual-tree search could otherwise find the wrong (or no) header depending on
        'scroll position.
        Dim newProjTvi = GetOrRealizeContainer(navTree, newProject)
        Dim projHeader = If(newProjTvi IsNot Nothing, WPFToolbox.FindVisualChild(Of ProjectTreeHeader)(newProjTvi), Nothing)
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

        'realize currProject's container first (rather than a raw ContainerFromItem lookup) in case it's
        'currently scrolled out of view and virtualized away
        Dim projTvi As TreeViewItem = GetOrRealizeContainer(navTree, currProject)
        navTree.UpdateLayout()

        Dim projFolderHeader = If(projTvi IsNot Nothing, WPFToolbox.FindVisualChild(Of ProjFolderTreeHeader)(projTvi), Nothing) 'takes first one encountered
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


    Private Sub ArchiveExpGroup(groupEntry As tblProjFolders)

        ' file save dialog
        Dim saveDlg As New Microsoft.Win32.SaveFileDialog With {
            .Title = "Select destination ZIP archive file ...",
            .Filter = "ZIP Archive (*.zip)|*.zip",
            .FileName = groupEntry.Project.Title + "_" + groupEntry.FolderName + ".zip",
            .DefaultExt = ".zip",
            .InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        }

        ' archival progress dialog
        With saveDlg
            If .ShowDialog(Application.Current.MainWindow) Then
                Dim destPath = .FileName
                Dim expList = (From exp In groupEntry.tblExperiments Order By exp.ExperimentID Ascending).ToList
                Dim zipDlg As New dlgZipProgress With {
                    .Owner = Application.Current.MainWindow,
                    .Title = "Archiving Group Experiments ...",
                    .ExperimentEntries = expList,
                    .ZipFilePath = destPath
                }
                zipDlg.ShowDialog()     'dialog contains archival logic
            End If
        End With

    End Sub


    Public Sub RefreshItems()

        navTree.Items.Refresh()

    End Sub


    Public Sub CollapseExperiments()

        Dim currUser = CType(Me.DataContext, tblUsers)

        For Each proj In currUser.tblProjects
            For Each folder In proj.tblProjFolders
                folder.IsNodeExpanded = 0
            Next
        Next

    End Sub


    Public Sub ExpandExperiments()

        Dim currUser = CType(Me.DataContext, tblUsers)

        For Each proj In currUser.tblProjects
            proj.IsNodeExpanded = 1
            For Each folder In proj.tblProjFolders
                folder.IsNodeExpanded = 1
            Next
        Next

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

            'The experiment node may currently be in another scrolling area of the navigation tree,
            'so scroll to it if necessary
            ScrollExperimentIntoView(expEntry)

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


    ''' <summary>
    ''' Selects the TreeViewItem of the specified expEntry, scrolling it into view first if it isn't currently
    ''' visible (e.g. realized-but-scrolled-away, or not yet realized at all because it's virtualized out of view).
    ''' </summary>
    ''' <returns>The TreeViewItem representing the specified expEntry.</returns>
    '''
    Public Function ScrollExperimentIntoView(expEntry As tblExperiments) As TreeViewItem

        Dim selTvi As TreeViewItem = TreeViewItemFromData(navTree, expEntry)

        If selTvi IsNot Nothing Then

            'raises RequestBringIntoView, which bubbles up through every ancestor (including nested virtualizing
            'panels) and scrolls each one by exactly the minimum amount needed - unlike the old ScrollToBottom()
            'fallback, this actually lands on the item regardless of how far away it currently is, but leaves it
            'flush against whichever edge of the viewport it approached from.
            selTvi.BringIntoView()
            NudgeTowardsViewportCenter(selTvi)

            _suppressTreeSelectedEvent = True
            Try
                selTvi.IsSelected = True
            Finally
                _suppressTreeSelectedEvent = False
            End Try

        End If

        Return selTvi

    End Function


    ''' <summary>
    ''' Nudges navTree's scroll position, in whole item rows, so the given (already brought-into-view)
    ''' TreeViewItem ends up closer to the vertical middle of the viewport instead of flush against whichever
    ''' edge BringIntoView happened to land it on - purely cosmetic, so it's skipped if there isn't enough
    ''' surrounding content to make a difference.
    ''' </summary>
    ''' <remarks>
    ''' Works in on-screen pixels to measure direction/distance, then issues that many ScrollViewer.LineUp/
    ''' LineDown calls rather than computing a VerticalOffset directly - navTree's virtualizing panels default
    ''' to VirtualizingPanel.ScrollUnit=Item, under which VerticalOffset/ViewportHeight are in logical item
    ''' units, not pixels, so LineUp/LineDown (which scroll by exactly one item under that mode) is the only
    ''' unit-safe way to move a specific number of rows.
    ''' </remarks>
    '''
    Private Sub NudgeTowardsViewportCenter(tvi As TreeViewItem)

        Dim scroller = WPFToolbox.FindVisualChild(Of ScrollViewer)(navTree)
        If scroller Is Nothing OrElse scroller.ActualHeight <= 0 Then Exit Sub

        navTree.UpdateLayout() 'ensure tvi's position reflects the scroll BringIntoView just performed

        Dim itemTop = tvi.TransformToAncestor(scroller).Transform(New Point(0, 0)).Y
        Dim itemCenter = itemTop + tvi.ActualHeight / 2
        Dim viewportCenter = scroller.ActualHeight / 2
        Dim rowHeight = Math.Max(tvi.ActualHeight, 1)

        Dim rowsToScroll = CInt(Math.Round((itemCenter - viewportCenter) / rowHeight))

        For i = 1 To Math.Abs(rowsToScroll)
            If rowsToScroll > 0 Then
                scroller.LineDown()
            Else
                scroller.LineUp()
            End If
        Next

    End Sub



    Private Sub TreeViewItem_Selected(sender As Object, e As RoutedEventArgs)

        If _suppressTreeSelectedEvent Then Exit Sub

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



    ''' <summary>
    ''' Gets the TreeViewItem container representing the specified experiment. Rather than a blind tree search,
    ''' this walks directly down the known Project -> ProjFolder -> Experiment path (available directly off
    ''' expEntry), realizing each ancestor container in turn via <see cref="GetOrRealizeContainer"/> if the
    ''' virtualizing panel hasn't generated it yet because it's currently scrolled out of view. This never needs
    ''' to realize unrelated sibling projects/folders/experiments just to search past them.
    ''' </summary>
    '''
    Private Function TreeViewItemFromData(tree As TreeView, expEntry As tblExperiments) As TreeViewItem

        Dim projectContainer = GetOrRealizeContainer(tree, expEntry.Project)
        If projectContainer Is Nothing Then Return Nothing

        projectContainer.IsExpanded = True
        projectContainer.UpdateLayout()

        Dim folderContainer = GetOrRealizeContainer(projectContainer, expEntry.ProjFolder)
        If folderContainer Is Nothing Then Return Nothing

        folderContainer.IsExpanded = True
        folderContainer.UpdateLayout()

        Return GetOrRealizeContainer(folderContainer, expEntry)

    End Function


    ''' <summary>
    ''' Gets the TreeViewItem container for the given data item within the given ItemsControl (navTree itself,
    ''' or an already-realized parent TreeViewItem), realizing it first if the virtualizing panel hasn't
    ''' generated it yet - e.g. because the item is currently scrolled out of view.
    ''' </summary>
    '''
    Private Function GetOrRealizeContainer(itemsControl As ItemsControl, data As Object) As TreeViewItem

        Dim container = TryCast(itemsControl.ItemContainerGenerator.ContainerFromItem(data), TreeViewItem)
        If container IsNot Nothing Then Return container

        Dim index = itemsControl.Items.IndexOf(data)
        If index < 0 Then Return Nothing

        itemsControl.UpdateLayout() 'ensure the items host panel itself has been created before searching for it below

        Dim itemsHost = WPFToolbox.FindVisualChild(Of VirtualizingStackPanel)(itemsControl)
        itemsHost?.BringIndexIntoViewPublic(index) 'public wrapper for the otherwise-protected BringIndexIntoView

        itemsControl.UpdateLayout() 'let the panel realize the now-brought-into-view container

        Return TryCast(itemsControl.ItemContainerGenerator.ContainerFromItem(data), TreeViewItem)

    End Function


End Class


Public Class NavTreeDropHandler

    '-------------------------------------------------------------------------
    ' Drag & Drop logic (Gong: https://github.com/punker76/gong-wpf-dragdrop) 
    ' (implemented with NuGet gong-wpf-drag/drop package)
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

                ' drag experiment group node
                '---------------------------

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

                    ElseIf .InsertPosition = RelativeInsertPosition.AfterTargetItem Then
                        .Effects = DragDropEffects.Move
                        .DropTargetAdorner = DropTargetAdorners.Insert

                    End If

                ElseIf TypeOf .TargetItem Is tblExperiments Then

                    'drag over last experiment node of last project folder (i.e. for append)

                    Dim targetExp = CType(dropInfo.TargetItem, tblExperiments)
                    Dim lastFolderExp = (From exp In targetExp.ProjFolder.tblExperiments Order By exp.ExperimentID Descending).Last

                    'prevent inserting into another project
                    If targetExp.Project IsNot .Data.Project Then
                        Exit Sub
                    End If

                    If targetExp Is lastFolderExp Then

                        If .InsertPosition = RelativeInsertPosition.AfterTargetItem Then
                            .DropTargetAdorner = DropTargetAdorners.Insert
                            .Effects = DragDropEffects.Move
                        End If

                    End If

                End If

            ElseIf TypeOf .Data Is tblProjects Then

                ' drag project node
                '-------------------

                Dim dragProject = CType(.Data, tblProjects)

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

                ElseIf TypeOf .TargetItem Is tblProjFolders Then

                    'drag over the last folder of the last (bottom-most) project - reached instead of "TargetItem
                    'Is Nothing" below when that project's last folder is collapsed or has no experiments, i.e.
                    'for append at the very bottom of the whole project list (mirrors the tblExperiments case
                    'used by the project-folder drag above)

                    Dim targetFolder = CType(.TargetItem, tblProjFolders)
                    Dim lastProject = (From p In dragProject.User.tblProjects Order By p.SequenceNr Ascending).First
                    Dim lastProjectFolder = (From f In lastProject.tblProjFolders Order By f.SequenceNr Descending).LastOrDefault

                    If lastProjectFolder IsNot Nothing AndAlso targetFolder Is lastProjectFolder AndAlso
                      targetFolder.Project IsNot dragProject AndAlso .InsertPosition = RelativeInsertPosition.AfterTargetItem Then
                        .DropTargetAdorner = DropTargetAdorners.Insert
                        .Effects = DragDropEffects.Move
                    End If

                ElseIf TypeOf .TargetItem Is tblExperiments Then

                    'drag over the last experiment of the last folder of the last (bottom-most) project, i.e.
                    'for append at the very bottom of the whole project list

                    Dim targetExp = CType(.TargetItem, tblExperiments)
                    Dim lastProject = (From p In dragProject.User.tblProjects Order By p.SequenceNr Ascending).First
                    Dim lastProjectFolder = (From f In lastProject.tblProjFolders Order By f.SequenceNr Descending).LastOrDefault
                    Dim lastFolderExp = If(lastProjectFolder Is Nothing, Nothing,
                        (From exp In lastProjectFolder.tblExperiments Order By exp.ExperimentID Descending).LastOrDefault)

                    If lastFolderExp IsNot Nothing AndAlso targetExp Is lastFolderExp AndAlso
                      targetExp.Project IsNot dragProject Then
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

            'scroll the dropped experiment into view at its new location - under virtualization it's easy to
            'drop something into a folder that's currently nowhere near the scroll position it was dragged from
            WPFToolbox.FindVisualParent(Of ExperimentTree)(navTree)?.ScrollExperimentIntoView(dragExperiment)

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

                If dropInfo.InsertPosition = RelativeInsertPosition.AfterTargetItem Then
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


            ElseIf TypeOf dropInfo.TargetItem Is tblProjFolders OrElse TypeOf dropInfo.TargetItem Is tblExperiments _
              OrElse dropInfo.TargetItem Is Nothing Then

                ' append to the end of the collection - reached either via empty space below everything
                ' (TargetItem Is Nothing), or via the last folder / last experiment of the last project used
                ' as an append proxy when that project is expanded (see IDropTarget_DragOver)

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