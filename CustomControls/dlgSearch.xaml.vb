Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Input
Imports System.Windows.Media
Imports ElnBase
Imports ElnBase.Search
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

    Private Property SearchContext As ElnDbContext   'assigned rdoLocal and rdoServer setting

    Private Property QueryRxnFileString As String

    Private _cvsUsers As CollectionViewSource
    Private _cvsProjects As CollectionViewSource
    Private _cvsProjFolders As CollectionViewSource

    Private _suppressQuery As Boolean = False


    Private Sub Me_Loaded() Handles Me.Loaded

        SearchContext = LocalDBContext

        _cvsUsers = Me.FindResource("UsersView")
        _cvsProjects = Me.FindResource("ProjectsView")
        _cvsProjFolders = Me.FindResource("ProjFoldersView")

        If ServerDBContext Is Nothing Then
            IsServerQuery = False
            chkServerSearch.IsEnabled = False
        Else
            chkServerSearch.IsChecked = IsServerQuery
        End If

        RefreshFilters()

    End Sub


    Private Sub pnlQuerySketch_SketchEdited(sender As Object, skInfo As SketchResults) Handles pnlQuerySketch.SketchEdited

        QueryRxnFileString = skInfo.MDLRxnFileString
        PerformQuery(skInfo.MDLRxnFileString)

    End Sub


    Private Sub btnSearch_Click() Handles btnSearch.Click

        PerformQuery(QueryRxnFileString)

    End Sub


    Private Sub PerformQuery(rxnFileStr As String)

        If _suppressQuery = True OrElse String.IsNullOrEmpty(rxnFileStr) Then
            Exit Sub
        End If

        'testing only
        Dim queryFilters = New RSSQueryFilters With {
            .UserID = If(cboUsers.SelectedIndex > 0, CStr(cboUsers.SelectedItem), String.Empty),
            .ProjectName = If(cboProjects.SelectedIndex > 0, CStr(cboProjects.SelectedItem), String.Empty),
            .ProjectGroupName = If(cboProjGroups.SelectedIndex > 0, CStr(cboProjGroups.SelectedItem), String.Empty),
            .MinYield = If(txtYield.Value, Double.MinValue),
            .MinScale = If(txtScale.Value, Double.MinValue)
        }

        ' perform filtered RSS query
        Dim newRssQuery As New Search(SearchContext, IsServerQuery)
        Dim hitExp = newRssQuery.FilteredRssQuery(rxnFileStr, queryFilters)

        If Not hitExp.Any Then
            lstRssHitGroups.DataContext = Nothing
            Dim finalizedStr = If(IsServerQuery, " - Only FINALIZED server experiments are listed.", "")
            cbMsgBox.Display("No matching experiments found." + finalizedStr, MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "No hits found")
            Exit Sub
        End If

        'the multi-property grouping criterion is implemented by concatenating the reactant and product InChIKeys 
        Dim sameRxnGroups = hitExp.GroupBy(Function(item) item.ReactantInChIKey + "/" + item.ProductInChIKey) _
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


        '  Me.UpdateLayout()

        blkResultsTitle.Visibility = Visibility.Visible
        lstRssHitGroups.Visibility = Visibility.Visible

        lstRssHitGroups.DataContext = rssGroups

    End Sub


    Private Sub chkServerSearch_CheckedChanged() Handles chkServerSearch.Checked, chkServerSearch.Unchecked

        If chkServerSearch.IsInitialized Then

            SearchContext = If(chkServerSearch.IsChecked, ServerDBContext, LocalDBContext)
            RefreshFilters()

            IsServerQuery = chkServerSearch.IsChecked

            PerformQuery(QueryRxnFileString)

        End If

    End Sub


    Private Sub cboUsers_SelectionChanged() Handles cboUsers.SelectionChanged

        If cboUsers.IsInitialized Then

            If cboUsers.SelectedIndex > 0 Then
                _suppressQuery = True
                cboProjects.SelectedIndex = 0
                cboProjGroups.SelectedIndex = 0
                _suppressQuery = False
            End If

            RefreshProjectsFilter()
            ' PerformQuery(QueryRxnFileString)

        End If

    End Sub


    Private Sub cboProjects_SelectionChanged() Handles cboProjects.SelectionChanged

        If cboProjects.IsInitialized Then

            _suppressQuery = True

            If cboProjects.SelectedIndex > 0 Then
                cboProjGroups.SelectedIndex = 0
            Else
                'no project specified: no project group associated with a specific project can be specified.
                cboProjGroups.SelectedIndex = 0
            End If

            _suppressQuery = False

            RefreshProjGroupsFilter()
            '  PerformQuery(QueryRxnFileString)

        End If

    End Sub


    Private Sub RefreshFilters()

        RefreshUsersFilter()
        RefreshProjectsFilter()
        RefreshProjGroupsFilter()

    End Sub


    Private Sub RefreshUsersFilter()

        _cvsUsers.Source = SearchContext.tblUsers _
            .Select(Function(p) p.UserID) _
            .OrderBy(Function(t) t) _ 'orders the list elements (strings) in ascending order directly
            .ToList()

        _cvsUsers.View.Refresh()

    End Sub


    Private Sub RefreshProjectsFilter()

        Dim parentUserName = If(cboUsers.SelectedIndex > 0, CType(cboUsers.SelectedValue, String), String.Empty)

        If String.IsNullOrEmpty(parentUserName) Then
            _cvsProjects.Source = SearchContext.tblProjects _
                .Select(Function(p) p.Title) _
                .ToList() _
                .Distinct(StringComparer.OrdinalIgnoreCase) _
                .OrderBy(Function(t) t) _
                .ToList()
        Else
            _cvsProjects.Source = SearchContext.tblProjects _
                .Where(Function(p) p.User.UserID = parentUserName) _
                .Select(Function(p) p.Title) _
                .ToList() _
                .Distinct(StringComparer.OrdinalIgnoreCase) _
                .OrderBy(Function(t) t) _
                .ToList()
        End If

        _cvsProjects.View.Refresh()

    End Sub


    Private Sub RefreshProjGroupsFilter()

        Dim parentProjectName = If(cboProjects.SelectedIndex > 0, CType(cboProjects.SelectedValue, String), String.Empty)

        If String.IsNullOrEmpty(parentProjectName) Then
            _cvsProjFolders.Source = SearchContext.tblProjFolders _
                .Select(Function(p) p.FolderName) _
                .ToList() _
                .Distinct(StringComparer.OrdinalIgnoreCase) _
                .OrderBy(Function(t) t) _
                .ToList()
        Else
            _cvsProjFolders.Source = SearchContext.tblProjFolders _
                .Where(Function(p) p.Project.Title = parentProjectName) _
                .Select(Function(p) p.FolderName) _
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

End Class


Public Class RssRxnGroup

    Public Property GroupTitle As String
    Public Property ReactCanvas As Canvas
    Public Property ProdCanvas As Canvas
    Public Property MaxYield As Double?
    Public Property ExpItems As IOrderedEnumerable(Of tblExperiments)

End Class




