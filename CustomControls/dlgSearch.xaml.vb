Imports System.Globalization
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Input
Imports System.Windows.Media
Imports ElnBase
Imports ElnBase.ReactionQuery
Imports ElnCoreModel

Public Class dlgSearch

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    ''' <summary>
    ''' Sets or gets if the current query is server based (true) or local (false).
    ''' </summary>
    '''
    Public Shared Property IsServerQuery As Boolean = False

    Public Property LocalDBContext As ElnDbContext
    Public Property ServerDBContext As ElnDbContext
    Private Property SearchContext As ElnDbContext

    Private Property QueryInfo As RSSQueryParameters
    Private Property RxnFileString As String

    Private Property RssHits As IEnumerable(Of tblExperiments)

    Private _cvsUsers As CollectionViewSource
    Private _cvsProjects As CollectionViewSource
    Private _cvsProjFolders As CollectionViewSource

    Private _suppressUIEvents As Boolean = False


    Private Sub Me_Loaded() Handles Me.Loaded

        SearchContext = LocalDBContext

        QueryInfo = New RSSQueryParameters

        _cvsUsers = Me.FindResource("UsersView")
        _cvsProjects = Me.FindResource("ProjectsView")
        _cvsProjFolders = Me.FindResource("ProjFoldersView")

        If ServerDBContext Is Nothing Then
            IsServerQuery = False
            chkServerSearch.IsEnabled = False
        Else
            chkServerSearch.IsChecked = IsServerQuery
        End If

        UpdateUsersFilter()

    End Sub


    Private Sub pnlQuerySketch_SketchEdited(sender As Object, skInfo As SketchResults) Handles pnlQuerySketch.SketchEdited

        QueryInfo.ReactionSketchXml = skInfo.NativeReactionXML

        RssHits = PerformRssQuery(skInfo.MDLRxnFileString)
        DisplayFilteredRss()

    End Sub


    Private Sub btnSearch_Click() Handles btnSearch.Click

        DisplayFilteredRss()

    End Sub


    ''' <summary>
    ''' Performs the RSS query based on the provided MDL reactionFile string. No filters are applied at this stage.
    ''' </summary>
    '''
    Private Function PerformRssQuery(rxnFileString As String) As IEnumerable(Of tblExperiments)

        If String.IsNullOrEmpty(rxnFileString) Then
            Return Nothing
        End If

        Dim rxnSub As New RxnSubstructure
        Dim rssRes = rxnSub.PerformRssQuery(rxnFileString, SearchContext, IsServerQuery)

        ' check for RSS-specific errors

        Select Case rssRes.ErrorType

            Case RssErrorType.None

                'all good

            Case RssErrorType.NoHitsFound

                'no substructure hits found 
                lstRssHitGroups.DataContext = Nothing

                Return Nothing

            Case RssErrorType.TooManyRxnHits

                'substructure search has too many fingerprint hits already
                cbMsgBox.Display("The query reaction sketch is too generic and would result in too many hits. Please refine the sketch and try again.",
                    MsgBoxStyle.OkOnly + MsgBoxStyle.Exclamation, "Too many hits")
                lstRssHitGroups.DataContext = Nothing

                Return Nothing

            Case RssErrorType.QueryStructureError

                'substructure search has too many fingerprint hits already
                cbMsgBox.Display("There was an error with the query reaction sketch. Please check the structure and try again.",
                    MsgBoxStyle.OkOnly + MsgBoxStyle.Exclamation, "Query structure error")
                lstRssHitGroups.DataContext = Nothing

                Return Nothing

        End Select

        Return rssRes.ExperimentHits


    End Function


    Private Sub DisplayFilteredRss()

        ' build query info object based on the current UI settings (sketch and filters)
        UpdateQueryInfo()

        ' filter the current RSS hits based on the applied filters in the UI (user, project, group, yield and scale filters)
        Dim newRssQuery As New ReactionQuery(SearchContext, IsServerQuery)
        Dim filteredRssExp = newRssQuery.FilterRssHits(RssHits, QueryInfo)

        'the multi-property grouping criterion is implemented by concatenating the reactant and product InChIKeys 
        Dim sameRxnGroups = filteredRssExp.GroupBy(Function(item) item.ReactantInChIKey + "/" + item.ProductInChIKey) _
                                 .Select(Function(group) New With {
                                     .MaxYield = group.Max(Function(item) item.Yield),
                                     .ExpEntries = group.OrderByDescending(Function(exp) exp.Yield)
                                 }).OrderByDescending(Function(group) group.MaxYield)

        'set global component structure color for obtained results
        SketchResults.ComponentStructureColor = Brushes.Black

        Dim rssGroups As New List(Of RssRxnGroup)
        Dim index As Integer = 1

        For Each grp In sameRxnGroups
            Dim firstExp = grp.ExpEntries.First
            Dim rssRxnGroup As New RssRxnGroup
            With rssRxnGroup
                Dim cbDrawInfo = DrawingEditor.GetSketchInfo(firstExp.RxnSketch)
                .GroupTitle = "Reaction " + index.ToString
                .ReactCanvas = cbDrawInfo.Reactants.First.StructureCanvas
                .ProdCanvas = cbDrawInfo.Products.First.StructureCanvas
                .MaxYield = grp.MaxYield
                .ExpItems = grp.ExpEntries
            End With
            rssGroups.Add(rssRxnGroup)
            index += 1
        Next

        lstRssHitGroups.DataContext = rssGroups

    End Sub


    ''' <summary>
    ''' Saves the current query parameters (including the reaction sketch and applied filters) 
    ''' </summary>
    ''' 
    Private Sub btnSaveQuery_Click() Handles btnSaveQuery.Click

        Dim saveFileDlg As New Microsoft.Win32.SaveFileDialog With {
            .Title = "Save the current query",
            .Filter = "Phoenix ELN queries (*.elnquery)|*.elnquery",
            .DefaultExt = "elnquery"
        }

        If saveFileDlg.ShowDialog() Then
            UpdateQueryInfo() 'ensure that the QueryInfo properties are up to date with the current UI settings before saving
            ReactionQuery.Save(QueryInfo, saveFileDlg.FileName)
        End If

    End Sub


    ''' <summary>
    ''' Loads query parameters (including the reaction sketch and applied filters) and performs the query
    ''' </summary>
    ''' 
    Private Sub btnLoadQuery_Click() Handles btnLoadQuery.Click

        Dim openFileDlg As New Microsoft.Win32.OpenFileDialog With {
            .Title = "Load a stored query",
            .Filter = "Phoenix ELN queries (*.elnquery)|*.elnquery",
            .DefaultExt = "elnquery"
        }

        If openFileDlg.ShowDialog() Then
            LoadQueryInfo(openFileDlg.FileName)
        End If

    End Sub


    ''' <summary>
    ''' Resets all filters to their default state ("any") and performs the query with the current sketch and default filters.
    ''' </summary>
    ''' 
    Private Sub btnResetFilters_Click() Handles btnResetFilters.Click

        _suppressUIEvents = True

        cboUsers.SelectedIndex = 0
        cboProjects.SelectedIndex = 0
        cboProjGroups.SelectedIndex = 0
        cboYieldComparer.SelectedIndex = 0
        txtYield.Value = Nothing
        cboScaleComparer.SelectedIndex = 0
        txtScale.Value = Nothing

        _suppressUIEvents = False

        UpdateQueryInfo()
        DisplayFilteredRss()

    End Sub


    ''' <summary>
    ''' Sets the QueryInfo properties based on the current UI settings (sketch and filters).
    ''' </summary>
    ''' 
    Private Sub UpdateQueryInfo()

        With QueryInfo

            .UserID = If(cboUsers.SelectedIndex > 0, CStr(cboUsers.SelectedItem), String.Empty)
            .ProjectName = If(cboProjects.SelectedIndex > 0, CStr(cboProjects.SelectedItem), String.Empty)
            .ProjectGroupName = If(cboProjGroups.SelectedIndex > 0, CStr(cboProjGroups.SelectedItem), String.Empty)

            .YieldValue = If(txtYield.Value, Nothing)
            .IsYieldSmallerOrEqual = (cboYieldComparer.SelectedIndex = 1)

            .ScaleValue = If(txtScale.Value, Nothing)
            .IsScaleSmallerOrEqual = (cboScaleComparer.SelectedIndex = 1)

            .IsServerQuery = IsServerQuery  'for determining the context (server vs local) of stored queries

        End With

    End Sub


    Private Sub LoadQueryInfo(filePath As String)

        Dim isServerMissing As Boolean = False

        Dim loadedQueryInfo = ReactionQuery.Load(SearchContext, filePath)

        If loadedQueryInfo IsNot Nothing Then

            QueryInfo = loadedQueryInfo

            With QueryInfo

                'determine if the loaded query is server-based but but no server connection is present.
                If .IsServerQuery AndAlso Not chkServerSearch.IsEnabled Then
                    cbMsgBox.Display("The loaded query is based on a server search, but no server connection is currently available." +
                        vbCrLf + vbCrLf + "The filters for user, project and group are reset to default.",
                        MsgBoxStyle.OkOnly + MsgBoxStyle.Exclamation, "Search context mismatch")
                    isServerMissing = True
                Else
                    _suppressUIEvents = True
                    chkServerSearch.IsChecked = .IsServerQuery
                    _suppressUIEvents = False
                End If

                'populate the user, project and group filters and resets them to "any"
                InitializeSearchContext(.IsServerQuery AndAlso Not isServerMissing)

                'draw query sketch and get MDL reaction file
                pnlQuerySketch.ReactionSketch = .ReactionSketchXml
                RxnFileString = DrawingEditor.GetSketchInfo(.ReactionSketchXml).MDLRxnFileString

                cboYieldComparer.SelectedIndex = If(.IsYieldSmallerOrEqual, 1, 0)
                txtYield.Value = .YieldValue
                cboScaleComparer.SelectedIndex = If(.IsScaleSmallerOrEqual, 1, 0)
                txtScale.Value = .ScaleValue

                'select navigation filters

                If Not isServerMissing Then

                    If .UserID IsNot String.Empty Then
                        Dim index = usersContainer.Collection.Cast(Of String)().ToList().
                            FindIndex(Function(x) String.Equals(x, .UserID, StringComparison.OrdinalIgnoreCase))
                        cboUsers.SelectedIndex = If(index >= 0, index + 2, 0)
                    End If

                    If .ProjectName IsNot String.Empty Then
                        Dim index = projectsContainer.Collection.Cast(Of String)().ToList().
                            FindIndex(Function(x) String.Equals(x, .ProjectName, StringComparison.OrdinalIgnoreCase))
                        cboProjects.SelectedIndex = If(index >= 0, index + 2, 0)
                    End If

                    If .ProjectGroupName IsNot String.Empty Then
                        Dim index = projectGroupsContainer.Collection.Cast(Of String)().ToList().
                            FindIndex(Function(x) String.Equals(x, .ProjectGroupName, StringComparison.OrdinalIgnoreCase))
                        cboProjGroups.SelectedIndex = If(index >= 0, index + 2, 0)
                    End If

                End If

            End With

            RssHits = PerformRssQuery(RxnFileString)
            DisplayFilteredRss()


        Else

            cbMsgBox.Display("The selected file could not be loaded as a query. Please make sure to open a valid .elnquery file.",
            MsgBoxStyle.OkOnly + MsgBoxStyle.Exclamation, "Error loading query")

        End If

    End Sub


    Private Sub InitializeSearchContext(isServerContext As Boolean)

        SearchContext = If(isServerContext, ServerDBContext, LocalDBContext)

        'reset all navigation filters since the search context changes
        _suppressUIEvents = True
        cboUsers.SelectedIndex = 0
        cboProjects.SelectedIndex = 0
        cboProjGroups.SelectedIndex = 0
        _suppressUIEvents = False

        ' cascades to update the other filters as well
        UpdateUsersFilter()

        IsServerQuery = isServerContext
        QueryInfo.IsServerQuery = isServerContext

    End Sub


    Private Sub chkServerSearch_CheckedChanged() Handles chkServerSearch.Checked, chkServerSearch.Unchecked

        If chkServerSearch.IsInitialized AndAlso Not _suppressUIEvents Then
            InitializeSearchContext(chkServerSearch.IsChecked)
            DisplayFilteredRss()
        End If

    End Sub


    Private Sub cboUsers_SelectionChanged() Handles cboUsers.SelectionChanged

        If Not _suppressUIEvents AndAlso cboUsers.IsInitialized Then

            If cboUsers.SelectedIndex > 0 Then
                _suppressUIEvents = True
                cboProjects.SelectedIndex = 0
                cboProjGroups.SelectedIndex = 0
                _suppressUIEvents = False
            End If

            Dim userName = If(cboUsers.SelectedIndex > 0, CType(cboUsers.SelectedValue, String), String.Empty)
            UpdateProjectsFilter(userName)

        End If

    End Sub


    Private Sub cboProjects_SelectionChanged() Handles cboProjects.SelectionChanged

        If Not _suppressUIEvents AndAlso cboProjects.IsInitialized Then

            If cboProjects.SelectedIndex > 0 Then
                Dim projectName = cboProjects.SelectedValue
                UpdateProjGroupsFilter(projectName)
            End If

            cboProjGroups.SelectedIndex = 0

        End If

    End Sub


    ''' <summary>
    ''' Updates the users filter options based on the current search context. The the projects filter is also updated, 
    ''' as it depends on the user filter selection. 
    ''' </summary>
    ''' 
    Private Sub UpdateUsersFilter()

        _cvsUsers.Source = SearchContext.tblUsers _
            .Select(Function(p) p.UserID) _
            .OrderBy(Function(t) t) _ 'orders the list elements (strings) in ascending order directly
            .ToList()

        _cvsUsers.View.Refresh()

        UpdateProjectsFilter("")

    End Sub


    ''' <summary>
    ''' Updates the projects filter options based on the current users filter selection. 
    ''' The project groups filter is also updated as it depends on the project filter selection.
    ''' </summary>
    ''' 
    Private Sub UpdateProjectsFilter(parentUserName As String)

        If String.IsNullOrEmpty(parentUserName) Then
            _cvsProjects.Source = SearchContext.tblProjects _
                .Select(Function(p) TitleCase(p.Title)) _
                .ToList() _
                .Distinct(StringComparer.OrdinalIgnoreCase) _
                .OrderBy(Function(t) t) _
                .ToList()
        Else
            _cvsProjects.Source = SearchContext.tblProjects _
                .Where(Function(p) p.User.UserID = parentUserName) _
                .Select(Function(p) TitleCase(p.Title)) _
                .ToList() _
                .Distinct(StringComparer.OrdinalIgnoreCase) _
                .OrderBy(Function(t) t) _
                .ToList()
        End If

        _cvsProjects.View.Refresh()

        Dim projectName = If(cboProjects.SelectedIndex > 0, CType(cboProjects.SelectedValue, String), String.Empty)
        UpdateProjGroupsFilter(projectName)

    End Sub


    ''' <summary>
    ''' Updates the project groups filter options based on the current projects filter selection. 
    ''' </summary>
    ''' 
    Private Sub UpdateProjGroupsFilter(parentProjectName As String)

        If String.IsNullOrEmpty(parentProjectName) Then
            'a project group cannot exist without a parent project
            _cvsProjFolders.Source = Enumerable.Empty(Of String)()
        Else
            _cvsProjFolders.Source = SearchContext.tblProjFolders _
            .Where(Function(p) p.Project.Title.ToLower = parentProjectName.ToLower) _
            .Select(Function(p) TitleCase(p.FolderName)) _
            .ToList() _
            .Distinct(StringComparer.OrdinalIgnoreCase) _
            .OrderBy(Function(t) t) _
            .ToList()
        End If

        _cvsProjFolders.View.Refresh()

    End Sub


    ''' <summary>
    ''' Allows the use of the MouseWheel for results scrolling
    ''' </summary>
    ''' 
    Private Sub lstHitGroups_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs) Handles lstRssHitGroups.PreviewMouseWheel

        e.Handled = True

        Dim e2 As New MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) With {
            .RoutedEvent = ListBox.MouseWheelEvent,
            .Source = e.Source
        }

        lstRssHitGroups.RaiseEvent(e2)

    End Sub


    Private Sub icoInfo_PreviewMouseUp() Handles icoInfo.PreviewMouseUp

        Dim info As New ProcessStartInfo("https://abrechts.github.io/phoenix-eln-help.github.io/pages/ReactionSearches.html")
        info.UseShellExecute = True
        Process.Start(info)

    End Sub


    Private Sub Me_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown

        If e.Key = Key.Escape Then
            Me.Close()
            Me.Hide()
        End If

    End Sub


    ''' <summary>
    ''' Converts the provided string to title case (first letter of each word capitalized).
    ''' </summary>
    ''' 
    Private Shared Function TitleCase(srcString As String) As String

        Dim ti = CultureInfo.CurrentCulture.TextInfo
        Return ti.ToTitleCase(srcString)

    End Function

End Class


Public Class RssRxnGroup

    Public Property GroupTitle As String
    Public Property ReactCanvas As Canvas
    Public Property ProdCanvas As Canvas
    Public Property MaxYield As Double?
    Public Property ExpItems As IOrderedEnumerable(Of tblExperiments)

End Class





