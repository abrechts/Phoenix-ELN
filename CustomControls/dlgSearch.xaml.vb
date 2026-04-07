Imports System.Globalization
Imports System.Windows
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
    Public Property LocalDBContext As ElnDbContext
    Public Property ServerDBContext As ElnDbContext
    Private Property SearchContext As ElnDbContext

    Private Property QueryInfo As RSSQueryParameters
    Private Property RssHits As IEnumerable(Of tblExperiments)

    Private _cvsUsers As CollectionViewSource
    Private _cvsProjects As CollectionViewSource
    Private _cvsProjFolders As CollectionViewSource

    Private _suppressUIEvents As Boolean = False


    ''' <summary>
    ''' Defines the maximum number of reaction groups per result page.
    ''' </summary>
    Private Const ResultPageItems As Integer = 20

    ''' <summary>
    ''' Sets or gets the result index of the first reaction group displayed on the current result page.
    ''' </summary>
    Private Property ResultPageStartIndex As Integer = 0

    ''' <summary>
    ''' Sets or gets the current result page number (starting from 1).
    ''' </summary>
    Private Property ResultPageNr As Integer = 1

    ''' <summary>
    ''' Sets or gets the total number of hits (i.e. reaction groups).
    ''' </summary>
    Private Property TotalHitCount As Integer = 0

    ''' <summary>
    ''' Sets or gets the time taken to perform the RSS query in milliseconds, excluding filtering and rendering of the results.
    ''' </summary>
    Private Property rssQueryTime As Integer = 0    'in ms


    Private Sub Me_Loaded() Handles Me.Loaded

        SearchContext = LocalDBContext

        QueryInfo = New RSSQueryParameters

        _cvsUsers = Me.FindResource("UsersView")
        _cvsProjects = Me.FindResource("ProjectsView")
        _cvsProjFolders = Me.FindResource("ProjFoldersView")

        If ServerDBContext Is Nothing Then
            chkServerSearch.IsEnabled = False
            chkServerSearch.IsChecked = False
        Else
            chkServerSearch.IsChecked = My.Settings.IsServerQuery
        End If

        cboSorting.SelectedIndex = If(My.Settings.RssSortByYield, 0, 1)

        UpdateUsersFilter()

    End Sub


    Private Sub pnlQuerySketch_SketchEdited(sender As Object, skInfo As SketchResults) Handles pnlQuerySketch.SketchEdited

        QueryInfo.ReactionSketchXml = skInfo.NativeReactionXML

        btnSaveQuery.IsEnabled = False

        ' check for identical query reactant and product structures
        If skInfo.Reactants.First.InChIKey = skInfo.Products.First.InChIKey Then
            cbMsgBox.Display("Sorry, can't search for identical reactant and product!",
                    MsgBoxStyle.OkOnly + MsgBoxStyle.Exclamation, "Query structure error")
            lstRssHitGroups.DataContext = Nothing
            Exit Sub
        End If

        btnSaveQuery.IsEnabled = True

        RssHits = PerformRssQuery(skInfo.MDLRxnFileString)
        DisplayFilteredRss()

        blkNoHitsFound.Text = "---  no reactions found  ---"    'invisible by default, shown when a query returns no hits

    End Sub


    ''' <summary>
    ''' Performs the RSS query based on the provided MDL reactionFile string. No filters are applied at this stage.
    ''' </summary>
    '''
    Private Function PerformRssQuery(rxnFileString As String) As IEnumerable(Of tblExperiments)

        If String.IsNullOrEmpty(rxnFileString) Then
            Return Nothing
        End If

        Dim sw = Stopwatch.StartNew

        Dim rxnSub As New RxnSubstructure
        Dim rssRes = rxnSub.PerformRssQuery(rxnFileString, SearchContext)

        sw.Stop()
        rssQueryTime = sw.ElapsedMilliseconds

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


    ''' <summary>
    ''' Converts the current RSS hits (if any) into display groups based on the currently applied filters and displays them in the UI.
    ''' </summary>
    ''' 
    Private Sub DisplayFilteredRss(Optional isPageChange As Boolean = False)

        ' exit if no query sketch present
        If RssHits Is Nothing Then
            Exit Sub
        End If

        'go back to first results page if not just changing results page
        If Not isPageChange Then
            ResultPageStartIndex = 0
            ResultPageNr = 1
        End If

        RssItemGroup.IsServerResult = chkServerSearch.IsChecked

        'set global component structure color for obtained results
        SketchResults.ComponentStructureColor = Brushes.Black

        ' build query info object based on the current UI settings (sketch and filters)
        UpdateQueryInfo()

        Dim isSortedByYield = (cboSorting.SelectedIndex = 0)

        ' filter the current RSS hits based on the applied filters in the UI (user, project, group, yield and scale filters)
        Dim newRssQuery As New ReactionQuery(SearchContext, chkServerSearch.IsChecked)
        Dim filteredRssExp = newRssQuery.FilterRssHits(RssHits, QueryInfo)

        ' The multi-property grouping criterion is implemented by concatenating the reactant and product InChIKeys.
        ' Sorts internal hits per reaction by yield or scale
        Dim sameRxnGroups = filteredRssExp.GroupBy(Function(item) item.ReactantInChIKey + "/" + item.ProductInChIKey) _
                            .Select(Function(group) New With {
                                .MaxYield = group.Max(Function(item) item.Yield),
                                .MaxScale = group.Max(Function(item) item.RefReactantGrams),
                                .ExpEntries = If(isSortedByYield,
                                   group.OrderByDescending(Function(exp) exp.Yield),
                                   group.OrderByDescending(Function(exp) exp.RefReactantGrams))})

        'sort the groups based on the selected sorting criterion (yield or scale)
        If isSortedByYield Then
            sameRxnGroups = sameRxnGroups.OrderByDescending(Function(group) group.MaxYield)
        Else
            sameRxnGroups = sameRxnGroups.OrderByDescending(Function(group) group.MaxScale)
        End If

        TotalHitCount = sameRxnGroups.Count()

        'separate results into display groups
        sameRxnGroups = sameRxnGroups.Skip(ResultPageStartIndex).Take(ResultPageItems)

        Dim rssGroups As New List(Of RssRxnGroup)
        Dim index As Integer = 1

        For Each grp In sameRxnGroups
            Dim firstExp = grp.ExpEntries.First
            Dim rssRxnGroup As New RssRxnGroup
            Dim cbDrawInfo = DrawingEditor.GetSketchInfo(firstExp.RxnSketch)
            With rssRxnGroup
                .GroupTitle = "Reaction " + (ResultPageStartIndex + index).ToString
                .ReactCanvas = cbDrawInfo.Reactants.First.StructureCanvas
                .ProdCanvas = cbDrawInfo.Products.First.StructureCanvas
                .MaxYield = grp.MaxYield
                .MaxScale = grp.MaxScale
                .ExpItems = grp.ExpEntries
            End With
            rssGroups.Add(rssRxnGroup)
            index += 1
        Next

        lstRssHitGroups.DataContext = rssGroups

        scrlResults.ScrollToHome()

        resBorder.Margin = If(lstRssHitGroups.Items.Count > 0, New Thickness(0, 0, 0, 6), New Thickness(0, 30, 0, 6))

        UpdateQueryStats()

    End Sub


    ''' <summary>
    ''' Updates the query statistics information .
    ''' </summary>
    ''' 
    Private Sub UpdateQueryStats()

        Dim finalSectionNr As Integer = (TotalHitCount + ResultPageItems - 1) \ ResultPageItems

        pnlHitNavigation.IsEnabled = (TotalHitCount > ResultPageItems)

        If pnlHitNavigation.IsEnabled Then

            btnNext.IsEnabled = (ResultPageNr < finalSectionNr)
            btnToEnd.IsEnabled = (ResultPageNr < finalSectionNr)

            btnBack.IsEnabled = (ResultPageNr > 1)
            btnBackToStart.IsEnabled = (ResultPageNr > 1)

        End If

        Dim groupEndIndex = If(ResultPageNr = finalSectionNr, TotalHitCount, ResultPageStartIndex + ResultPageItems)
        blkHitInfo.Text = If(TotalHitCount > 0, $"{ResultPageStartIndex + 1} - {groupEndIndex} (of {TotalHitCount})", "")

        blkHitCount.Text = $"{TotalHitCount} reactions"
        blkQueryTime.Text = $"{rssQueryTime} ms"

    End Sub


    ''' <summary>
    ''' Result group button navigation
    ''' </summary>
    ''' 
    Private Sub btnNext_Click() Handles btnNext.Click

        ResultPageNr += 1
        ResultPageStartIndex = (ResultPageNr - 1) * ResultPageItems
        DisplayFilteredRss(isPageChange:=True)

    End Sub

    Private Sub btnToEnd_Click() Handles btnToEnd.Click

        Dim finalSectionNr As Integer = (TotalHitCount + ResultPageItems - 1) \ ResultPageItems

        ResultPageNr = finalSectionNr
        ResultPageStartIndex = (ResultPageNr - 1) * ResultPageItems
        DisplayFilteredRss(isPageChange:=True)

    End Sub

    Private Sub btnBack_Click() Handles btnBack.Click

        ResultPageNr -= 1
        ResultPageStartIndex = (ResultPageNr - 1) * ResultPageItems
        DisplayFilteredRss(isPageChange:=True)

    End Sub

    Private Sub btnBackToStart_Click() Handles btnBackToStart.Click

        DisplayFilteredRss(isPageChange:=False) 'specifying false resets to first page

    End Sub


    ''' <summary>
    ''' Saves the current query parameters (including the reaction sketch and applied filters) 
    ''' </summary>
    ''' 
    Private Sub btnSaveQuery_Click() Handles btnSaveQuery.Click

        If String.IsNullOrEmpty(QueryInfo.ReactionSketchXml) Then
            Exit Sub 'the substructure is required
        End If

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
            btnSaveQuery.IsEnabled = True
            blkNoHitsFound.Text = "---  no reactions found  ---"    'invisible by default, shown when a query returns no hits

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

        End With

    End Sub


    Private Sub LoadQueryInfo(filePath As String)

        Dim isServerMissing As Boolean = False
        Dim rxnFileStr As String

        Dim loadedQueryInfo = ReactionQuery.Load(SearchContext, filePath)

        If loadedQueryInfo IsNot Nothing Then

            QueryInfo = loadedQueryInfo

            With QueryInfo

                'draw query sketch and get MDL reaction file
                pnlQuerySketch.ReactionSketch = .ReactionSketchXml
                rxnFileStr = DrawingEditor.GetSketchInfo(.ReactionSketchXml).MDLRxnFileString

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

            RssHits = PerformRssQuery(rxnFileStr)
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

    End Sub


    Private Sub chkServerSearch_CheckedChanged() Handles chkServerSearch.Checked, chkServerSearch.Unchecked

        If chkServerSearch.IsInitialized AndAlso Not _suppressUIEvents Then

            InitializeSearchContext(chkServerSearch.IsChecked)

            'update setting after *manual* change only, i.e. not when server currently unavailable
            If chkServerSearch.IsMouseOver Then
                My.Settings.IsServerQuery = chkServerSearch.IsChecked
            End If

            If Not String.IsNullOrEmpty(QueryInfo.ReactionSketchXml) Then
                Dim rxnFileStr = DrawingEditor.GetSketchInfo(QueryInfo.ReactionSketchXml).MDLRxnFileString
                RssHits = PerformRssQuery(rxnFileStr)
            End If

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
            DisplayFilteredRss()

        End If

    End Sub


    Private Sub cboProjects_SelectionChanged() Handles cboProjects.SelectionChanged

        If Not _suppressUIEvents AndAlso cboProjects.IsInitialized Then

            If cboProjects.SelectedIndex > 0 Then
                Dim projectName = cboProjects.SelectedValue
                UpdateProjGroupsFilter(projectName)
            End If

            cboProjGroups.SelectedIndex = 0
            DisplayFilteredRss()

        End If

    End Sub


    Private Sub cboProjectGroups_SelectionChanged() Handles cboProjGroups.SelectionChanged

        If Not _suppressUIEvents AndAlso cboProjGroups.IsInitialized Then
            DisplayFilteredRss()
        End If

    End Sub


    Private Sub cboYieldComparer_SelectionChanged() Handles cboYieldComparer.SelectionChanged

        If Not _suppressUIEvents AndAlso cboYieldComparer.IsInitialized Then
            DisplayFilteredRss()
        End If

    End Sub


    Private Sub cboScaleComparer_SelectionChanged() Handles cboScaleComparer.SelectionChanged

        If Not _suppressUIEvents AndAlso cboScaleComparer.IsInitialized Then
            DisplayFilteredRss()
        End If

    End Sub


    Private Sub cboSorting_SelectionChanged() Handles cboSorting.SelectionChanged

        If Not _suppressUIEvents AndAlso cboSorting.IsInitialized Then

            DisplayFilteredRss()
            My.Settings.RssSortByYield = (cboSorting.SelectedIndex = 0)

        End If

    End Sub


    ''' <summary>
    ''' Refreshes the displayed RSS hits when the user finishes editing the yield or scale filter values. 
    ''' </summary>
    ''' 
    Private Sub Filters_LostKeyboardFocus() Handles txtYield.LostKeyboardFocus, txtScale.LostKeyboardFocus

        With QueryInfo
            If Not Equals(.YieldValue, txtYield.Value) OrElse Not Equals(.ScaleValue, txtScale.Value) Then
                DisplayFilteredRss()
            End If
        End With

    End Sub


    ''' <summary>
    ''' Finalizes editing of the yield or scale filter values
    ''' </summary>
    ''' 
    Private Sub Filters_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles txtYield.PreviewKeyDown, txtScale.PreviewKeyDown

        If e.Key = Key.Enter OrElse e.Key = Key.Return Then
            With QueryInfo
                If Not Equals(.YieldValue, txtYield.Value) OrElse Not Equals(.ScaleValue, txtScale.Value) Then
                    DisplayFilteredRss()
                End If
                Keyboard.ClearFocus()
            End With
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

        Dim e2 As New MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) With {
            .RoutedEvent = ListBox.MouseWheelEvent,
            .Source = e.Source
        }

        lstRssHitGroups.RaiseEvent(e2)

    End Sub


    Private Sub icoInfo_PreviewMouseUp() Handles icoInfo.PreviewMouseUp
        Dim info As New ProcessStartInfo("https://abrechts.github.io/phoenix-eln-help.github.io/pages/ReactionSearches.html") With {
            .UseShellExecute = True}
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
    Public Property MaxScale As Double?
    Public Property ExpItems As IOrderedEnumerable(Of tblExperiments)

End Class









